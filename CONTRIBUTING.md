# Contributing to DbShift

Thanks for your interest! Here's how to get started.

## Development setup

```bash
git clone https://github.com/your-org/dbshift.git
cd dbshift
dotnet restore
dotnet build
dotnet test
```

Requirements:
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (pinned by `global.json`)
- PowerShell 7+ (Windows) or bash (Linux/macOS) for the build scripts

## Project structure

```
src/
├── DbShift.Core/          domain model, no dependencies
├── DbShift.Engine/        script parser, migration executor, in-memory doubles
├── DbShift.Infrastructure/ providers, relational implementations, config loading
├── DbShift.Reports/       status/audit report generation
└── DbShift.CLI/           executable, argument parsing, Spectre.Console UI
tests/
└── DbShift.Engine.Tests/  15+ tests for ScriptParser + MigrationExecutor
```

## Code conventions

- **Language:** C# 12, nullable enabled, file-scoped namespaces.
- **Style:** Follow existing patterns. No BOM, LF line endings.
- **Warnings:** `TreatWarningsAsErrors` is enforced. Your code must compile with zero warnings.
- **Tests:** Every PR should include or update tests. `dotnet test` must pass.
- **No comments:** Production code should be self-documenting. Use meaningful names.

## Making changes

1. Fork and create a feature branch from `main`.
2. Make your changes. Keep them focused — one change per PR.
3. Run `dotnet build` and `dotnet test` — both must pass cleanly.
4. If adding a new command, add it to the help table and docs.
5. Open a PR against `main`.

## Adding a new database provider

1. Create a class implementing `IDatabaseProvider` in `Infrastructure/Database/Providers/`.
2. Add the NuGet package reference to `DbShift.Infrastructure.csproj`.
3. Register it in `DatabaseProviderFactory.CreateProvider()`.
4. Add the provider config value to the README table.
5. Update provider-specific SQL helpers in `NewCommand.cs`.

## Commit messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add SQLite provider support
fix: resolve duplicate version detection in ScriptParser
docs: update README with new command table
ci: add macOS to build matrix
```

## PR checklist

Before submitting:

- [ ] Code builds with zero warnings
- [ ] All existing tests pass
- [ ] New tests added for any new behaviour
- [ ] Documentation updated (README, docs/, or inline XML docs)
- [ ] CHANGELOG.md updated under "Unreleased"
- [ ] PR title follows Conventional Commits

## Questions?

Open a [Discussion](https://github.com/your-org/dbshift/discussions) or an Issue.
