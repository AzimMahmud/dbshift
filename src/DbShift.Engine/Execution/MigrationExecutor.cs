using System.Diagnostics;
using System.Text.Json;
using DbShift.Core.Entities;
using DbShift.Core.Enums;
using DbShift.Core.Interfaces;
using DbShift.Core.ValueObjects;
using DbShift.Engine.Parsing;
using Microsoft.Extensions.Logging;

namespace DbShift.Engine.Execution;

/// <summary>
/// Orchestrates validation, planning, deployment, rollback and repair of migrations.
/// Acts as the application core that coordinates the tracker, lock manager, audit log,
/// environment provider and (optionally) the target-database script executor.
/// </summary>
public sealed class MigrationExecutor
{
    private readonly IMigrationTracker _tracker;
    private readonly IMigrationLockManager _lockManager;
    private readonly ScriptParser _parser;
    private readonly IEnvironmentProvider _environmentProvider;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<MigrationExecutor> _logger;
    private readonly IMigrationScriptExecutor? _scriptExecutor;
    private readonly string _scriptsPath;
    private readonly string? _connectionString;
    private readonly int _commandTimeoutSeconds;
    private List<ParsedMigration>? _discoveryCache;

    public MigrationExecutor(
        IMigrationTracker tracker,
        IMigrationLockManager lockManager,
        ScriptParser parser,
        IEnvironmentProvider environmentProvider,
        IAuditLogger auditLogger,
        ILogger<MigrationExecutor> logger,
        IMigrationScriptExecutor? scriptExecutor = null,
        string? connectionString = null,
        int commandTimeoutSeconds = 3600,
        string? scriptsPath = null)
    {
        _tracker = tracker;
        _lockManager = lockManager;
        _parser = parser;
        _environmentProvider = environmentProvider;
        _auditLogger = auditLogger;
        _logger = logger;
        _scriptExecutor = scriptExecutor;
        _connectionString = connectionString;
        _commandTimeoutSeconds = commandTimeoutSeconds;
        _scriptsPath = scriptsPath ?? ResolveDefaultScriptsPath();
    }

    /// <summary>Validates every script under the scripts path: naming, syntax, uniqueness and dependencies.</summary>
    public async Task<ValidationResult> ValidateAsync(string environment, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        var (migrations, errors) = DiscoverAllCore();
        _discoveryCache = migrations;

        result.Errors.AddRange(errors);
        result.ScriptsChecked = migrations.Count;

        var byVersion = new Dictionary<string, ParsedMigration>(StringComparer.OrdinalIgnoreCase);
        var fileNames = migrations.Select(m => m.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var migration in migrations.OrderBy(m => OrderKey(m.Version)))
        {
            // Rollback scripts (U-prefix) intentionally share a version with their forward
            // counterpart, so they are excluded from the version-uniqueness check.
            if (migration.Type != MigrationType.Rollback && !byVersion.TryAdd(migration.Version, migration))
            {
                result.Errors.Add($"Duplicate migration version '{migration.Version}' ({migration.FileName} conflicts with {byVersion[migration.Version].FileName}).");
            }

            if (!_parser.ValidateSyntax(migration.Content))
            {
                result.Errors.Add($"Migration '{migration.FileName}' has no executable content.");
            }

            foreach (var dependency in migration.Dependencies)
            {
                if (!fileNames.Contains(dependency))
                {
                    result.Errors.Add($"Migration '{migration.FileName}' depends on missing script '{dependency}'.");
                }
            }
        }

        result.IsValid = result.Errors.Count == 0;
        await AuditSafe(environment, AuditAction.Validate, performedBy: "system",
            details: result.IsValid ? $"validated {result.ScriptsChecked} scripts" : $"{result.Errors.Count} error(s)");

        _logger.LogInformation("Validation for {Environment}: {Status} ({Count} scripts, {Errors} errors)",
            environment, result.IsValid ? "valid" : "invalid", result.ScriptsChecked, result.Errors.Count);

        return result;
    }

