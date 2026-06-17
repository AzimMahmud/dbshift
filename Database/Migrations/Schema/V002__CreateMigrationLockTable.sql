-- Migration: Create Migration Lock Table
-- Author: System
-- Created: 2026-06-16
-- Description: Creates the migration lock table for concurrency control

CREATE TABLE IF NOT EXISTS __migration_lock (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    lock_key            VARCHAR(100) NOT NULL UNIQUE,
    locked_by           VARCHAR(255) NOT NULL,
    locked_at_utc       TIMESTAMP NOT NULL DEFAULT NOW(),
    expires_at_utc      TIMESTAMP NOT NULL,
    environment         VARCHAR(50) NOT NULL,
    is_active           BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX idx_migration_lock_env_active ON __migration_lock(environment, is_active);
