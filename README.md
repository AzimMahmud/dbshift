<div align="center">

<img src=".github/assets/logo.svg" alt="DbShift" width="480">

**Database migrations that ship.**

A Flyway-style migration tool for **PostgreSQL**, **SQL Server**, **MySQL**, and **SQLite**.
Beautiful CLI, zero magic, production-tested patterns.

[![CI](https://github.com/AzimMahmud/dbshift/actions/workflows/ci.yml/badge.svg)](https://github.com/AzimMahmud/dbshift/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/AzimMahmud/dbshift?include_prereleases&color=success)](https://github.com/AzimMahmud/dbshift/releases)
[![.NET](https://img.shields.io/badge/.NET-6.0%20|%208.0%20|%2010.0-512bd4)]()
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)]()

</div>

---

## Quick start

```bash
# Install (no .NET required)
curl -fsSL https://github.com/AzimMahmud/dbshift/releases/latest/download/install.sh | bash

# Scaffold a project
dbshift new --name MyApp --provider postgresql

# Create a migration
dbshift create --name CreateUsersTable --type schema

# Validate scripts
dbshift validate

# Preview the plan
dbshift plan

# Deploy
dbshift migrate -c "Host=localhost;Database=myapp;Username=postgres"
```

---

## Why DbShift?

Most .NET teams end up with Entity Framework migrations that are hard to control in production,
or a pile of loose SQL scripts with no tracking. DbShift gives you the middle ground:

- **SQL-first.** You write plain `.sql` files. No embedded DSL, no XML, no surprises.
- **Multi-database.** One tool, four engines. Switch providers without changing your workflow.
- **Safe by design.** Distributed locks, approval gates, deployment windows, and audit trails.
- **CI-friendly.** Every command supports `--json` output and deterministic exit codes.
- **Works offline.** Validate, plan, and scaffold without a database connection.

---

## Installation

No .NET SDK or runtime required — the binary is self-contained.

### One-liner (recommended)

```bash
# Linux / macOS
curl -fsSL https://github.com/AzimMahmud/dbshift/releases/latest/download/install.sh | bash

# Windows (PowerShell)
powershell -c "iwr -Uri https://github.com/AzimMahmud/dbshift/releases/latest/download/install.ps1 | iex"
```

The install scripts detect your OS and architecture, download the correct pre-built binary,
and add it to your `PATH` automatically.

### Manual download

| Platform | Download |
|----------|----------|
| Windows x64 | [`dbshift-windows-x64.zip`](https://github.com/AzimMahmud/dbshift/releases/latest/download/dbshift-windows-x64.zip) (~40 MB) |
| Linux x64 | [`dbshift-linux-x64.tar.gz`](https://github.com/AzimMahmud/dbshift/releases/latest/download/dbshift-linux-x64.tar.gz) (~40 MB) |
| Linux arm64 | [`dbshift-linux-arm64.tar.gz`](https://github.com/AzimMahmud/dbshift/releases/latest/download/dbshift-linux-arm64.tar.gz) (~40 MB) |
| macOS x64 | [`dbshift-macos-x64.tar.gz`](https://github.com/AzimMahmud/dbshift/releases/latest/download/dbshift-macos-x64.tar.gz) (~40 MB) |
| macOS arm64 | [`dbshift-macos-arm64.tar.gz`](https://github.com/AzimMahmud/dbshift/releases/latest/download/dbshift-macos-arm64.tar.gz) (~40 MB) |

Extract and place `dbshift` (or `dbshift.exe` on Windows) anywhere on your `PATH`.

### .NET global tool

> **Note:** Publication to NuGet is pending. Until the package is listed, use the
> [one-liner](#one-liner-recommended) or [manual download](#manual-download) above —
> no .NET runtime is required. The command below will work as soon as the package is published.

Requires [.NET SDK 6.0, 8.0, or 10.0](https://dotnet.microsoft.com/download/dotnet) (any LTS from 6 onward). The package multi-targets `net6.0`, `net8.0`, and `net10.0`, so `dotnet tool install` resolves to whichever runtime you have installed (including STS runtimes 7 and 9 via NuGet fallback).

```bash
dotnet tool install --global DbShift
dbshift --version
```

### Build from source

```bash
git clone https://github.com/AzimMahmud/dbshift.git
cd dbshift
.\publish.ps1          # Windows → dist\dbshift.exe
./publish.sh           # Linux/macOS → dist/dbshift

# Optionally bundle a different .NET runtime (default: net8.0 LTS)
FRAMEWORK=net10.0 ./publish.sh linux-x64          # Linux/macOS
.\publish.ps1 -Runtime win-x64 -Framework net10.0  # Windows
```

### Verify

```bash
dbshift --version
# → DbShift v1.0.0
# → database migrations for .NET
```

---

## Commands

### Setup

| Command | Aliases | Description | DB required |
|---------|---------|-------------|-------------|
| `new` | `scaffold`, `init-project` | Scaffold a complete DbShift project (config, migrations, templates, CI, gitignore). Supports interactive mode when called without flags. | no |
| `init` | | Create the migration tracking schema (4 tables) on the target database. | yes |
| `create` | | Scaffold a new migration script from a template. | no |

### Validation

| Command | Description | DB required |
|---------|-------------|-------------|
| `validate` | Check scripts for naming, syntax, duplicate, and dependency errors. | no |

### Inspection

| Command | Aliases | Description | DB required |
|---------|---------|-------------|-------------|
| `status` | | Show migration status for an environment. | yes |
| `plan` | `dry-run` | Compute and display the pending execution plan (dry-run). | no |
| `history` | `audit` | Show the audit trail for an environment. | yes |
| `info` | `config` | Show configuration, provider, environments, and paths. | no |

### Execution

| Command | Aliases | Description | DB required |
|---------|---------|-------------|-------------|
| `migrate` | `deploy`, `apply` | Apply pending migrations to the target environment. | yes |
| `rollback` | | Roll back one or more previously applied migrations using `U` scripts. | yes |
| `repair` | | Re-queue one or all failed migrations so they can be retried. | yes |

### Global options

These apply to every command:

```
-e, --environment <NAME>      Target environment (default: local)
-p, --provider <NAME>         Database provider (postgresql | sqlserver | mysql | sqlite)
-c, --connection-string <CONN> Override connection string
    --config <PATH>           Path to the repository root
    --in-memory               Force offline mode (no database)
-y, --yes                     Skip interactive confirmation prompts
-v, --verbose                 Verbose logging
    --no-color                Disable colored output
    --json                    Emit machine-readable JSON
-h, --help                    Show help
    --version                 Show version
```

Run `dbshift <command> --help` for command-specific options.

---

## Quick start (detailed)

### 1. Scaffold a project

```bash
# Interactive mode — prompts for name, provider, and directory
dbshift new

# Non-interactive (all flags provided)
dbshift new --name MyApp --provider postgresql --output ./my-db-project

# List of aliases
dbshift scaffold --name MyApp --provider mysql
dbshift init-project --name MyApi --provider sqlserver
```

This creates:
```
my-db-project/
├── Database/
│   ├── Config/
│   │   ├── migration.json              # global config
│   │   └── environments/
│   │       ├── local.json
│   │       ├── development.json
│   │       ├── staging.json
│   │       └── production.json
│   ├── Migrations/
│   │   ├── Schema/V001__Example_Users.sql
│   │   ├── Data/.gitkeep
│   │   ├── Patch/.gitkeep
│   │   └── Rollback/U001__Example_Users.sql
│   └── Templates/
│       ├── schema_migration.sql
│       ├── data_migration.sql
│       ├── patch_migration.sql
│       ├── rollback_migration.sql
│       └── repeatable_migration.sql
├── .github/workflows/database-migration.yml
└── .gitignore
```

The example migration contains correct provider-specific SQL — proper column types,
UUID/TEXT/UNIQUEIDENTIFIER primary keys, and boolean/bit/tinyint/integer types.

### 2. Create a migration

```bash
dbshift create --name AddOrders --type schema
# → Creates Database/Migrations/Schema/V20260617120000__AddOrders.sql
```

Types: `schema` (DDL), `data` (DML), `patch` (hotfix), `rollback` (U script), `repeatable` (R script).

### 3. Write SQL

```sql
-- Migration: AddOrders
-- Author: jane
-- Created: 2026-06-17
-- Description: Creates the orders table

CREATE TABLE orders (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID NOT NULL REFERENCES users(id),
    total       DECIMAL(10,2) NOT NULL,
    status      VARCHAR(20) NOT NULL DEFAULT 'pending',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_orders_user_id ON orders (user_id);
```

### 4. Validate

```bash
dbshift validate
```

Checks naming conventions, duplicate versions, missing dependencies, and required metadata fields.

### 5. Preview

```bash
dbshift plan
```

Shows exactly what will run and in what order. No database required.

### 6. Deploy

```bash
# Set your connection string
export DB_CONNECTION_STRING="Host=localhost;Database=myapp;Username=postgres"

# Apply pending migrations
dbshift migrate -c "$DB_CONNECTION_STRING"

# Or use the configured environment
dbshift migrate --environment production
```

### 7. Rollback

```bash
dbshift rollback --count 1                     # undo the last migration
dbshift rollback --version 002                 # roll back a specific version
```

Requires a matching `U` script in `Database/Migrations/Rollback/`.

---

## Script conventions

```
Database/Migrations/
├── Schema/          versioned DDL (V001__CreateUsers.sql)
├── Data/            seed/migration data (V002__SeedRoles.sql)
├── Patch/           hotfix DDL/DML (V003__FixIndex.sql)
└── Rollback/        undo scripts (U001__Rollback_CreateUsers.sql)
```

| Pattern | Example | Behaviour |
|---------|---------|-----------|
| `V<version>__<Name>.sql` | `V001__CreateUsers.sql` | Applied once by version order |
| `V<timestamp>__<Name>.sql` | `V20260617120000__AddIndex.sql` | Applied once by timestamp order |
| `R__<Name>.sql` | `R__RefreshUserView.sql` | Re-applied when checksum changes |
| `U<version>__Rollback_<Name>.sql` | `U001__Rollback_CreateUsers.sql` | Rollback for `V001` |

Optional metadata headers:

```sql
-- Migration: CreateUsersTable
-- Author: jane.doe
-- Created: 2025-01-15
-- Description: Creates the users table
-- Depends: V000__InitialSetup.sql
```

---

## Configuration

### `Database/Config/migration.json`

```jsonc
{
  "migration": {
    "version": "1.0.0",
    "database": {
      "provider": "postgresql",          // postgresql | sqlserver | mysql | sqlite
      "connectionString": "${DB_CONNECTION_STRING}"
    },
    "scripts": {
      "path": "./Database/Migrations"
    },
    "tracking": {
      "schema": "public",
      "tableName": "__migration_history"
    },
    "execution": {
      "lockTimeoutSeconds": 300,
      "commandTimeoutSeconds": 3600,
      "batchSize": 10,
      "stopOnFailure": true
    }
  }
}
```

`${VAR}` tokens are expanded from environment variables. Secrets never need to be committed.

### Per-environment files

`Database/Config/environments/<name>.json`:

```jsonc
{
  "name": "production",
  "database": {
    "connectionString": "${PROD_DB_CONNECTION_STRING}"
  },
  "migration": {
    "requireApproval": true,
    "lockTimeoutSeconds": 300,
    "maxBatchSize": 5
  },
  "deploymentWindow": {
    "enabled": true,
    "startTime": "02:00",
    "endTime": "06:00",
    "allowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  }
}
```

### Connection string resolution

```
1. --connection-string CLI flag
2. DB_CONNECTION_STRING environment variable
3. environments/<name>.json → database.connectionString (with ${VAR} expansion)
4. migration.json → database.connectionString (with ${VAR} expansion)
```

---

## Supported databases

| Provider | Driver | Config value |
|----------|--------|-------------|
| PostgreSQL 12+ | Npgsql 8 | `postgresql` |
| SQL Server 2016+ | Microsoft.Data.SqlClient 5 | `sqlserver` |
| MySQL 8+ / MariaDB 10.5+ | MySqlConnector 2 | `mysql` |
| SQLite 3 | Microsoft.Data.Sqlite 8 | `sqlite` |

Switch providers by changing the `provider` field in `migration.json` or using `--provider`.

---

## Examples

### Offline workflow (no database)

```bash
# Create a timestamp-versioned schema migration
dbshift create --name AddOrders --type schema

# Create a rollback script
dbshift create --name AddOrders --type rollback

# Validate everything
dbshift validate

# Preview the execution plan
dbshift plan
```

### Live workflow (requires a database)

```bash
# Initialise tracking tables
dbshift init --connection-string "Host=localhost;Database=myapp;Username=postgres"

# Deploy with approval gating
dbshift migrate --environment production \
  --connection-string "$DB" \
  --approver jane@corp.com --yes

# Check status (JSON for CI)
dbshift status --environment production --json

# View recent audit entries
dbshift history --environment production

# Recover from a failure
dbshift repair
dbshift rollback --count 1
```

### CI/CD integration

```bash
# Validate in CI
dbshift validate --json

# Deploy to development
dbshift migrate --environment development --yes

# Deploy to production (with approver)
dbshift migrate --environment production --approver "$APPROVER" --yes
```

Every command supports `--json` for machine-readable output and non-zero exit codes on failure. A complete `.github/workflows/database-migration.yml` is generated by `dbshift new`.

---

## Architecture

```
┌─────────────────────────────────────────────┐
│              DbShift.CLI                     │
│   Program.cs · Commands · ConsoleHelper      │
└───────────────────┬─────────────────────────┘
                    │ uses
            ┌───────▼────────┐     ┌──────────────────┐
            │    Engine      │────▶│      Core         │
            │ ScriptParser   │     │ Entities · Enums  │
            │ MigrationExec  │     │ ValueObjects      │
            │ InMemory impls │     └──────────────────┘
            └───────┬────────┘              ▲
                    │ implements            │ implements
    ┌───────────────▼───────────────────────┴──────────┐
    │                  Infrastructure                   │
    │  Providers/                                        │
    │    PostgreSql · SqlServer · MySql · Sqlite         │
    │  Relational{Tracker, LockManager, Executor, Audit} │
    │  FileSystemConfigLoader                            │
    └────────────────────────────────────────────────────┘
```

| Project | Responsibility |
|---------|---------------|
| `Core` | Pure domain model — entities, enums, value objects, interfaces. Zero dependencies. |
| `Engine` | `ScriptParser`, `MigrationExecutor`, in-memory test doubles. |
| `Infrastructure` | Provider implementations for all 4 databases + file-system config loading. |
| `Reports` | Plain-text status and audit report generation. |
| `CLI` | The `dbshift` executable — argument parsing, Spectre.Console TUI, commands. |

### Multi-database design

Every database-specific behaviour (connection creation, parameter construction, tracking-schema DDL)
is encapsulated behind `IDatabaseProvider`. The `Relational*` classes use `System.Data.Common`
base types (`DbConnection`, `DbCommand`, `DbDataReader`) so the same code path works across all
four engines. Adding a new provider is a single class.

[Docs: Multi-database setup](docs/USAGE.md#6-multi-database-setup) ·
[Docs: Full usage guide](docs/USAGE.md)

---

## Tracking tables

`dbshift init` creates four tables, all in the configured schema:

| Table | Purpose |
|-------|---------|
| `__migration_history` | One row per applied migration per environment |
| `__migration_lock` | Distributed lock to prevent concurrent deploys |
| `__migration_audit` | Append-only audit trail of every action |
| `__migration_release` | Coordinated release bundles |

DDL is idempotent and engine-specific (UUID, boolean, and timestamp types differ
between PostgreSQL, SQL Server, MySQL, and SQLite).

---

## Build & test

```bash
dotnet build DbShift.sln
dotnet test  DbShift.sln
```

Conditions:
- Compiled with `TreatWarningsAsErrors` — zero warnings required.
- Target frameworks: `net6.0`, `net8.0`, `net10.0` (LTS span — runs on any .NET 6+ runtime). SDK resolved by `global.json` with `latestMajor` roll-forward.
- Test suite: 15 tests covering `ScriptParser` and `MigrationExecutor`, executed across all three target frameworks.

---

## Project layout

```
DbShift/
├── Database/                  # default scaffold output
│   ├── Config/                migration.json + environments/*.json
│   ├── Migrations/            Schema/ Data/ Patch/ Rollback/
│   └── Templates/             templates for `dbshift create`
├── src/
│   ├── DbShift.Core/
│   ├── DbShift.Engine/
│   ├── DbShift.Infrastructure/
│   ├── DbShift.Reports/
│   └── DbShift.CLI/
├── tests/
│   └── DbShift.Engine.Tests/
├── docs/
│   └── USAGE.md               full end-to-end usage guide
├── install.sh                 Linux/macOS one-liner installer
├── install.ps1                Windows one-liner installer
├── publish.sh                 Linux/macOS build script
├── publish.ps1                Windows build script
├── DbShift.sln
├── Directory.Build.props
├── global.json
├── README.md
├── CHANGELOG.md
└── LICENSE (MIT)
```

---

## License

MIT. See [LICENSE](LICENSE).