    /// <summary>Computes the ordered set of migrations that would be applied without touching the database.</summary>
    public async Task<DryRunResult> DryRunAsync(MigrationContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = await ComputePendingAsync(context.Environment, cancellationToken);
            var plan = new MigrationExecutionPlan
            {
                Environment = context.Environment,
                TotalCount = pending.Count,
                Items = pending.Select(m => new MigrationExecutionItem
                {
                    Version = m.Version,
                    Name = m.Name,
                    ScriptPath = m.FilePath,
                    Hash = m.Hash,
                    Type = m.Type,
                    Category = m.Category,
                    HasRollback = HasRollbackFor(m.Version)
                }).ToArray()
            };

            await AuditSafe(context.Environment, AuditAction.DryRun, context.ExecutedBy, $"{plan.TotalCount} migration(s) planned");
            return DryRunResult.Success(plan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dry-run failed for {Environment}", context.Environment);
            return DryRunResult.Failure(ex.Message);
        }
    }

    /// <summary>Applies all pending migrations to the target environment within a distributed lock.</summary>
    public async Task<DeployResult> DeployAsync(MigrationContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DeployResult();
        var lockKey = $"migration:{context.Environment}";

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            result.ErrorMessage = "No connection string is configured. A database connection is required to deploy.";
            return result;
        }

        var env = await _environmentProvider.GetEnvironmentAsync(context.Environment, cancellationToken);
        var locked = await _lockManager.AcquireAsync(context.Environment, lockKey, context.ExecutedBy, env.Migration.LockTimeoutSeconds, cancellationToken);
        if (!locked)
        {
            result.ErrorMessage = $"Could not acquire migration lock for environment '{context.Environment}'. Another deployment may be in progress.";
            return result;
        }

