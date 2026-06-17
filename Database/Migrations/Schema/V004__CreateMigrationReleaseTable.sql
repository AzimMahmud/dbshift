-- Migration: Create Migration Release Table
-- Author: System
-- Created: 2026-06-16
-- Description: Creates the release tracking table

CREATE TABLE IF NOT EXISTS __migration_release (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    release_version     VARCHAR(50) NOT NULL UNIQUE,
    name                VARCHAR(255) NOT NULL,
    description         TEXT,
    created_by          VARCHAR(255) NOT NULL,
    created_at_utc      TIMESTAMP NOT NULL DEFAULT NOW(),
    status              VARCHAR(20) NOT NULL,
    target_environment  VARCHAR(50) NOT NULL,
    migration_ids       UUID[] NOT NULL,
    approved_by         VARCHAR(255),
    approved_at_utc     TIMESTAMP,
    deployed_by         VARCHAR(255),
    deployed_at_utc     TIMESTAMP,
    rolled_back_by      VARCHAR(255),
    rolled_back_at_utc  TIMESTAMP,
    checksum            VARCHAR(64) NOT NULL
);

CREATE INDEX idx_migration_release_status ON __migration_release(status);
CREATE INDEX idx_migration_release_environment ON __migration_release(target_environment);
CREATE INDEX idx_migration_release_version ON __migration_release(release_version);
