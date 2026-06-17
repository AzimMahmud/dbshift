using System.Data.Common;
using MySqlConnector;

namespace DbShift.Infrastructure.Database.Providers;

/// <summary>MySQL / MariaDB provider backed by MySqlConnector.</summary>
public sealed class MySqlProvider : IDatabaseProvider
{
    public string Name => "MySQL";

    public DbConnection CreateConnection(string connectionString)
        => new MySqlConnection(connectionString);

    public DbParameter CreateParameter(string name, object? value)
        => new MySqlParameter("@" + name, value ?? DBNull.Value);

    public string GetTrackingSchemaDdl() => """
        CREATE TABLE IF NOT EXISTS __migration_history (
            id                   CHAR(36)     NOT NULL PRIMARY KEY,
            version              VARCHAR(50)  NOT NULL,
            name                 VARCHAR(255) NOT NULL,
            script_name          VARCHAR(500) NOT NULL,
            script_hash          VARCHAR(64)  NOT NULL,
            migration_type       VARCHAR(20)  NOT NULL,
            category             VARCHAR(50)  NOT NULL,
            executed_by          VARCHAR(255) NOT NULL,
            executed_at_utc      DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
            execution_time_ms    INT          NOT NULL,
            environment          VARCHAR(50)  NOT NULL,
            status               VARCHAR(20)  NOT NULL,
            rollback_available   TINYINT(1)   NOT NULL DEFAULT 0,
            rollback_script_name VARCHAR(500),
            error_message        LONGTEXT,
            execution_plan       LONGTEXT,
            batch_number         INT          NOT NULL DEFAULT 1,
            approved_by          VARCHAR(255),
            approved_at_utc      DATETIME,
            checksum             VARCHAR(64)  NOT NULL,
            created_at_utc       DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
            UNIQUE KEY uk_migration_version_env (version, environment),
            UNIQUE KEY uk_migration_script_name (script_name, environment)
        );
        CREATE INDEX idx_migration_history_env    ON __migration_history(environment);
        CREATE INDEX idx_migration_history_status ON __migration_history(status);

        CREATE TABLE IF NOT EXISTS __migration_lock (
            id              CHAR(36)     NOT NULL PRIMARY KEY,
            lock_key        VARCHAR(100) NOT NULL,
            locked_by       VARCHAR(255) NOT NULL,
            locked_at_utc   DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
            expires_at_utc  DATETIME     NOT NULL,
            environment     VARCHAR(50)  NOT NULL,
            is_active       TINYINT(1)   NOT NULL DEFAULT 1,
            UNIQUE KEY uk_migration_lock_key (lock_key)
        );
        CREATE INDEX idx_migration_lock_env_active ON __migration_lock(environment, is_active);

        CREATE TABLE IF NOT EXISTS __migration_audit (
            id               CHAR(36)     NOT NULL PRIMARY KEY,
            migration_id     CHAR(36),
            action           VARCHAR(50)  NOT NULL,
            performed_by     VARCHAR(255) NOT NULL,
            performed_at_utc DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
            environment      VARCHAR(50)  NOT NULL,
            details          LONGTEXT,
            ip_address       VARCHAR(45),
            user_agent       VARCHAR(500),
            request_id       CHAR(36),
            CONSTRAINT fk_migration_audit FOREIGN KEY (migration_id) REFERENCES __migration_history(id)
        );
        CREATE INDEX idx_migration_audit_mid  ON __migration_audit(migration_id);
        CREATE INDEX idx_migration_audit_date ON __migration_audit(performed_at_utc);

        CREATE TABLE IF NOT EXISTS __migration_release (
            id                 CHAR(36)     NOT NULL PRIMARY KEY,
            release_version    VARCHAR(50)  NOT NULL,
            name               VARCHAR(255) NOT NULL,
            description        LONGTEXT,
            created_by         VARCHAR(255) NOT NULL,
            created_at_utc     DATETIME     NOT NULL DEFAULT (UTC_TIMESTAMP()),
            status             VARCHAR(20)  NOT NULL,
            target_environment VARCHAR(50)  NOT NULL,
            migration_ids      LONGTEXT     NOT NULL,
            approved_by        VARCHAR(255),
            approved_at_utc    DATETIME,
            deployed_by        VARCHAR(255),
            deployed_at_utc    DATETIME,
            rolled_back_by     VARCHAR(255),
            rolled_back_at_utc DATETIME,
            checksum           VARCHAR(64)  NOT NULL,
            UNIQUE KEY uk_migration_release_ver (release_version)
        );
        CREATE INDEX idx_migration_release_status ON __migration_release(status);
        CREATE INDEX idx_migration_release_env    ON __migration_release(target_environment);
        """;
}
