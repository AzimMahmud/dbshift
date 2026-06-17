using DbShift.Core.Enums;

namespace DbShift.Core.Entities;

/// <summary>
/// A persisted row in <c>__migration_history</c> describing the execution of one
/// migration script within a single environment.
/// </summary>
public sealed class MigrationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ScriptName { get; set; } = string.Empty;
    public string ScriptHash { get; set; } = string.Empty;
    public MigrationType Type { get; set; } = MigrationType.Schema;
    public string Category { get; set; } = string.Empty;
    public string ExecutedBy { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    public long ExecutionTimeMs { get; set; }
    public string Environment { get; set; } = string.Empty;
    public MigrationStatus Status { get; set; } = MigrationStatus.Pending;
    public bool RollbackAvailable { get; set; }
    public string? RollbackScriptName { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExecutionPlan { get; set; }
    public int BatchNumber { get; set; } = 1;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A persisted row in <c>__migration_audit</c> describing a single auditable action.</summary>
public sealed class MigrationAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MigrationId { get; set; }
    public AuditAction Action { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public string Environment { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? RequestId { get; set; }
}

/// <summary>A persisted row in <c>__migration_lock</c> used for distributed concurrency control.</summary>
public sealed class MigrationLock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string LockKey { get; set; } = string.Empty;
    public string LockedBy { get; set; } = string.Empty;
    public DateTime LockedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public string Environment { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>A persisted row in <c>__migration_release</c> describing a coordinated release bundle.</summary>
public sealed class MigrationRelease
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReleaseVersion { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Draft;
    public string TargetEnvironment { get; set; } = string.Empty;
    public Guid[] MigrationIds { get; set; } = Array.Empty<Guid>();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? DeployedBy { get; set; }
    public DateTime? DeployedAtUtc { get; set; }
    public string? RolledBackBy { get; set; }
    public DateTime? RolledBackAtUtc { get; set; }
    public string Checksum { get; set; } = string.Empty;
}
