# DbShift usage guide

A practical walkthrough from zero to production.

---

## Table of contents

1. [Installation](#1-installation)
2. [Setting up a project](#2-setting-up-a-project)
3. [Creating migrations](#3-creating-migrations)
4. [Running migrations](#4-running-migrations)
5. [Rolling back](#5-rolling-back)
6. [Multi-database setup](#6-multi-database-setup)
7. [CI/CD integration](#7-cicd-integration)
8. [Approval gates and deployment windows](#8-approval-gates-and-deployment-windows)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Installation

**Prerequisite:** None for the binary install methods. For the .NET tool method, you need [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### One-liner (recommended)

```bash
# Linux / macOS
curl -fsSL https://github.com/your-org/dbshift/releases/latest/download/install.sh | bash

# Windows (PowerShell)
powershell -c "iwr -Uri https://github.com/your-org/dbshift/releases/latest/download/install.ps1 | iex"
```

These scripts detect your OS and architecture, download the correct binary, and add it to your PATH.

### Manual binary download

| Platform | Download |
|----------|----------|
| Windows x64 | [`dbshift-windows-x64.zip`](https://github.com/your-org/dbshift/releases/latest/download/dbshift-windows-x64.zip) |
| Linux x64 | [`dbshift-linux-x64.tar.gz`](https://github.com/your-org/dbshift/releases/latest/download/dbshift-linux-x64.tar.gz) |
| macOS x64 | [`dbshift-macos-x64.tar.gz`](https://github.com/your-org/dbshift/releases/latest/download/dbshift-macos-x64.tar.gz) |

Extract and place `dbshift` (or `dbshift.exe`) anywhere on your `PATH`.

### .NET global tool

```bash
dotnet tool install --global DbShift
dbshift --version
```

### Build from source

```bash
git clone <your-repo>
cd DbShift
.\publish.ps1                    # Windows → dist\dbshift.exe
./publish.sh                     # Linux/macOS → dist/dbshift
./dist/dbshift --help
```

---

## 2. Setting up a project

### 2.1 Quick start (zero setup)

The fastest way to start is a single command — no manual file creation needed:

```bash
# Interactive mode (prompts for name, provider, directory)
dbshift new

# Non-interactive (all flags provided)
dbshift new --name MyApp --provider postgresql

# Specify output directory
dbshift new --name MyApp --provider sqlserver --output ./my-database-project

# JSON mode for CI
dbshift new --name MyApp --json
```

When called without flags, `dbshift new` enters interactive mode:
1. Prompts for your **project name** (default: `MyApp`)
2. Lets you **select the database provider** from a list
3. Asks whether to use the **current directory** or a different one
4. Detects existing projects and asks about **overwriting files**

This creates:

```
Database/
  Config/
    migration.json                     # global config (edit connection string)
    environments/
      local.json                       # dev defaults, approval off
      development.json                 # CI/CD friendly
      staging.json                     # gated environment
      production.json                  # approval + deployment window
  Migrations/
    Schema/
      .gitkeep
      V001__Example_Users.sql          # runnable example (provider-specific SQL)
    Data/
      .gitkeep
    Patch/
      .gitkeep
    Rollback/
      .gitkeep
      U001__Example_Users.sql          # example rollback
  Templates/
    schema_migration.sql               # used by `dbshift create`
    data_migration.sql
    patch_migration.sql
    rollback_migration.sql
.github/workflows/
  database-migration.yml               # GitHub Actions CI pipeline
.gitignore
```

Options:

| Option | Short | Description |
| --- | --- | --- |
| `--name` | `-n` | Project name (default: `MyApp`) |
| `--output` | `-o` | Output directory (default: current directory) |
| `--force` | `-f` | Overwrite existing files |
| `--provider` | `-p` | Global option: `postgresql`, `sqlserver`, `mysql`, `sqlite` |
| `--json` | | Global option: machine-readable JSON output |

The example migration (`V001__Example_Users.sql`) is generated with correct SQL for your chosen provider — proper column types, defaults, and index syntax for PostgreSQL, SQL Server, MySQL, or SQLite.

After scaffolding, you only need to:
1. Set your connection string (env var or edit `migration.json`)
2. Run `dbshift create --name YourMigration --type schema` to add your own scripts
3. Write your SQL
4. Run `dbshift migrate -c "$DB_CONNECTION_STRING"`

### 2.2 Directory structure (manual)

If you prefer to set things up by hand, DbShift expects this layout (paths are configurable in `migration.json`):

```
your-repo/
  Database/
    Config/
      migration.json              # global settings
      environments/
        local.json                # per-environment overrides
        production.json
    Migrations/
      Schema/                     # V-prefix schema migrations
      Data/                       # V-prefix data migrations
      Patch/                      # V-prefix patch migrations
      Rollback/                   # U-prefix rollback scripts
    Templates/                    # used by `dbshift create`
```

### 2.3 Configuration file

Create `Database/Config/migration.json`:

```json
{
  "migration": {
    "version": "1.0.0",
    "database": {
      "provider": "postgresql",
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
    },
    "approval": {
      "requireApproval": ["production"],
      "approvers": ["admin@company.com"]
    }
  }
}
```

Connection string resolution order:

1. `--connection-string` CLI flag
2. `DB_CONNECTION_STRING` environment variable
3. `migration.json` → `connectionString`

### 2.4 Environment files

`Database/Config/environments/local.json`:

```json
{
  "name": "local",
  "database": {
    "host": "localhost",
    "port": 5432,
    "name": "myapp_local",
    "schema": "public"
  },
  "migration": {
    "requireApproval": false,
    "allowRollback": true,
    "lockTimeoutSeconds": 30,
    "maxBatchSize": 10
  }
}
```

`Database/Config/environments/production.json`:

```json
{
  "name": "production",
  "database": {
    "host": "${PROD_DB_HOST}",
    "port": 5432,
    "name": "myapp_prod",
    "schema": "public"
  },
  "migration": {
    "requireApproval": true,
    "allowRollback": true,
    "lockTimeoutSeconds": 300,
    "maxBatchSize": 5,
    "allowedRoles": ["admin", "deployer"]
  },
  "deploymentWindow": {
    "enabled": true,
    "startTime": "02:00",
    "endTime": "06:00",
    "allowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  }
}
```

### 2.5 Information commands

Check your setup:

```bash
# Show configuration, paths, and available environments
dbshift info

# Show available environments only
dbshift info --json
```

---

## 3. Creating migrations

### 3.1 Naming conventions

DbShift uses Flyway-style naming. All scripts go under `Database/Migrations/`:

#### Schema migrations (`V` prefix)

```
Database/Migrations/Schema/V001__CreateUsersTable.sql
Database/Migrations/Schema/V002__AddEmailColumn.sql
Database/Migrations/Schema/V20260617120000__CreateOrders.sql
```

- `V` = versioned
- Version can be a sequence (`001`, `002`) or a UTC timestamp (`20260617120000`)
- `__` separates the version from the name
- The name is PascalCase, underscores allowed

#### Data migrations (`V` prefix, `Data/` folder)

```
Database/Migrations/Data/V003__SeedRoles.sql
```

These go in the `Data/` subdirectory, which tells DbShift they're data/seed migrations.

#### Patch migrations (`V` prefix, `Patch/` folder)

```
Database/Migrations/Patch/V004__FixIndexCollation.sql
```

Patches are schema changes in a separate folder for organisational clarity.

#### Rollback scripts (`U` prefix)

```
Database/Migrations/Rollback/U001__Rollback_CreateUsersTable.sql
Database/Migrations/Rollback/U002__Rollback_AddEmailColumn.sql
```

- `U` = undo
- The version must match the forward migration's version (without the `V` prefix)
- Rollback scripts are paired with their forward counterparts

#### Repeatable scripts (`R` prefix)

```
Database/Migrations/Schema/R__RefreshUserView.sql
```

- `R` = repeatable (re-runs every time the checksum changes)
- No version number, so `R__` is the entire prefix

### 3.2 Using `dbshift create`

The fastest way to create a migration:

```bash
# Schema migration with timestamp version
dbshift create --name CreateUsersTable --type schema --author jane

# Schema migration with sequence version (001, 002...)
dbshift create --name AddEmailColumn --type schema --sequence

# Data migration
dbshift create --name SeedRoles --type data --author jane

# Patch migration
dbshift create --name FixIndexes --type patch

# Rollback script
dbshift create --name CreateUsersTable --type rollback

# Specify output directory
dbshift create --name AddColumns --type schema --dir ./Database/Migrations/Schema

# Include metadata
dbshift create --name CreateOrders --type schema --author jane --description "Orders and order_items tables"
```

This creates a file like `Database/Migrations/Schema/V20260617120000__CreateOrders.sql` with:

```sql
-- Migration: CreateOrders
-- Author: jane
-- Created: 2026-06-17
-- Description: Orders and order_items tables

-- TODO: Add your SQL migration here
```

### 3.3 Hand-writing migrations

You can also create migration files manually. The only requirement is the filename follows the convention:

```
V<version>__<Name>.sql
```

Optional metadata headers in the SQL file:

```sql
-- Depends: V001__CreateUsersTable.sql, V002__CreateRoles.sql
-- Author: jane
-- Description: Creates the orders table
```

The `validate` command checks these for correctness.

### 3.4 Validating scripts

Before you run anything, validate:

```bash
dbshift validate
dbshift validate --environment local
dbshift validate --json    # for CI
```

Validation checks:

- **Naming**: every file must match the `V/R/U` + version + `__` + name convention
- **Syntax**: scripts must not be empty
- **Duplicates**: no two versioned scripts can have the same version
- **Dependencies**: every `-- Depends:` reference must point to an existing file

---

## 4. Running migrations

### 4.1 Preview the plan

See what would be applied before touching the database:

```bash
dbshift plan
```

This works offline (no database needed). It shows:

- Which migrations are pending
- Their type (Schema/Data/Patch)
- Whether a rollback script is available

### 4.2 Initialise the tracking schema

Before your first deploy, create the tracking tables:

```bash
dbshift init --connection-string "Host=localhost;Database=myapp;Username=postgres;Password=secret"
```

This creates four tables in your database:

| Table | What it tracks |
| --- | --- |
| `__migration_history` | Every applied migration per environment |
| `__migration_lock` | Distributed lock preventing concurrent deploys |
| `__migration_audit` | Append-only audit trail |
| `__migration_release` | Coordinated release bundles |

If the tables already exist (from a previous run), `init` is idempotent — it won't change them.

### 4.3 Deploy

```bash
# Interactive (asks for confirmation)
dbshift migrate --connection-string "Host=localhost;Database=myapp;Username=postgres;Password=secret"

# Non-interactive (for automation)
dbshift migrate --connection-string "$DB_CONNECTION_STRING" --yes

# Specify environment (uses per-environment config)
dbshift migrate --environment production --yes

# Override batch size
dbshift migrate --batch-size 5

# Bypass deployment window check
dbshift migrate --environment production --force
```

What happens during a deploy:

1. **Lock acquisition**: acquires a row-level lock to prevent concurrent runs
2. **Plan computation**: determines which migrations are pending
3. **Batch execution**: applies migrations in batches (configurable)
4. **Status tracking**: each migration is recorded in `__migration_history`
5. **Audit logging**: every action logged in `__migration_audit`
6. **Lock release**: releases the distributed lock
7. **Result reporting**: shows what was applied, how long it took

If a migration fails:

- The error is recorded in `__migration_history`
- The transaction for that single script is rolled back
- If `stopOnFailure` is `true` (default), the deployment stops immediately
- You can repair the failed migration and retry

### 4.4 Check status

```bash
dbshift status
dbshift status --environment production
dbshift status --json    # machine-readable for CI
```

Shows a summary and detailed table of all migrations with their status:

- **Completed** (green): successfully applied
- **Pending** (yellow): waiting to be applied
- **Failed** (red): failed during execution
- **RolledBack** (violet): undone via rollback
- **InProgress** (blue): currently being applied

### 4.5 View history

```bash
dbshift history
dbshift history --environment production --limit 50
dbshift history --json
```

Shows the audit log: who did what, when, and details.

### 4.6 Repair a failed migration

If a migration fails (e.g. because of a syntax error), fix the SQL, then:

```bash
dbshift repair --version 005
```

This removes the failed record from `__migration_history`, allowing the migration to be retried. It does NOT undo any database changes the failed script may have made — you need to clean those up manually.

---

## 5. Rolling back

### 5.1 Setup

Each forward migration must have a paired rollback script:

```
Database/Migrations/Schema/V001__CreateUsersTable.sql
Database/Migrations/Rollback/U001__Rollback_CreateUsersTable.sql   ← pairs with V001
```

The version must match (without the `V`/`U` prefix).

### 5.2 Roll back the last migration

```bash
dbshift rollback --environment local
```

This rolls back the most recent completed migration using its `U` script.

### 5.3 Roll back by count

```bash
dbshift rollback --environment production --count 3
```

Rolls back the 3 most recent completed migrations, in reverse order.

### 5.4 Roll back by version

```bash
dbshift rollback --environment production --version 003
```

Rolls back the migration with version `003` (if it's completed).

### 5.5 Roll back non-interactively

```bash
dbshift rollback --environment production --yes
```

Skips the confirmation prompt.

---

## 6. Multi-database setup

DbShift supports four databases. The only thing that changes is the connection string and the `provider` setting.

### 6.1 PostgreSQL

```bash
dbshift migrate -c "Host=localhost;Port=5432;Database=myapp;Username=postgres;Password=secret"
```

Config:

```json
{ "database": { "provider": "postgresql", "connectionString": "..." } }
```

### 6.2 SQL Server

```bash
dbshift migrate -c "Server=localhost;Database=myapp;User Id=sa;Password=secret;TrustServerCertificate=True"
```

Config:

```json
{ "database": { "provider": "sqlserver", "connectionString": "..." } }
```

### 6.3 MySQL

```bash
dbshift migrate -c "Server=localhost;Database=myapp;User=root;Password=secret"
```

Config:

```json
{ "database": { "provider": "mysql", "connectionString": "..." } }
```

### 6.4 SQLite

```bash
dbshift migrate -c "Data Source=./myapp.db"
```

Config:

```json
{ "database": { "provider": "sqlite", "connectionString": "..." } }
```

### 6.5 Override provider from CLI

You can override the provider at runtime without changing config:

```bash
dbshift migrate -c "..." -p sqlserver
```

### 6.6 Provider-specific notes

| Concern | PostgreSQL | SQL Server | MySQL | SQLite |
| --- | --- | --- | --- | --- |
| Bool type | `BOOLEAN` | `BIT` | `TINYINT(1)` | `INTEGER` (0/1) |
| UUID type | `UUID` | `UNIQUEIDENTIFIER` | `CHAR(36)` | `TEXT` |
| Timestamp func | `NOW()` | `GETUTCDATE()` | `UTC_TIMESTAMP()` | C# DateTime (ISO string) |
| ID generation | `gen_random_uuid()` | `NEWID()` | C# Guid | C# Guid |
| JSON storage | `TEXT` | `NVARCHAR(MAX)` | `LONGTEXT` | `TEXT` |
| Auto-index | Yes | Requires `IF NOT EXISTS` | Yes | Uses `CREATE INDEX IF NOT EXISTS` |

---

## 7. CI/CD integration

### 7.1 JSON output

Every command supports `--json` for machine-readable output:

```bash
# Validate
dbshift validate --json
# → { "success": true, "scriptsChecked": 16, "errors": [], "warnings": [] }

# Deploy
dbshift migrate --json
# → { "success": true, "applied": 3, "appliedMigrations": ["001", "002", "003"], ... }

# Status
dbshift status --json
# → { "success": true, "applied": 3, "pending": 1, "failed": 0, ... }
```

Exit codes: `0` = success, `1` = error, `2` = parse error.

### 7.2 GitHub Actions

```yaml
jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4 with: { dotnet-version: '8.0.x' }
      - run: dotnet build DbShift.sln -c Release
      - run: dotnet test DbShift.sln -c Release --no-build
      - run: dotnet run --project src/DbShift.CLI -- validate

  deploy-dev:
    needs: validate
    if: github.ref == 'refs/heads/main'
    steps:
      - run: dotnet run --project src/DbShift.CLI -- migrate -e development -y
```

### 7.3 Azure DevOps

```yaml
- script: dotnet run --project src/DbShift.CLI -- validate
  displayName: 'Validate migrations'

- script: dotnet run --project src/DbShift.CLI -- migrate -e development -y
  displayName: 'Deploy to dev'
```

### 7.4 Best practices for CI

- Run `validate` first (fast, no database needed)
- Use `--json` to capture structured results
- Use `--yes` to skip interactive prompts
- Store connection strings in secrets / variable groups
- Use `--in-memory` for validation-only steps
- Promote through environments (dev → qa → prod) with approvals at each gate

---

## 8. Approval gates and deployment windows

### 8.1 Approval gating

For environments with `requireApproval: true`:

```bash
# Interactive: prompts for approver identity
dbshift migrate --environment production

# Non-interactive: provide approver
dbshift migrate --environment production --approver jane@corp.com

# With --yes, requires --approver
dbshift migrate --environment production --approver jane@corp.com --yes
```

If you attempt to deploy to an approval-gated environment without providing an approver, the command fails with a clear message.

### 8.2 Deployment windows

For environments with a configured `deploymentWindow`:

```bash
# Outside the window → blocked with a message
dbshift migrate --environment production
# → Error: Outside the configured deployment window. Current time 14:30 is outside 02:00-06:00.

# Override with --force
dbshift migrate --environment production --force
```

The deployment window checks:

- **Time range**: current time must be between `startTime` and `endTime`
- **Allowed days**: current day of week must be in `allowedDays`
- **Override**: `--force` bypasses both checks

### 8.3 Combining gates

Approval gating and deployment windows work together:

```bash
# Both checks must pass (or be overridden)
dbshift migrate --environment production --approver deploy-bot --force --yes
```

---

## 9. Troubleshooting

### 9.1 Migration fails

```
[FAIL] Migration '005' failed: syntax error at or near "CREAT"
```

**What happened**: The SQL in `V005__*.sql` has a syntax error.

**Fix**:
1. Fix the SQL in the file
2. Revert any partial changes the failed script made to the database
3. Run `dbshift repair --version 005` to remove the failed record
4. Run `dbshift migrate` again

### 9.2 Validation errors

```
Error: Duplicate migration version '002' (V002__CreateOrders.sql conflicts with V002__AddEmailColumn.sql).
```

**Fix**: Rename one of the files to use a different version.

### 9.3 Naming errors

```
Error: Migration filename 'something.sql' is invalid.
Expected format '<prefix><version>__<name>.sql'
```

**Fix**: Rename the file to follow the `V001__Name.sql` convention.

### 9.4 Connection refused

```
Failed to connect to database: Connection refused
```

**Check**:
- Is the database server running?
- Is the connection string correct?
- Is the host/port accessible?
- Does the provider match the database? (`-p sqlserver` for SQL Server, etc.)

### 9.5 Lock not acquired

```
Could not acquire migration lock for environment 'production'. Another deployment may be in progress.
```

**Check**:
- Is another CI job running `dbshift migrate` at the same time?
- Wait for it to finish, or run `dbshift repair` to check lock status

### 9.6 Config file not found

```
Migration configuration not found at 'C:\projects\app\Database\Config\migration.json'.
Run the CLI from the repository root or pass --config.
```

**Fix**: Run `dbshift` from the repository root, or use `--config <path>`.

### 9.7 JSON output has text mixed in

If you see spinner text or markdown mixed in with JSON output, you may be using an older version. Since v1.0.0, `--json` suppresses all decorative output. Run `dbshift validate --json` and verify the output is pure JSON.

### 9.8 Common SQL gotchas by database

| Issue | PostgreSQL | SQL Server | MySQL | SQLite |
| --- | --- | --- | --- | --- |
| Auto-increment | `SERIAL` / `BIGSERIAL` | `IDENTITY(1,1)` | `AUTO_INCREMENT` | `AUTOINCREMENT` |
| String concat | `\|\|` or `CONCAT()` | `+` | `CONCAT()` | `\|\|` |
| Limit/offset | `LIMIT n OFFSET m` | `OFFSET m ROWS FETCH NEXT n ROWS ONLY` | `LIMIT n OFFSET m` | `LIMIT n OFFSET m` |
| ILIKE | `ILIKE` | use `LOWER()` | use `LOWER()` | use `LOWER()` |
| Now/UTC | `NOW()` / `NOW() AT TIME ZONE 'UTC'` | `GETUTCDATE()` | `UTC_TIMESTAMP()` | `datetime('now')` |
| IF NOT EXISTS | `CREATE TABLE IF NOT EXISTS` | `IF NOT EXISTS (SELECT ...) CREATE TABLE ...` | `CREATE TABLE IF NOT EXISTS` | `CREATE TABLE IF NOT EXISTS` |

---

## Quick reference

| I want to... | Command |
| --- | --- |
| See what's available | `dbshift --help` |
| Scaffold a new project | `dbshift new --name MyApp --provider postgresql` |
| Check my setup | `dbshift info` |
| Create a migration | `dbshift create --name Foo --type schema` |
| Validate all scripts | `dbshift validate` |
| Preview changes | `dbshift plan` |
| Create tracking tables | `dbshift init -c "..."` |
| Deploy | `dbshift migrate -c "..."` |
| Check what's deployed | `dbshift status` |
| Roll back | `dbshift rollback` |
| Fix a failed migration | `dbshift repair --version 003` |
| See the audit log | `dbshift history` |
| Run without DB | all commands using `--in-memory` |
| Switch database engine | `-p sqlserver` or `-p mysql` or `-p sqlite` |
| Output as JSON | add `--json` to any command |
| Skip prompts | add `--yes` to any command |
| Use a different config | `--config /path/to/repo/root` |
