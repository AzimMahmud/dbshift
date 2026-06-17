using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace DbShift.Infrastructure.Database.Providers;

/// <summary>SQLite provider backed by Microsoft.Data.Sqlite.</summary>
public sealed class SqliteProvider : IDatabaseProvider
{
    public string Name => "SQLite";

    public DbConnection CreateConnection(string connectionString)
        => new SqliteConnection(connectionString);

    public DbParameter CreateParameter(string name, object? value)
        => new SqliteParameter("@" + name, value ?? DBNull.Value);

    public string GetTrackingSchemaDdl() => """
        CREATE TABLE IF NOT EXISTS __migration_history (
            id                   TEXT NOT NULL PRIMARY KEY,
            version              TEXT NOT NULL,
            name                 TEXT NOT NULL,
            script_name          TEXT NOT NULL,
            script_hash          TEXT NOT NULL,
            migration_type       TEXT NOT NULL,
            category             TEXT NOT NULL,
            executed_by          TEXT NOT NULL,
            executed_at_utc      TEXT NOT NULL,
            execution_time_ms    INTEGER NOT NULL,
            environment          TEXT NOT NULL,
            status               TEXT NOT NULL,
            rollback_available   INTEGER NOT NULL DEFAULT 0,
            rollback_script_name TEXT,
            error_message        TEXT,
            execution_plan       TEXT,
            batch_number         INTEGER NOT NULL DEFAULT 1,
            approved_by          TEXT,
            approved_at_utc      TEXT,
            checksum             TEXT NOT NULL,
            created_at_utc       TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS uk_migration_version_env ON __migration_history(version, environment);
        CREATE UNIQUE INDEX IF NOT EXISTS uk_migration_script_name ON __migration_history(script_name, environment);
        CREATE INDEX IF NOT EXISTS idx_migration_history_env    ON __migration_history(environment);
        CREATE INDEX IF NOT EXISTS idx_migration_history_status ON __migration_history(status);

        CREATE TABLE IF NOT EXISTS __migration_lock (
            id              TEXT NOT NULL PRIMARY KEY,
            lock_key        TEXT NOT NULL,
            locked_by       TEXT NOT NULL,
            locked_at_utc   TEXT NOT NULL,
            expires_at_utc  TEXT NOT NULL,
            environment     TEXT NOT NULL,
            is_active       INTEGER NOT NULL DEFAULT 1
        );
        CREATE UNIQUE INDEX IF NOT EXISTS uk_migration_lock_key ON __migration_lock(lock_key);
        CREATE INDEX IF NOT EXISTS idx_migration_lock_env_active ON __migration_lock(environment, is_active);

        CREATE TABLE IF NOT EXISTS __migration_audit (
            id               TEXT NOT NULL PRIMARY KEY,
            migration_id     TEXT,
            action           TEXT NOT NULL,
            performed_by     TEXT NOT NULL,
            performed_at_utc TEXT NOT NULL,
            environment      TEXT NOT NULL,
            details          TEXT,
            ip_address       TEXT,
            user_agent       TEXT,
            request_id       TEXT,
            FOREIGN KEY (migration_id) REFERENCES __migration_history(id)
        );
        CREATE INDEX IF NOT EXISTS idx_migration_audit_mid  ON __migration_audit(migration_id);
        CREATE INDEX IF NOT EXISTS idx_migration_audit_date ON __migration_audit(performed_at_utc);

        CREATE TABLE IF NOT EXISTS __migration_release (
            id                 TEXT NOT NULL PRIMARY KEY,
            release_version    TEXT NOT NULL,
            name               TEXT NOT NULL,
            description        TEXT,
            created_by         TEXT NOT NULL,
            created_at_utc     TEXT NOT NULL,
            status             TEXT NOT NULL,
            target_environment TEXT NOT NULL,
            migration_ids      TEXT NOT NULL,
            approved_by        TEXT,
            approved_at_utc    TEXT,
            deployed_by        TEXT,
            deployed_at_utc    TEXT,
            rolled_back_by     TEXT,
            rolled_back_at_utc TEXT,
            checksum           TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS uk_migration_release_ver ON __migration_release(release_version);
        CREATE INDEX IF NOT EXISTS idx_migration_release_status ON __migration_release(status);
        CREATE INDEX IF NOT EXISTS idx_migration_release_env    ON __migration_release(target_environment);
        """;
}
