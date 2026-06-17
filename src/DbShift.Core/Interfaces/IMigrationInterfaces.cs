using DbShift.Core.Entities;
using DbShift.Core.Enums;
using DbShift.Core.ValueObjects;

namespace DbShift.Core.Interfaces;

/// <summary>Persists and queries the migration history for an environment.</summary>
public interface IMigrationTracker
{
    Task AddAsync(MigrationRecord record, CancellationToken cancellationToken = default);
    Task<MigrationRecord?> GetByVersionAsync(string environment, string version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MigrationRecord>> GetAllAsync(string environment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MigrationRecord>> GetAppliedAsync(string environment, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string environment, string version, MigrationStatus status, string? errorMessage, CancellationToken cancellationToken = default);
    Task DeleteAsync(string environment, string version, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string environment, string version, CancellationToken cancellationToken = default);
}

/// <summary>Manages distributed locks to serialise concurrent migration runs.</summary>
public interface IMigrationLockManager
{
    Task<bool> AcquireAsync(string environment, string lockKey, string lockedBy, int timeoutSeconds, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string environment, string lockKey, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(string environment, string lockKey, CancellationToken cancellationToken = default);
}

/// <summary>Records auditable migration actions.</summary>
public interface IAuditLogger
{
    Task LogAsync(MigrationAuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MigrationAuditEntry>> GetHistoryAsync(string environment, int limit, CancellationToken cancellationToken = default);
}

/// <summary>Resolves environment-specific configuration.</summary>
public interface IEnvironmentProvider
{
    Task<EnvironmentConfiguration> GetEnvironmentAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> EnvironmentExistsAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAvailableEnvironmentsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Executes SQL scripts against the target database.</summary>
public interface IMigrationScriptExecutor
{
    Task<ScriptExecutionResult> ExecuteAsync(string connectionString, string sql, int timeoutSeconds, CancellationToken cancellationToken = default);
    Task EnsureTrackingSchemaAsync(string connectionString, CancellationToken cancellationToken = default);
}

/// <summary>Loads global and environment configuration from the file system.</summary>
public interface IConfigLoader
{
    MigrationConfiguration LoadMigrationConfiguration(string? basePath = null);
    EnvironmentConfiguration LoadEnvironment(string name, string? basePath = null);
    IReadOnlyList<string> GetAvailableEnvironments(string? basePath = null);
}

/// <summary>Outcome of executing a single SQL script.</summary>
public sealed class ScriptExecutionResult
{
    public bool IsSuccess { get; set; }
    public long ElapsedMs { get; set; }
    public string? ErrorMessage { get; set; }
}
