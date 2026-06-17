# Changelog

All notable changes to the DbShift project will be documented in this file.

## [1.0.0] — 2026-06-17

### Added

#### Professional tooling
- **CI workflow** (`.github/workflows/ci.yml`) — build, test, code coverage on ubuntu/windows/macos for every push and PR. Produces NuGet packages on main pushes.
- **Release workflow** (`.github/workflows/release.yml`) — triggered by `v*` tags. Builds self-contained binaries for 5 platforms (win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64), packages as `.zip`/`.tar.gz`, publishes to NuGet, creates a GitHub Release with release notes and all artifacts.
- **Dependabot** (`.github/dependabot.yml`) — weekly dependency updates for NuGet and GitHub Actions.
- **Issue templates** — structured bug report and feature request forms via `.github/ISSUE_TEMPLATE/`.
- **PR template** (`.github/pull_request_template.md`) — checklist for contributors covering build, tests, changelog, and docs.
- **EditorConfig** (`.editorconfig`) — consistent indentation and line endings across all file types.
- **CONTRIBUTING.md** — development setup, code conventions, commit style, and PR process.
- **CODE_OF_CONDUCT.md** — standard Contributor Covenant v2.1.
- **SECURITY.md** — vulnerability reporting process and supported versions.
- **Install scripts** — `install.sh` (Linux/macOS curl-bash) and `install.ps1` (Windows iwr-iex). Auto-detect platform, download from GitHub Releases, install to PATH.
- **Build scripts** — `publish.sh` (Linux/macOS) and `publish.ps1` (Windows) for building self-contained binaries locally.
- `dist/` added to `.gitignore`.

#### CLI — project scaffolding
- `dbshift new` — interactive project scaffold. Run without flags to be prompted for project name, database provider, and output directory. Creates the full directory tree, config files, per-environment settings, example migrations (provider-specific SQL), templates, `.gitignore`, and a GitHub Actions CI pipeline.
- `scaffold` / `init-project` aliases for `new`.

#### CLI — command-specific help
- `dbshift <command> --help` now shows options specific to that command, plus the global options (without duplication).

#### CLI — onboarding screen
- `dbshift` (no arguments) now shows a "Quick start" panel with the three most common commands before the full help table.

#### Multi-database provider support
- `IDatabaseProvider` interface with four implementations:
  - `PostgreSqlProvider` — PostgreSQL 12+ (Npgsql)
  - `SqlServerProvider` — SQL Server 2016+ (Microsoft.Data.SqlClient)
  - `MySqlProvider` — MySQL 8+ / MariaDB 10.5+ (MySqlConnector)
  - `SqliteProvider` — SQLite 3 (Microsoft.Data.Sqlite)
- `DatabaseProviderFactory` — resolves the correct provider by string alias.
- Provider override via `--provider` CLI flag or `migration.json → database.provider`.

#### Provider-agnostic infrastructure
- `RelationalMigrationTracker` — DELETE+INSERT upsert pattern (works on all four engines).
- `RelationalMigrationLockManager` — C# date math for lock expiry (no provider-specific SQL).
- `RelationalMigrationExecutor` — transaction-bound SQL execution via `System.Data.Common`.
- `RelationalAuditLogger` — parameterized INSERT for audit trail.
- `ConfigEnvironmentProvider` — environment config from JSON files.

#### Professional project assets
- `README.md` — comprehensive GitHub open-source README with badges, Quick Start, command table, script conventions, config reference, architecture diagram, multi-database explanation, and installation guide.
- `LICENSE` — MIT license.
- `CHANGELOG.md` — this file.
- `Directory.Build.props` — package metadata (authors, copyright, license).
- `docs/USAGE.md` — complete end-to-end usage guide covering installation, setup, migrations, rollbacks, multi-database, CI/CD, approval gates, deployment windows, and troubleshooting.

### Changed

#### Renamed from "DatabaseMigrationPlatform" (dbpilot) to "DbShift"
- Solution: `DbShift.sln`
- Source projects: `DbShift.Core`, `DbShift.Engine`, `DbShift.Infrastructure`, `DbShift.Reports`, `DbShift.CLI`
- Test project: `DbShift.Engine.Tests`
- Tool command: `dbshift` (was `migration`)
- Package: `DbShift` (was `dbpilot`)
- All namespaces, directories, and project references updated.

#### Configuration
- `migration.json` — added `database.provider` field.
- Environment JSON files — added to `Database/Config/environments/`.
- Connection string resolution: `--connection-string` > `DB_CONNECTION_STRING` env var > config file.

### Removed

- All PostgreSQL-specific infrastructure classes:
  - `PostgresMigrationTracker.cs`
  - `PostgresMigrationLockManager.cs`
  - `PostgresMigrationExecutor.cs`
  - `PostgresAuditLogger.cs`
  - `PostgresEnvironmentProvider.cs`
  - `TrackingSchema.cs`
- Empty directories: `Engine/Rollback/`, `Engine/Tracking/`, `Engine/Validation/`, `Infrastructure/Git/`, `CLI/Output/`, `docs/` (old).
- Old `README.md` and `LICENSE` files (replaced).

### Fixed

- `dbshift <command> --help` now correctly shows command-specific help (was showing global help for all commands).
- Help output no longer duplicates global options when showing command-specific help.
- JSON output is now emitted cleanly without decorative UI text.
- Migration template files generate correct `{{NAME}}` placeholders (used by `dbshift create`).

### Security

- No connection strings, secrets, or tokens are stored in the repository.
- All connection strings use environment variable expansion (`${VAR}`).
