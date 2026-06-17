using System.Data.Common;
using DbShift.Core.Entities;
using DbShift.Core.Interfaces;
using DbShift.Infrastructure.Database.Providers;

namespace DbShift.Infrastructure.Database;

/// <summary>
/// Row-based distributed lock manager. Computes expiry timestamps in C# rather than
/// relying on engine-specific date arithmetic, so it works uniformly across providers.
/// </summary>
public sealed class RelationalMigrationLockManager : IMigrationLockManager
{
    private readonly IDatabaseProvider _provider;
    private readonly string _connectionString;

    public RelationalMigrationLockManager(IDatabaseProvider provider, string connectionString)
    {
        _provider = provider;
        _connectionString = connectionString;
    }

    public async Task<bool> AcquireAsync(string environment, string lockKey, string lockedBy, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddSeconds(Math.Max(1, timeoutSeconds));

        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Step 1: expire stale or inactive locks for this key.
        await using (var expireCommand = connection.CreateCommand())
        {
            expireCommand.CommandText = """
                UPDATE __migration_lock
                SET is_active = @false
                WHERE lock_key = @lock_key
                  AND (is_active = @false OR expires_at_utc < @now)
                """;
            expireCommand.Parameters.Add(_provider.CreateParameter("lock_key", lockKey));
            expireCommand.Parameters.Add(_provider.CreateParameter("false", false));
            expireCommand.Parameters.Add(_provider.CreateParameter("now", now));
            await expireCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        // Step 2: check whether an active lock still exists.
        bool canInsert;
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM __migration_lock
                    WHERE lock_key = @lock_key AND is_active = @true AND expires_at_utc > @now
                ) THEN 1 ELSE 0 END
                """;
            checkCommand.Parameters.Add(_provider.CreateParameter("lock_key", lockKey));
            checkCommand.Parameters.Add(_provider.CreateParameter("true", true));
            checkCommand.Parameters.Add(_provider.CreateParameter("now", now));
            var scalar = await checkCommand.ExecuteScalarAsync(cancellationToken);
            canInsert = scalar is null || !Convert.ToBoolean(scalar);
        }

        if (!canInsert)
        {
            return false;
        }

        // Step 3: insert the new lease.
        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO __migration_lock (id, lock_key, locked_by, locked_at_utc, expires_at_utc, environment, is_active)
            VALUES (@id, @lock_key, @locked_by, @locked_at_utc, @expires_at_utc, @environment, @true)
            """;
        insertCommand.Parameters.Add(_provider.CreateParameter("id", Guid.NewGuid()));
        insertCommand.Parameters.Add(_provider.CreateParameter("lock_key", lockKey));
        insertCommand.Parameters.Add(_provider.CreateParameter("locked_by", lockedBy));
        insertCommand.Parameters.Add(_provider.CreateParameter("locked_at_utc", now));
        insertCommand.Parameters.Add(_provider.CreateParameter("expires_at_utc", expiresAt));
        insertCommand.Parameters.Add(_provider.CreateParameter("environment", environment));
        insertCommand.Parameters.Add(_provider.CreateParameter("true", true));

        try
        {
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (DbException)
        {
            // Another process inserted a lock between our check and insert.
            return false;
        }
    }

    public async Task ReleaseAsync(string environment, string lockKey, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE __migration_lock SET is_active = @false WHERE lock_key = @lock_key";
        command.Parameters.Add(_provider.CreateParameter("lock_key", lockKey));
        command.Parameters.Add(_provider.CreateParameter("false", false));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsActiveAsync(string environment, string lockKey, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM __migration_lock
                WHERE lock_key = @lock_key AND is_active = @true AND expires_at_utc > @now
            ) THEN 1 ELSE 0 END
            """;
        command.Parameters.Add(_provider.CreateParameter("lock_key", lockKey));
        command.Parameters.Add(_provider.CreateParameter("true", true));
        command.Parameters.Add(_provider.CreateParameter("now", now));

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is not null && Convert.ToBoolean(scalar);
    }
}
