namespace DbShift.Core.ValueObjects;

/// <summary>Global migration configuration loaded from <c>migration.json</c>.</summary>
public sealed class MigrationConfiguration
{
    public string Version { get; set; } = "1.0.0";

    /// <summary>Database provider: "postgresql", "sqlserver", "mysql", or "sqlite".</summary>
    public string Provider { get; set; } = "postgresql";

    public string ConnectionString { get; set; } = string.Empty;
    public string ScriptsPath { get; set; } = "./Database/Migrations";
    public string Pattern { get; set; } = "V*__*.sql";
    public string RollbackPattern { get; set; } = "U*__*.sql";
    public string TrackingSchema { get; set; } = "public";
    public string TrackingTable { get; set; } = "__migration_history";
    public int LockTimeoutSeconds { get; set; } = 300;
    public int CommandTimeoutSeconds { get; set; } = 3600;
    public int BatchSize { get; set; } = 10;
    public bool StopOnFailure { get; set; } = true;
    public IReadOnlyList<string> RequireApprovalEnvironments { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Approvers { get; set; } = Array.Empty<string>();
}

/// <summary>Database endpoint resolved for a specific environment.</summary>
public sealed class DatabaseEndpoint
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = "public";
    public string? ConnectionString { get; set; }
}

/// <summary>Migration policy settings scoped to an environment.</summary>
public sealed class MigrationEnvironmentSettings
{
    public bool RequireApproval { get; set; }
    public bool AllowRollback { get; set; } = true;
    public int LockTimeoutSeconds { get; set; } = 300;
    public int MaxBatchSize { get; set; } = 10;
    public IReadOnlyList<string>? AllowedRoles { get; set; }
}

/// <summary>Optional deployment window restriction for an environment.</summary>
public sealed class DeploymentWindow
{
    public bool Enabled { get; set; }
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "23:59";
    public IReadOnlyList<string> AllowedDays { get; set; } = Array.Empty<string>();
}

/// <summary>Full configuration for a single named environment.</summary>
public sealed class EnvironmentConfiguration
{
    public string Name { get; set; } = string.Empty;
    public DatabaseEndpoint Database { get; set; } = new();
    public MigrationEnvironmentSettings Migration { get; set; } = new();
    public DeploymentWindow? DeploymentWindow { get; set; }
    public IReadOnlyList<string>? AllowedRoles => Migration.AllowedRoles;
}
