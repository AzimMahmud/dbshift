using System.Data.Common;

namespace DbShift.Infrastructure.Database.Providers;

/// <summary>
/// Abstraction over a specific database engine. Provides connection creation,
/// parameter construction, and engine-specific tracking-schema DDL so that the
/// tracker, lock manager, audit logger, and executor can stay fully generic.
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>Human-readable name (e.g. "PostgreSQL", "SQL Server").</summary>
    string Name { get; }

    /// <summary>Creates a ready-to-open <see cref="DbConnection"/> for this engine.</summary>
    DbConnection CreateConnection(string connectionString);

    /// <summary>Creates a parameter, converting null values to <see cref="DBNull.Value"/>.</summary>
    DbParameter CreateParameter(string name, object? value);

    /// <summary>Returns the complete DDL that creates all four tracking tables, idempotently.</summary>
    string GetTrackingSchemaDdl();
}
