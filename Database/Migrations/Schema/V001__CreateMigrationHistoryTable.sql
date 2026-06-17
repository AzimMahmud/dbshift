-- Migration: Create Migration History Table
-- Author: System
-- Created: 2026-06-16
-- Description: Creates the migration history tracking table

CREATE TABLE IF NOT EXISTS __migration_history (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    version             VARCHAR(50) NOT NULL,
    name                VARCHAR(255) NOT NULL,
    script_name         VARCHAR(500) NOT NULL,
    script_hash         VARCHAR(64) NOT NULL,
    migration_type      VARCHAR(20) NOT NULL,
    category            VARCHAR(50) NOT NULL,
    executed_by         VARCHAR(255) NOT NULL,
    executed_at_utc     TIMESTAMP NOT NULL DEFAULT NOW(),
    execution_time_ms   INTEGER NOT NULL,
    environment         VARCHAR(50) NOT NULL,
    status              VARCHAR(20) NOT NULL,
    rollback_available  BOOLEAN NOT NULL DEFAULT FALSE,
    rollback_script_name VARCHAR(500),
    error_message       TEXT,
    execution_plan      TEXT,
    batch_number        INTEGER NOT NULL DEFAULT 1,
    approved_by         VARCHAR(255),
    approved_at_utc     TIMESTAMP,
    checksum            VARCHAR(64) NOT NULL,
    created_at_utc      TIMESTAMP NOT NULL DEFAULT NOW(),
    
    CONSTRAINT uk_migration_version_env UNIQUE (version, environment),
    CONSTRAINT uk_migration_script_name UNIQUE (script_name, environment)
);

CREATE INDEX idx_migration_history_env ON __migration_history(environment);
CREATE INDEX idx_migration_history_status ON __migration_history(status);
CREATE INDEX idx_migration_history_executed_at ON __migration_history(executed_at_utc);
CREATE INDEX idx_migration_history_version ON __migration_history(version);
