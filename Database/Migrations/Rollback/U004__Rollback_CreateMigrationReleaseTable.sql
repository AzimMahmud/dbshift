-- Rollback: Create Migration Release Table
-- Author: System
-- Created: 2026-06-16
-- Description: Rolls back V004__CreateMigrationReleaseTable.sql

DROP INDEX IF EXISTS idx_migration_release_version;
DROP INDEX IF EXISTS idx_migration_release_environment;
DROP INDEX IF EXISTS idx_migration_release_status;
DROP TABLE IF EXISTS __migration_release;
