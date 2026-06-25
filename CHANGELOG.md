# Changelog

All notable changes to the DbShift project will be documented in this file.

## [Unreleased]

### Added

- **.NET 6 → .NET 10 compatibility.** All projects now multi-target `net6.0`, `net8.0`, and `net10.0` (the LTS span). The NuGet tool package ships all three TFMs so `dotnet tool install` resolves to whatever runtime a user has installed — STS runtimes (7, 9) fall back to the nearest LTS via NuGet's runtime graph. Self-contained binaries are unaffected (no .NET required on the host).

### Security

- **SQLite CVE-2025-6965** ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q), high): pinned `SQLitePCLRaw.lib.e_sqlite3` to `3.50.3` (bundles SQLite 3.50.3, which is ≥ the fixed 3.50.2). The entire `2.1.x` line bundles a vulnerable SQLite; `Microsoft.Data.Sqlite` (even 10.0.x) still resolves a vulnerable `2.1.x` transitively, so the native lib is pinned explicitly in `DbShift.Infrastructure`.

### Added

- **PolySharp polyfill** for `net6.0`, so the latest C# features (`required`, `init`, `record`) compile on the oldest supported runtime. Source-only generator, no runtime dependency.

### Fixed

- `net6.0` build: the `[GeneratedRegex]` source generators (.NET 7+) are now guarded by `#if NET7_0_OR_GREATER` with a `RegexOptions.Compiled` fallback on `net6.0`.
- **`.NET global tool` package was broken.** The CLI project carried `SelfContained`, `PublishSingleFile`, and a default `win-x64` `RuntimeIdentifier`, which leaked a RID into the package (`tools/any/win-x64/...`, NU5118) and made the tool effectively Windows-only. Removed those properties (self-contained binaries set them via the publish command line) so `dotnet tool install --global DbShift` now ships RID-agnostic `tools/net6.0|net8.0|net10.0/any/...` and installs cross-platform.
- **Release asset naming.** `release.yml` produced `dbshift-win-x64.zip` / `dbshift-osx-x64.tar.gz` (raw .NET RIDs) while `install.ps1`, `install.sh`, and the README expected `dbshift-windows-x64.zip` / `dbshift-macos-x64.tar.gz`, so the one-liner installers would 404 on Windows and macOS. The workflow now emits the friendly names.
- `dbshift new --output <dir>` now creates the target directory instead of erroring when it does not exist, matching the documented Quick Start.

### Changed

- `Directory.Build.props` now sets `<TargetFrameworks>net6.0;net8.0;net10.0</TargetFrameworks>` instead of a single `net8.0`.
- `global.json` uses `rollForward: latestMajor` with a `6.0.100` baseline, so any installed .NET SDK (6 through 10) is accepted.
- `publish.sh` / `publish.ps1` accept a `FRAMEWORK` / `-Framework` parameter (default `net8.0` LTS) for the self-contained bundled runtime.
- CI installs the 6.0.x, 8.0.x, and 10.0.x SDKs and runs the test suite against all three target frameworks.
- Release workflow publishes self-contained binaries with an explicit `--framework` (`net8.0` by default) and installs all three SDKs to build the multi-targeted NuGet package.
- `.NET global tool` README section updated to reflect .NET 6/8/10 support.

- **Per-environment connection strings** — `environments/<name>.json` can now specify `database.connectionString`, resolved as step 3 in the connection string lookup chain (CLI flag → env var → environment file → global config).
- **Repeatable migration creation** — `dbshift create --type repeatable` generates `R__Name.sql` scripts (the parser already supported them).
- **`repeatable_migration.sql` template** — included in the scaffolded `Database/Templates/` directory.
- **`repair` without `--version`** — running `dbshift repair` with no version now re-queues all failed migrations at once.

### Changed

- Connection string resolution is now a 4-step chain: `--connection-string` → `DB_CONNECTION_STRING` env var → `environments/<name>.json` → `migration.json`.
- `repair --version` is now optional (was previously required).
- Removed redundant global-option redeclarations (`environment`, `json`, `yes`) from individual command definitions — they are already parsed globally.
- Removed redundant `GetFlag("yes")` checks in `migrate` and `rollback` commands (duplicated `AssumeYes`).

### Fixed

- Per-environment `connectionString` fields are no longer silently ignored during deserialization.
- User-facing strings that incorrectly used "dbshift" instead of "migration" (create, migrate, rollback, repair commands).
- Grammar: "No failed migrations needed repair" → "No failed migrations need repair".

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
