namespace DbShift.Core.Enums;

/// <summary>Lifecycle state of a single migration within an environment.</summary>
public enum MigrationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    RolledBack
}

/// <summary>Classification of a migration script, derived from its filename and folder.</summary>
public enum MigrationType
{
    Schema,
    Data,
    Patch,
    Repeatable,
    Rollback
}

/// <summary>Auditable operations recorded against the migration history.</summary>
public enum AuditAction
{
    Init,
    Validate,
    DryRun,
    Deploy,
    Rollback,
    Repair,
    Create,
    Lock,
    Unlock,
    Approve
}

/// <summary>Supported database providers.</summary>
public enum DatabaseProvider
{
    PostgreSql,
    SqlServer,
    MySql,
    Sqlite
}

/// <summary>State machine for a coordinated migration release.</summary>
public enum ReleaseStatus
{
    Draft,
    PendingApproval,
    Approved,
    Deployed,
    RolledBack,
    Failed
}
