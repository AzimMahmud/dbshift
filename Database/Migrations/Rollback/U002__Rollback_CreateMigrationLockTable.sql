-- Rollback: Create Migration Lock Table
-- Author: System
-- Created: 2026-06-16
-- Description: Rolls back V002__CreateMigrationLockTable.sql

DROP INDEX IF EXISTS idx_migration_lock_env_active;
DROP TABLE IF EXISTS __migration_lock;
