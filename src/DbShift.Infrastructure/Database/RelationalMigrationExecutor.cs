using System.Data.Common;
using System.Diagnostics;
using DbShift.Core.Interfaces;
using DbShift.Infrastructure.Database.Providers;

namespace DbShift.Infrastructure.Database;

/// <summary>
/// Executes raw SQL migration scripts inside an idempotent transaction.
/// Uses <see cref="System.Data.Common"/> abstractions so it works with any provider.
/// </summary>
public sealed class RelationalMigrationExecutor : IMigrationScriptExecutor
{
    private readonly IDatabaseProvider _provider;

    public RelationalMigrationExecutor(IDatabaseProvider provider)
    {
        _provider = provider;
    }

    public async Task<ScriptExecutionResult> ExecuteAsync(string connectionString, string sql, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var connection = _provider.CreateConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = Math.Max(1, timeoutSeconds);
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ScriptExecutionResult { IsSuccess = true, ElapsedMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            return new ScriptExecutionResult { IsSuccess = false, ElapsedMs = stopwatch.ElapsedMilliseconds, ErrorMessage = ex.Message };
        }
    }

    public async Task EnsureTrackingSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = _provider.CreateConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = _provider.GetTrackingSchemaDdl();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
