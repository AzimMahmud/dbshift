using System.Data.Common;
using Npgsql;

namespace DbShift.Infrastructure.Database.Providers;

/// <summary>PostgreSQL provider backed by Npgsql.</summary>
public sealed class PostgreSqlProvider : IDatabaseProvider
{
    public string Name => "PostgreSQL";

    public DbConnection CreateConnection(string connectionString)
        => new NpgsqlConnection(connectionString);

    public DbParameter CreateParameter(string name, object? value)
        => new NpgsqlParameter(name, value ?? DBNull.Value);

    public string GetTrackingSchemaDdl() => """
        CREATE TABLE IF NOT EXISTS __migration_history (
            id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            version              VARCHAR(50)  NOT NULL,
            name                 VARCHAR(255) NOT NULL,
            script_name          VARCHAR(500) NOT NULL,
            script_hash          VARCHAR(64)  NOT NULL,
            migration_type       VARCHAR(20)  NOT NULL,
            category             VARCHAR(50)  NOT NULL,
            executed_by          VARCHAR(255) NOT NULL,
            executed_at_utc      TIMESTAMP    NOT NULL DEFAULT NOW(),
            execution_time_ms    INTEGER      NOT NULL,
            environment          VARCHAR(50)  NOT NULL,
            status               VARCHAR(20)  NOT NULL,
            rollback_available   BOOLEAN      NOT NULL DEFAULT FALSE,
            rollback_script_name VARCHAR(500),
            error_message        TEXT,
            execution_plan       TEXT,
            batch_number         INTEGER      NOT NULL DEFAULT 1,
            approved_by          VARCHAR(255),
            approved_at_utc      TIMESTAMP,
            checksum             VARCHAR(64)  NOT NULL,
            created_at_utc       TIMESTAMP    NOT NULL DEFAULT NOW(),
            CONSTRAINT uk_migration_version_env  UNIQUE (version, environment),
            CONSTRAINT uk_migration_script_name  UNIQUE (script_name, environment)
        );
        CREATE INDEX IF NOT EXISTS idx_migration_history_env        ON __migration_history(environment);
        CREATE INDEX IF NOT EXISTS idx_migration_history_status     ON __migration_history(status);
        CREATE INDEX IF NOT EXISTS idx_migration_history_executed   ON __migration_history(executed_at_utc);
        CREATE INDEX IF NOT EXISTS idx_migration_history_version    ON __migration_history(version);

        CREATE TABLE IF NOT EXISTS __migration_lock (
            id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            lock_key        VARCHAR(100) NOT NULL UNIQUE,
            locked_by       VARCHAR(255) NOT NULL,
            locked_at_utc   TIMESTAMP    NOT NULL DEFAULT NOW(),
            expires_at_utc  TIMESTAMP    NOT NULL,
            environment     VARCHAR(50)  NOT NULL,
            is_active       BOOLEAN      NOT NULL DEFAULT TRUE
        );
        CREATE INDEX IF NOT EXISTS idx_migration_lock_env_active ON __migration_lock(environment, is_active);

        CREATE TABLE IF NOT EXISTS __migration_audit (
            id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            migration_id     UUID REFERENCES __migration_history(id),
            action           VARCHAR(50)  NOT NULL,
            performed_by     VARCHAR(255) NOT NULL,
            performed_at_utc TIMESTAMP    NOT NULL DEFAULT NOW(),
            environment      VARCHAR(50)  NOT NULL,
            details          TEXT,
            ip_address       VARCHAR(45),
            user_agent       VARCHAR(500),
            request_id       UUID
        );
        CREATE INDEX IF NOT EXISTS idx_migration_audit_mid   ON __migration_audit(migration_id);
        CREATE INDEX IF NOT EXISTS idx_migration_audit_date  ON __migration_audit(performed_at_utc);
        CREATE INDEX IF NOT EXISTS idx_migration_audit_env   ON __migration_audit(environment);

        CREATE TABLE IF NOT EXISTS __migration_release (
            id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            release_version    VARCHAR(50)  NOT NULL UNIQUE,
            name               VARCHAR(255) NOT NULL,
            description        TEXT,
            created_by         VARCHAR(255) NOT NULL,
            created_at_utc     TIMESTAMP    NOT NULL DEFAULT NOW(),
            status             VARCHAR(20)  NOT NULL,
            target_environment VARCHAR(50)  NOT NULL,
            migration_ids      TEXT         NOT NULL,
            approved_by        VARCHAR(255),
            approved_at_utc    TIMESTAMP,
            deployed_by        VARCHAR(255),
            deployed_at_utc    TIMESTAMP,
            rolled_back_by     VARCHAR(255),
            rolled_back_at_utc TIMESTAMP,
            checksum           VARCHAR(64)  NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_migration_release_status ON __migration_release(status);
        CREATE INDEX IF NOT EXISTS idx_migration_release_env    ON __migration_release(target_environment);
        CREATE INDEX IF NOT EXISTS idx_migration_release_ver    ON __migration_release(release_version);
        """;
}
