-- Migration: Create Migration Audit Table
-- Author: System
-- Created: 2026-06-16
-- Description: Creates the migration audit trail table

CREATE TABLE IF NOT EXISTS __migration_audit (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    migration_id        UUID REFERENCES __migration_history(id),
    action              VARCHAR(50) NOT NULL,
    performed_by        VARCHAR(255) NOT NULL,
    performed_at_utc    TIMESTAMP NOT NULL DEFAULT NOW(),
    environment         VARCHAR(50) NOT NULL,
    details             JSONB,
    ip_address          INET,
    user_agent          VARCHAR(500),
    request_id          UUID
);

CREATE INDEX idx_migration_audit_migration_id ON __migration_audit(migration_id);
CREATE INDEX idx_migration_audit_performed_at ON __migration_audit(performed_at_utc);
CREATE INDEX idx_migration_audit_environment ON __migration_audit(environment);
