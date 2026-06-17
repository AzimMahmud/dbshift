using System.Data.Common;
using System.Runtime.CompilerServices;
using DbShift.Core.Entities;
using DbShift.Core.Enums;
using DbShift.Core.Interfaces;
using DbShift.Infrastructure.Database.Providers;

namespace DbShift.Infrastructure.Database;

/// <summary>
/// Provider-agnostic migration history tracker. Uses <see cref="System.Data.Common"/>
/// abstractions so the same code works against PostgreSQL, SQL Server, MySQL, and SQLite.
/// </summary>
public sealed class RelationalMigrationTracker : IMigrationTracker
{
    private readonly IDatabaseProvider _provider;
    private readonly string _connectionString;

    public RelationalMigrationTracker(IDatabaseProvider provider, string connectionString)
    {
        _provider = provider;
        _connectionString = connectionString;
    }

    public async Task AddAsync(MigrationRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Delete-then-insert gives us cross-database upsert semantics without
        // engine-specific ON CONFLICT / MERGE syntax.
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM __migration_history WHERE environment = @environment AND version = @version;

            INSERT INTO __migration_history
                (id, version, name, script_name, script_hash, migration_type, category, executed_by,
                 executed_at_utc, execution_time_ms, environment, status, rollback_available,
                 rollback_script_name, error_message, execution_plan, batch_number, approved_by,
                 approved_at_utc, checksum, created_at_utc)
            VALUES
                (@id, @version, @name, @script_name, @script_hash, @migration_type, @category, @executed_by,
                 @executed_at_utc, @execution_time_ms, @environment, @status, @rollback_available,
                 @rollback_script_name, @error_message, @execution_plan, @batch_number, @approved_by,
                 @approved_at_utc, @checksum, @created_at_utc)
            """;
        AddParameters(command, record);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<MigrationRecord?> GetByVersionAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM __migration_history WHERE environment = @environment AND version = @version";
        command.Parameters.Add(_provider.CreateParameter("environment", environment));
        command.Parameters.Add(_provider.CreateParameter("version", version));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<MigrationRecord>> GetAllAsync(string environment, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM __migration_history WHERE environment = @environment ORDER BY version";
        command.Parameters.Add(_provider.CreateParameter("environment", environment));

        return await ReadListAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<MigrationRecord>> GetAppliedAsync(string environment, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM __migration_history WHERE environment = @environment AND status = @status ORDER BY version DESC";
        command.Parameters.Add(_provider.CreateParameter("environment", environment));
        command.Parameters.Add(_provider.CreateParameter("status", MigrationStatus.Completed.ToString()));

        return await ReadListAsync(command, cancellationToken);
    }

    public async Task UpdateStatusAsync(string environment, string version, MigrationStatus status, string? errorMessage, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE __migration_history SET status = @status, error_message = @error_message WHERE environment = @environment AND version = @version";
        command.Parameters.Add(_provider.CreateParameter("status", status.ToString()));
        command.Parameters.Add(_provider.CreateParameter("error_message", (object?)errorMessage ?? DBNull.Value));
        command.Parameters.Add(_provider.CreateParameter("environment", environment));
        command.Parameters.Add(_provider.CreateParameter("version", version));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM __migration_history WHERE environment = @environment AND version = @version";
        command.Parameters.Add(_provider.CreateParameter("environment", environment));
        command.Parameters.Add(_provider.CreateParameter("version", version));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string environment, string version, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM __migration_history WHERE environment = @environment AND version = @version) THEN 1 ELSE 0 END";
        command.Parameters.Add(_provider.CreateParameter("environment", environment));
        command.Parameters.Add(_provider.CreateParameter("version", version));

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is not null && Convert.ToBoolean(scalar);
    }

    private async Task<IReadOnlyList<MigrationRecord>> ReadListAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var list = new List<MigrationRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(Map(reader));
        }
        return list;
    }

    private void AddParameters(DbCommand command, MigrationRecord r)
    {
        command.Parameters.Add(_provider.CreateParameter("id", r.Id));
        command.Parameters.Add(_provider.CreateParameter("version", r.Version));
        command.Parameters.Add(_provider.CreateParameter("name", r.Name));
        command.Parameters.Add(_provider.CreateParameter("script_name", r.ScriptName));
        command.Parameters.Add(_provider.CreateParameter("script_hash", r.ScriptHash));
        command.Parameters.Add(_provider.CreateParameter("migration_type", r.Type.ToString()));
        command.Parameters.Add(_provider.CreateParameter("category", r.Category));
        command.Parameters.Add(_provider.CreateParameter("executed_by", r.ExecutedBy));
        command.Parameters.Add(_provider.CreateParameter("executed_at_utc", r.ExecutedAtUtc));
        command.Parameters.Add(_provider.CreateParameter("execution_time_ms", r.ExecutionTimeMs));
        command.Parameters.Add(_provider.CreateParameter("environment", r.Environment));
        command.Parameters.Add(_provider.CreateParameter("status", r.Status.ToString()));
        command.Parameters.Add(_provider.CreateParameter("rollback_available", r.RollbackAvailable));
        command.Parameters.Add(_provider.CreateParameter("rollback_script_name", (object?)r.RollbackScriptName ?? DBNull.Value));
        command.Parameters.Add(_provider.CreateParameter("error_message", (object?)r.ErrorMessage ?? DBNull.Value));
        command.Parameters.Add(_provider.CreateParameter("execution_plan", (object?)r.ExecutionPlan ?? DBNull.Value));
        command.Parameters.Add(_provider.CreateParameter("batch_number", r.BatchNumber));
        command.Parameters.Add(_provider.CreateParameter("approved_by", (object?)r.ApprovedBy ?? DBNull.Value));
        command.Parameters.Add(_provider.CreateParameter("approved_at_utc", (object?)r.ApprovedAtUtc ?? DBNull.Value));
        command.Parameters.Add(_provider.CreateParameter("checksum", r.Checksum));
        command.Parameters.Add(_provider.CreateParameter("created_at_utc", r.CreatedAtUtc));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MigrationRecord Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("id")),
        Version = r.GetString(r.GetOrdinal("version")),
        Name = r.GetString(r.GetOrdinal("name")),
        ScriptName = r.GetString(r.GetOrdinal("script_name")),
        ScriptHash = r.GetString(r.GetOrdinal("script_hash")),
        Type = Enum.TryParse<MigrationType>(r.GetString(r.GetOrdinal("migration_type")), true, out var t) ? t : MigrationType.Schema,
        Category = r.GetString(r.GetOrdinal("category")),
        ExecutedBy = r.GetString(r.GetOrdinal("executed_by")),
        ExecutedAtUtc = r.GetDateTime(r.GetOrdinal("executed_at_utc")),
        ExecutionTimeMs = r.GetInt32(r.GetOrdinal("execution_time_ms")),
        Environment = r.GetString(r.GetOrdinal("environment")),
        Status = Enum.TryParse<MigrationStatus>(r.GetString(r.GetOrdinal("status")), true, out var s) ? s : MigrationStatus.Pending,
        RollbackAvailable = r.GetBoolean(r.GetOrdinal("rollback_available")),
        RollbackScriptName = r.IsDBNull(r.GetOrdinal("rollback_script_name")) ? null : r.GetString(r.GetOrdinal("rollback_script_name")),
        ErrorMessage = r.IsDBNull(r.GetOrdinal("error_message")) ? null : r.GetString(r.GetOrdinal("error_message")),
        ExecutionPlan = r.IsDBNull(r.GetOrdinal("execution_plan")) ? null : r.GetString(r.GetOrdinal("execution_plan")),
        BatchNumber = r.GetInt32(r.GetOrdinal("batch_number")),
        ApprovedBy = r.IsDBNull(r.GetOrdinal("approved_by")) ? null : r.GetString(r.GetOrdinal("approved_by")),
        ApprovedAtUtc = r.IsDBNull(r.GetOrdinal("approved_at_utc")) ? null : r.GetDateTime(r.GetOrdinal("approved_at_utc")),
        Checksum = r.GetString(r.GetOrdinal("checksum")),
        CreatedAtUtc = r.GetDateTime(r.GetOrdinal("created_at_utc"))
    };
}