        try
        {
            var pending = await ComputePendingAsync(context.Environment, cancellationToken);
            if (pending.Count == 0)
            {
                result.IsSuccess = true;
                await AuditSafe(context.Environment, AuditAction.Deploy, context.ExecutedBy, "nothing to deploy");
                return result;
            }

            if (_scriptExecutor is null)
            {
                result.ErrorMessage = "No script executor is configured. A database connection is required to deploy.";
                return result;
            }

            var batchSize = context.BatchSize ?? env.Migration.MaxBatchSize;
            var batchNumber = 1;
            foreach (var batch in pending.Chunk(Math.Max(1, batchSize)))
            {
                foreach (var migration in batch)
                {
                    await _tracker.UpdateStatusAsync(context.Environment, migration.Version, MigrationStatus.InProgress, null, cancellationToken);

                    var record = new MigrationRecord
                    {
                        Version = migration.Version,
                        Name = migration.Name,
                        ScriptName = migration.FileName,
                        ScriptHash = migration.Hash,
                        Type = migration.Type,
                        Category = migration.Category,
                        ExecutedBy = context.ExecutedBy,
                        Environment = context.Environment,
                        Status = MigrationStatus.InProgress,
                        RollbackAvailable = HasRollbackFor(migration.Version),
                        RollbackScriptName = FindRollbackName(migration.Version),
                        BatchNumber = batchNumber,
                        Checksum = migration.Hash
                    };

                    var execution = await _scriptExecutor.ExecuteAsync(_connectionString!, migration.Content, _commandTimeoutSeconds, cancellationToken);
                    record.ExecutionTimeMs = execution.ElapsedMs;

                    if (execution.IsSuccess)
                    {
                        record.Status = MigrationStatus.Completed;
                        record.ExecutedAtUtc = DateTime.UtcNow;
                        await _tracker.AddAsync(record, cancellationToken);
                        result.AppliedMigrations.Add(migration.Version);
                        result.TotalApplied++;
                        _logger.LogInformation("Applied {Version} {Name} in {Ms}ms", migration.Version, migration.Name, execution.ElapsedMs);
                    }
                    else
                    {
                        record.Status = MigrationStatus.Failed;
                        record.ErrorMessage = execution.ErrorMessage;
                        await _tracker.AddAsync(record, cancellationToken);
                        result.FailedMigrations.Add(migration.Version);
                        _logger.LogError("Migration {Version} failed: {Error}", migration.Version, execution.ErrorMessage);

                        if (context.StopOnFailure)
                        {
                            result.ErrorMessage = $"Migration '{migration.Version}' failed: {execution.ErrorMessage}";
                            return result;
                        }
                    }
                }
                batchNumber++;
            }

            result.IsSuccess = result.FailedMigrations.Count == 0;
            await AuditSafe(context.Environment, AuditAction.Deploy, context.ExecutedBy,
                $"applied {result.TotalApplied}, failed {result.FailedMigrations.Count}");
            return result;
        }
        finally
        {
            await _lockManager.ReleaseAsync(context.Environment, lockKey, cancellationToken);
            stopwatch.Stop();
            result.Elapsed = stopwatch.Elapsed;
        }
    }

    /// <summary>Repairs a failed migration by re-queuing it as pending (and optionally removing its failed record).</summary>
    public async Task<RepairResult> RepairAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        var result = new RepairResult();
        var record = await _tracker.GetByVersionAsync(environment, version, cancellationToken);

        if (record is null)
        {
            result.IsSuccess = true;
            return result;
        }

        if (record.Status == MigrationStatus.Failed)
        {
            await _tracker.DeleteAsync(environment, version, cancellationToken);
            result.RepairedMigrations.Add(version);
        }
        else
        {
            await _tracker.UpdateStatusAsync(environment, version, record.Status, null, cancellationToken);
        }

        result.IsSuccess = true;
        await AuditSafe(environment, AuditAction.Repair, "system", $"repaired {version}");
        return result;
    }

    /// <summary>Rolls back one or more previously applied migrations using their rollback scripts.</summary>
    public async Task<RollbackResult> RollbackAsync(RollbackRequest request, CancellationToken cancellationToken = default)
    {
        var result = new RollbackResult();

        var applied = await _tracker.GetAppliedAsync(request.Environment, cancellationToken);
        if (applied.Count == 0)
        {
            result.IsSuccess = true;
            return result;
        }

        var targets = ResolveRollbackTargets(request, applied).ToList();
        if (targets.Count == 0)
        {
            result.IsSuccess = true;
            return result;
        }

        if (string.IsNullOrWhiteSpace(_connectionString) || _scriptExecutor is null)
        {
            result.ErrorMessage = "No database connection is configured. A connection is required to roll back.";
            return result;
        }

        var rollbacks = DiscoverRollbacks();
        foreach (var target in targets)
        {
            var rollback = rollbacks.FirstOrDefault(r => r.Version.Equals(target.Version, StringComparison.OrdinalIgnoreCase));
            if (rollback is null)
            {
                result.ErrorMessage = $"No rollback script found for migration '{target.Version}'.";
                return result;
            }

            var execution = await _scriptExecutor.ExecuteAsync(_connectionString, rollback.Content, _commandTimeoutSeconds, cancellationToken);
            if (!execution.IsSuccess)
            {
                result.ErrorMessage = $"Rollback of '{target.Version}' failed: {execution.ErrorMessage}";
                return result;
            }

            await _tracker.UpdateStatusAsync(request.Environment, target.Version, MigrationStatus.RolledBack, null, cancellationToken);
            result.RolledBackMigrations.Add(target.Version);
        }

        result.IsSuccess = true;
        await AuditSafe(request.Environment, AuditAction.Rollback, request.ExecutedBy,
            $"rolled back {result.RolledBackMigrations.Count}");
        return result;
    }

    /// <summary>Returns the migration status summary for an environment.</summary>
    public async Task<StatusResult> GetStatusAsync(string environment, CancellationToken cancellationToken = default)
    {
        var records = await _tracker.GetAllAsync(environment, cancellationToken);
        return new StatusResult
        {
            Environment = environment,
            Records = records,
            Total = records.Count,
            Applied = records.Count(r => r.Status == MigrationStatus.Completed),
            Pending = records.Count(r => r.Status is MigrationStatus.Pending or MigrationStatus.InProgress),
            Failed = records.Count(r => r.Status == MigrationStatus.Failed)
        };
    }

    /// <summary>Returns recent audit entries for an environment.</summary>
    public Task<IReadOnlyList<MigrationAuditEntry>> GetHistoryAsync(string environment, int limit, CancellationToken cancellationToken = default)
        => _auditLogger.GetHistoryAsync(environment, limit, cancellationToken);

    /// <summary>Creates the tracking schema on the target database.</summary>
    public async Task<InitResult> InitAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || _scriptExecutor is null)
        {
            return new InitResult { ErrorMessage = "No database connection is configured. A connection is required to initialise." };
        }

        await _scriptExecutor.EnsureTrackingSchemaAsync(_connectionString, cancellationToken);
        return new InitResult { IsSuccess = true, CreatedObjects = { "__migration_history", "__migration_lock", "__migration_audit", "__migration_release" } };
    }

    private async Task<List<ParsedMigration>> ComputePendingAsync(string environment, CancellationToken cancellationToken)
    {
        var versioned = DiscoverVersioned();
        if (versioned.Count == 0)
        {
            return new List<ParsedMigration>();
        }

        var applied = await _tracker.GetAppliedAsync(environment, cancellationToken);
        var appliedVersions = applied.Select(r => r.Version).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return versioned
            .Where(m => !appliedVersions.Contains(m.Version))
            .OrderBy(m => OrderKey(m.Version))
            .ToList();
    }

    private (List<ParsedMigration> Migrations, List<string> Errors) DiscoverAllCore()
    {
        var migrations = new List<ParsedMigration>();
        var errors = new List<string>();

        if (!Directory.Exists(_scriptsPath))
        {
            return (migrations, errors);
        }

        foreach (var file in Directory.EnumerateFiles(_scriptsPath, "*.sql", SearchOption.AllDirectories).OrderBy(f => f))
        {
            try
            {
                var content = File.ReadAllText(file);
                migrations.Add(_parser.Parse(file, content));
            }
            catch (FormatException ex)
            {
                errors.Add(ex.Message);
            }
            catch (IOException ex)
            {
                errors.Add($"Could not read '{file}': {ex.Message}");
            }
        }

        return (migrations, errors);
    }

    private List<ParsedMigration> DiscoverVersioned() =>
        GetCachedDiscovered()
            .Where(m => m.Type != MigrationType.Rollback && !m.IsRepeatable)
            .ToList();

    private List<ParsedMigration> DiscoverRollbacks() =>
        GetCachedDiscovered().Where(m => m.Type == MigrationType.Rollback).ToList();

    private List<ParsedMigration> GetCachedDiscovered()
    {
        if (_discoveryCache is null)
        {
            var (migrations, _) = DiscoverAllCore();
            _discoveryCache = migrations;
        }
        return _discoveryCache;
    }

    private bool HasRollbackFor(string version) => DiscoverRollbacks().Any(r => r.Version.Equals(version, StringComparison.OrdinalIgnoreCase));

    private string? FindRollbackName(string version) =>
        DiscoverRollbacks().FirstOrDefault(r => r.Version.Equals(version, StringComparison.OrdinalIgnoreCase))?.FileName;

    private static IEnumerable<MigrationRecord> ResolveRollbackTargets(RollbackRequest request, IReadOnlyList<MigrationRecord> applied)
    {
        if (!string.IsNullOrWhiteSpace(request.Version) &&
            !request.Version.Equals("last", StringComparison.OrdinalIgnoreCase))
        {
            return applied.Where(r => r.Version.Equals(request.Version, StringComparison.OrdinalIgnoreCase));
        }

        return applied.Take(Math.Max(1, request.Count));
    }

    private async Task AuditSafe(string environment, AuditAction action, string performedBy, string details)
    {
        try
        {
            await _auditLogger.LogAsync(new MigrationAuditEntry
            {
                Action = action,
                PerformedBy = string.IsNullOrEmpty(performedBy) ? "system" : performedBy,
                Environment = environment,
                Details = JsonSerializer.Serialize(new { details })
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry for {Action}", action);
        }
    }

    private static string OrderKey(string version) => version.PadLeft(20, '0');

    private static string ResolveDefaultScriptsPath()
    {
        var candidates = new[] { "./Database/Migrations", "../Database/Migrations", "../../Database/Migrations" };
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        return Path.GetFullPath("./Database/Migrations");
    }
}
