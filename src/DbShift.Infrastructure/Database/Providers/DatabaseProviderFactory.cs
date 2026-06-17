namespace DbShift.Infrastructure.Database.Providers;

/// <summary>
/// Resolves the correct <see cref="IDatabaseProvider"/> from a config string.
/// Accepted aliases are lenient: "postgres", "npgsql", "pgsql" all map to PostgreSQL.
/// </summary>
public static class DatabaseProviderFactory
{
    public static IDatabaseProvider Create(string? providerName)
    {
        var key = (providerName ?? string.Empty).Trim().ToLowerInvariant();

        return key switch
        {
            "postgresql" or "postgres" or "npgsql" or "pgsql" => new PostgreSqlProvider(),
            "sqlserver" or "mssql" or "sql-server" or "sql server" => new SqlServerProvider(),
            "mysql" or "mariadb" or "maria" => new MySqlProvider(),
            "sqlite" => new SqliteProvider(),
            "" => new PostgreSqlProvider(),
            _ => throw new ArgumentException(
                $"Unknown database provider '{providerName}'. " +
                "Supported providers: postgresql, sqlserver, mysql, sqlite.")
        };
    }

    /// <summary>Returns the list of supported provider identifiers.</summary>
    public static readonly string[] SupportedProviders = { "postgresql", "sqlserver", "mysql", "sqlite" };
}
