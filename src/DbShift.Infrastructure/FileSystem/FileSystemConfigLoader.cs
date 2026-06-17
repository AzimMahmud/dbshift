using System.Text.Json;
using System.Text.Json.Serialization;
using DbShift.Core.Interfaces;
using DbShift.Core.ValueObjects;

namespace DbShift.Infrastructure.FileSystem;

/// <summary>
/// Loads <c>Database/Config/migration.json</c> and the per-environment files under
/// <c>Database/Config/environments</c>, expanding <c>${VAR}</c> environment variables.
/// </summary>
public sealed class FileSystemConfigLoader : IConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly System.Text.RegularExpressions.Regex EnvironmentVariablesRegex =
        new(@"\$\{([A-Z0-9_]+)\}", System.Text.RegularExpressions.RegexOptions.IgnoreCase, System.TimeSpan.FromSeconds(5));

    private readonly string _baseDirectory;

    public FileSystemConfigLoader(string? baseDirectory = null)
    {
        _baseDirectory = ResolveBase(baseDirectory);
    }

    public string BaseDirectory => _baseDirectory;

    public MigrationConfiguration LoadMigrationConfiguration(string? basePath = null)
    {
        var root = ResolveBase(basePath ?? _baseDirectory);
        var path = Path.Combine(root, "Database", "Config", "migration.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Migration configuration not found at '{path}'. Run the CLI from the repository root or pass --config.", path);
        }

        var dto = JsonSerializer.Deserialize<MigrationConfigDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Migration configuration at '{path}' is empty or invalid.");
        var migration = dto.Migration ?? new MigrationConfigDto.MigrationDto();

        return new MigrationConfiguration
        {
            Version = migration.Version ?? "1.0.0",
            Provider = migration.Database?.Provider ?? "postgresql",
            ConnectionString = Expand(migration.Database?.ConnectionString),
            ScriptsPath = migration.Scripts?.Path ?? "./Database/Migrations",
            Pattern = migration.Scripts?.Pattern ?? "V*__*.sql",
            RollbackPattern = migration.Scripts?.RollbackPattern ?? "U*__*.sql",
            TrackingSchema = migration.Tracking?.Schema ?? "public",
            TrackingTable = migration.Tracking?.TableName ?? "__migration_history",
            LockTimeoutSeconds = migration.Execution?.LockTimeoutSeconds ?? 300,
            CommandTimeoutSeconds = migration.Execution?.CommandTimeoutSeconds ?? 3600,
            BatchSize = migration.Execution?.BatchSize ?? 10,
            StopOnFailure = migration.Execution?.StopOnFailure ?? true,
            RequireApprovalEnvironments = migration.Approval?.RequireApproval ?? new List<string>(),
            Approvers = migration.Approval?.Approvers ?? new List<string>()
        };
    }

    public EnvironmentConfiguration LoadEnvironment(string name, string? basePath = null)
    {
        var root = ResolveBase(basePath ?? _baseDirectory);
        var path = Path.Combine(root, "Database", "Config", "environments", $"{name}.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Environment '{name}' is not configured. Expected file at '{path}'.", path);
        }

        var dto = JsonSerializer.Deserialize<EnvironmentDto>(File.ReadAllText(path), JsonOptions)
                  ?? throw new InvalidDataException($"Environment configuration at '{path}' is empty or invalid.");

        var allowedRoles = dto.Migration?.AllowedRoles;
        return new EnvironmentConfiguration
        {
            Name = dto.Name ?? name,
            Database = new DatabaseEndpoint
            {
                Host = Expand(dto.Database?.Host) ?? "localhost",
                Port = dto.Database?.Port ?? 5432,
                Name = dto.Database?.Name ?? $"{name}_db",
                Schema = dto.Database?.Schema ?? "public"
            },
            Migration = new MigrationEnvironmentSettings
            {
                RequireApproval = dto.Migration?.RequireApproval ?? false,
                AllowRollback = dto.Migration?.AllowRollback ?? true,
                LockTimeoutSeconds = dto.Migration?.LockTimeoutSeconds ?? 300,
                MaxBatchSize = dto.Migration?.MaxBatchSize ?? 10,
                AllowedRoles = allowedRoles
            },
            DeploymentWindow = dto.DeploymentWindow is null
                ? null
                : new DeploymentWindow
                {
                    Enabled = dto.DeploymentWindow.Enabled,
                    StartTime = dto.DeploymentWindow.StartTime ?? "00:00",
                    EndTime = dto.DeploymentWindow.EndTime ?? "23:59",
                    AllowedDays = dto.DeploymentWindow.AllowedDays ?? new List<string>()
                }
        };
    }

    public IReadOnlyList<string> GetAvailableEnvironments(string? basePath = null)
    {
        var root = ResolveBase(basePath ?? _baseDirectory);
        var dir = Path.Combine(root, "Database", "Config", "environments");
        if (!Directory.Exists(dir))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(dir, "*.json")
            .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
            .OrderBy(n => n)
            .ToArray();
    }

    private static string ResolveBase(string? baseDirectory) =>
        string.IsNullOrWhiteSpace(baseDirectory) ? Directory.GetCurrentDirectory() : baseDirectory;

    private static string Expand(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return EnvironmentVariablesRegex.Replace(value, match =>
        {
            var name = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(name) ?? string.Empty;
        });
    }

    private sealed class MigrationConfigDto
    {
        [JsonPropertyName("migration")]
        public MigrationDto? Migration { get; set; }

        public sealed class MigrationDto
        {
            public string? Version { get; set; }
            public DatabaseDto? Database { get; set; }
            public ScriptsDto? Scripts { get; set; }
            public TrackingDto? Tracking { get; set; }
            public ExecutionDto? Execution { get; set; }
            public ApprovalDto? Approval { get; set; }
        }

        public sealed class DatabaseDto { public string? Provider { get; set; } public string? ConnectionString { get; set; } }
        public sealed class ScriptsDto { public string? Path { get; set; } public string? Pattern { get; set; } public string? RollbackPattern { get; set; } }
        public sealed class TrackingDto { public string? Schema { get; set; } public string? TableName { get; set; } }
        public sealed class ExecutionDto { public int? LockTimeoutSeconds { get; set; } public int? CommandTimeoutSeconds { get; set; } public int? BatchSize { get; set; } public bool? StopOnFailure { get; set; } }
        public sealed class ApprovalDto { public List<string>? RequireApproval { get; set; } public List<string>? Approvers { get; set; } }
    }

    private sealed class EnvironmentDto
    {
        public string? Name { get; set; }
        public DatabaseDto? Database { get; set; }
        public MigrationDto? Migration { get; set; }
        public DeploymentWindowDto? DeploymentWindow { get; set; }

        public sealed class DatabaseDto { public string? Host { get; set; } public int? Port { get; set; } public string? Name { get; set; } public string? Schema { get; set; } }
        public sealed class MigrationDto { public bool? RequireApproval { get; set; } public bool? AllowRollback { get; set; } public int? LockTimeoutSeconds { get; set; } public int? MaxBatchSize { get; set; } public List<string>? AllowedRoles { get; set; } }
        public sealed class DeploymentWindowDto { public bool Enabled { get; set; } public string? StartTime { get; set; } public string? EndTime { get; set; } public List<string>? AllowedDays { get; set; } }
    }
}
