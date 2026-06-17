using System.Collections.Concurrent;
using DbShift.Core.Entities;
using DbShift.Core.Enums;
using DbShift.Core.Interfaces;
using DbShift.Core.ValueObjects;

namespace DbShift.Engine.Execution;

/// <summary>In-memory <see cref="IMigrationTracker"/> used for tests and offline workflows.</summary>
public sealed class InMemoryMigrationTracker : IMigrationTracker
{
    private readonly ConcurrentDictionary<(string Env, string Version), MigrationRecord> _store = new();

    public Task AddAsync(MigrationRecord record, CancellationToken cancellationToken = default)
    {
        record.Environment ??= string.Empty;
        _store[(record.Environment, record.Version)] = record;
        return Task.CompletedTask;
    }

    public Task<MigrationRecord?> GetByVersionAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((environment, version), out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<MigrationRecord>> GetAllAsync(string environment, CancellationToken cancellationToken = default)
    {
        var items = _store.Values.Where(r => r.Environment == environment)
            .OrderBy(r => r.Version)
            .ToList();
        return Task.FromResult<IReadOnlyList<MigrationRecord>>(items);
    }

    public Task<IReadOnlyList<MigrationRecord>> GetAppliedAsync(string environment, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(r => r.Environment == environment && r.Status == MigrationStatus.Completed)
            .OrderByDescending(r => r.Version)
            .ToList();
        return Task.FromResult<IReadOnlyList<MigrationRecord>>(items);
    }

    public Task UpdateStatusAsync(string environment, string version, MigrationStatus status, string? errorMessage, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue((environment, version), out var record))
        {
            record.Status = status;
            record.ErrorMessage = errorMessage;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        _store.TryRemove((environment, version), out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey((environment, version)));
    }
}

/// <summary>In-memory <see cref="IMigrationLockManager"/> used for tests and offline workflows.</summary>
public sealed class InMemoryMigrationLockManager : IMigrationLockManager
{
    private readonly ConcurrentDictionary<(string Env, string Key), MigrationLock> _locks = new();

    public Task<bool> AcquireAsync(string environment, string lockKey, string lockedBy, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var key = (environment, lockKey);
        if (_locks.TryGetValue(key, out var existing) && existing.IsActive && existing.ExpiresAtUtc > now)
        {
            return Task.FromResult(false);
        }

        _locks[key] = new MigrationLock
        {
            LockKey = lockKey,
            LockedBy = lockedBy,
            LockedAtUtc = now,
            ExpiresAtUtc = now.AddSeconds(Math.Max(1, timeoutSeconds)),
            Environment = environment,
            IsActive = true
        };
        return Task.FromResult(true);
    }

    public Task ReleaseAsync(string environment, string lockKey, CancellationToken cancellationToken = default)
    {
        if (_locks.TryGetValue((environment, lockKey), out var existing))
        {
            existing.IsActive = false;
        }
        return Task.CompletedTask;
    }

    public Task<bool> IsActiveAsync(string environment, string lockKey, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return Task.FromResult(_locks.TryGetValue((environment, lockKey), out var existing)
                               && existing.IsActive
                               && existing.ExpiresAtUtc > now);
    }
}

/// <summary>In-memory <see cref="IAuditLogger"/> used for tests and offline workflows.</summary>
public sealed class InMemoryAuditLogger : IAuditLogger
{
    private readonly ConcurrentQueue<MigrationAuditEntry> _entries = new();

    public Task LogAsync(MigrationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MigrationAuditEntry>> GetHistoryAsync(string environment, int limit, CancellationToken cancellationToken = default)
    {
        var items = _entries
            .Where(e => e.Environment == environment)
            .OrderByDescending(e => e.PerformedAtUtc)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<MigrationAuditEntry>>(items);
    }
}

/// <summary>In-memory <see cref="IEnvironmentProvider"/> used for tests and offline workflows.</summary>
public sealed class InMemoryEnvironmentProvider : IEnvironmentProvider
{
    private readonly HashSet<string> _environments = new(StringComparer.OrdinalIgnoreCase) { "development", "local", "qa", "production" };

    public Task<EnvironmentConfiguration> GetEnvironmentAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EnvironmentConfiguration
        {
            Name = name,
            Database = new DatabaseEndpoint { Host = "localhost", Port = 5432, Name = $"{name}_db", Schema = "public" },
            Migration = new MigrationEnvironmentSettings { RequireApproval = false, AllowRollback = true, LockTimeoutSeconds = 30, MaxBatchSize = 10 }
        });
    }

    public Task<bool> EnvironmentExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_environments.Contains(name));
    }

    public Task<IReadOnlyList<string>> GetAvailableEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(_environments.OrderBy(e => e).ToArray());
    }
}
