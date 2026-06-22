using System.Text.Json;
using DbShift.CLI.Helpers;
using Spectre.Console;

namespace DbShift.CLI.Commands;

public sealed class NewCommand : CommandBase
{
    private static readonly JsonSerializerOptions JsonPretty = new() { WriteIndented = true };

    public override string Name => "new";
    public override string Description => "Scaffold a complete DbShift project structure in the current directory.";
    public override string Category => "Setup";
    public override string? UsageExample => "dbshift new --name MyApp -p postgresql";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("name", 'n', "Project name (used in example migrations and CI)", false, "NAME"),
        new CommandOption("output", 'o', "Output directory (default: current directory)", false, "PATH"),
        new CommandOption("force", 'f', "Overwrite existing files", true, null),
    };

    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var isInteractive = !context.Json && !context.HasOption("name") && !context.HasOption("output") && !context.HasOption("provider");

        string projectName;
        string provider;
        string outputDir;
        bool force;

        if (isInteractive)
        {
            ConsoleHelper.PrintBanner();

            var welcome = new Panel(
                new Markup($"[white]This wizard will scaffold a complete [bold {Theme.Primary}]DbShift[/] project structure[/]{Environment.NewLine}" +
                           $"[grey]including config files, migration directories, templates, and CI pipelines.[/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse(Theme.Muted))
                .Header($" [{Theme.Primary}] Project setup [/]", Justify.Left)
                .Padding(1, 0);
            AnsiConsole.Write(welcome);
            AnsiConsole.WriteLine();

            projectName = AnsiConsole.Ask<string>($"  [bold {Theme.Primary}]Project name[/] [grey](MyApp)[/]:", "MyApp");
            provider = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"  [bold {Theme.Primary}]Database provider[/]:")
                    .PageSize(5)
                    .HighlightStyle(Style.Parse(Theme.Primary))
                    .AddChoices("postgresql", "sqlserver", "mysql", "sqlite"));

            var useCurrentDir = AnsiConsole.Confirm($"  [bold {Theme.Primary}]Use current directory?[/]", true);
            outputDir = useCurrentDir
                ? Directory.GetCurrentDirectory()
                : AnsiConsole.Ask<string>($"  [bold {Theme.Primary}]Output directory[/]:");

            if (Directory.Exists(Path.Combine(outputDir, "Database")))
            {
                force = AnsiConsole.Confirm(
                    $"  [bold {Theme.Warning}]Overwrite existing files?[/] [grey](a Database directory already exists)[/]", false);
            }
            else
            {
                force = false;
            }

            AnsiConsole.WriteLine();
        }
        else
        {
            projectName = context.GetOption("name", "MyApp");
            provider = context.GetOption("provider", "postgresql");
            outputDir = context.GetOption("output") ?? Directory.GetCurrentDirectory();
            force = context.GetFlag("force");
        }

        outputDir = Path.GetFullPath(outputDir);

        if (!Directory.Exists(outputDir))
        {
            return Task.FromResult(Fail(context, $"Output directory '{outputDir}' does not exist."));
        }

        var created = new List<string>();
        var skipped = new List<string>();

        void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(outputDir, relativePath);
            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(fullPath) && !force)
            {
                skipped.Add(relativePath);
                return;
            }

            File.WriteAllText(fullPath, content);
            created.Add(relativePath);
        }

        // ── Database/Config/migration.json ──────────────────────────────
        var config = new Dictionary<string, object>
        {
            ["migration"] = new Dictionary<string, object>
            {
                ["version"] = "1.0.0",
                ["database"] = new Dictionary<string, object>
                {
                    ["provider"] = provider,
                    ["connectionString"] = "${DB_CONNECTION_STRING}"
                },
                ["scripts"] = new Dictionary<string, object>
                {
                    ["path"] = "./Database/Migrations"
                },
                ["tracking"] = new Dictionary<string, object>
                {
                    ["schema"] = "public",
                    ["tableName"] = "__migration_history"
                },
                ["execution"] = new Dictionary<string, object>
                {
                    ["lockTimeoutSeconds"] = 300,
                    ["commandTimeoutSeconds"] = 3600,
                    ["batchSize"] = 10,
                    ["stopOnFailure"] = true
                }
            }
        };
        WriteFile("Database/Config/migration.json",
            JsonSerializer.Serialize(config, JsonPretty));

        // ── Database/Config/environments/*.json ─────────────────────────
        var environments = new[]
        {
            ("local", "DB_CONNECTION_STRING", true, 30, 10),
            ("development", "DB_CONNECTION_STRING", false, 60, 10),
            ("staging", "DB_CONNECTION_STRING", false, 120, 5),
            ("production", "PROD_DB_CONNECTION_STRING", true, 300, 5),
        };

        foreach (var (envName, csVar, requireApproval, lockTimeout, maxBatch) in environments)
        {
            var env = new Dictionary<string, object>
            {
                ["name"] = envName,
                ["database"] = new Dictionary<string, object>
                {
                    ["connectionString"] = $"${{{csVar}}}"
                },
                ["migration"] = new Dictionary<string, object>
                {
                    ["requireApproval"] = requireApproval,
                    ["lockTimeoutSeconds"] = lockTimeout,
                    ["maxBatchSize"] = maxBatch
                }
            };

            if (envName == "production")
            {
                env["deploymentWindow"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["startTime"] = "02:00",
                    ["endTime"] = "06:00",
                    ["allowedDays"] = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" }
                };
            }

            WriteFile($"Database/Config/environments/{envName}.json",
                JsonSerializer.Serialize(env, JsonPretty));
        }

        // ── Migration directories ───────────────────────────────────────
        WriteFile("Database/Migrations/Schema/.gitkeep", "");
        WriteFile("Database/Migrations/Data/.gitkeep", "");
        WriteFile("Database/Migrations/Patch/.gitkeep", "");
        WriteFile("Database/Migrations/Rollback/.gitkeep", "");

        // ── Example migration V001__Example_Users.sql ───────────────────
        WriteFile("Database/Migrations/Schema/V001__Example_Users.sql", $$"""
            -- Migration: Example_Users
            -- Author: {{projectName}}
            -- Created: {{DateTime.UtcNow:yyyy-MM-dd}}
            -- Description: Creates the users table (example migration)

            CREATE TABLE IF NOT EXISTS users (
                id              {{IdType(provider)}} PRIMARY KEY {{DefaultId(provider)}},
                email           VARCHAR(255) NOT NULL,
                display_name    VARCHAR(100) NOT NULL,
                is_active       {{BoolType(provider)}} NOT NULL DEFAULT {{BoolTrue(provider)}},
                created_at      {{TimestampType(provider)}} NOT NULL {{DefaultNow(provider)}},
                updated_at      {{TimestampType(provider)}} NOT NULL {{DefaultNow(provider)}}
            );

            CREATE {{UniqueIndex(provider)}} idx_users_email ON users (email);
            """.Replace("\r\n", "\n"));

        // ── Example rollback U001__Example_Users.sql ────────────────────
        WriteFile("Database/Migrations/Rollback/U001__Example_Users.sql", $$"""
            -- Rollback: Example_Users
            -- Author: {{projectName}}
            -- Created: {{DateTime.UtcNow:yyyy-MM-dd}}
            -- Description: Drops the users table

            DROP TABLE IF EXISTS users;
            """.Replace("\r\n", "\n"));

        // ── Templates (plain """ with no interpolation — {{NAME}} etc. are literal) ──
        WriteFile("Database/Templates/schema_migration.sql", """
            -- Migration: {{NAME}}
            -- Author: {{AUTHOR}}
            -- Created: {{DATE}}
            -- Description: {{DESCRIPTION}}

            -- TODO: add your DDL here
            """);

        WriteFile("Database/Templates/data_migration.sql", """
            -- Migration: {{NAME}}
            -- Author: {{AUTHOR}}
            -- Created: {{DATE}}
            -- Description: {{DESCRIPTION}}

            -- TODO: add your DML here
            """);

        WriteFile("Database/Templates/patch_migration.sql", """
            -- Migration: {{NAME}}
            -- Author: {{AUTHOR}}
            -- Created: {{DATE}}
            -- Description: {{DESCRIPTION}}

            -- TODO: add your DDL/DML here
            """);

        WriteFile("Database/Templates/rollback_migration.sql", """
            -- Rollback: {{NAME}}
            -- Author: {{AUTHOR}}
            -- Created: {{DATE}}
            -- Description: {{DESCRIPTION}}

            -- TODO: add your rollback SQL here
            """);

        WriteFile("Database/Templates/repeatable_migration.sql", """
            -- Migration: {{NAME}}
            -- Author: {{AUTHOR}}
            -- Created: {{DATE}}
            -- Description: {{DESCRIPTION}}

            -- TODO: add your repeatable SQL here (re-applied when checksum changes)
            """);

        // ── .gitignore ──────────────────────────────────────────────────
        WriteFile(".gitignore", $$"""
            # DbShift project scaffold
            # Ignore OS / editor files

            # Windows
            Thumbs.db
            Desktop.ini

            # macOS
            .DS_Store

            # Editor
            *.swp
            *.swo
            .vscode/
            .idea/
            *.suo
            *.user
            .vs/

            # Build
            bin/
            obj/
            *.nupkg
            """.Replace("\r\n", "\n"));

        // ── .github/workflows/database-migration.yml ───────────────────
        // Plain """ (no interpolation) so ${{ }} is passed through verbatim.
        WriteFile(".github/workflows/database-migration.yml", """
            name: database-migration

            on:
              push:
                branches: [main, develop]
                paths:
                  - 'Database/Migrations/**'
              pull_request:
                paths:
                  - 'Database/Migrations/**'
              workflow_dispatch:

            jobs:
              validate:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: '8.0.x'
                  - run: dotnet tool restore
                  - run: dbshift validate --json

              deploy-dev:
                needs: validate
                if: github.ref == 'refs/heads/develop'
                environment: development
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: '8.0.x'
                  - run: dotnet tool restore
                  - run: dbshift migrate --environment development --yes
                    env:
                      DB_CONNECTION_STRING: ${{ secrets.DB_CONNECTION_STRING }}

              deploy-prod:
                needs: validate
                if: github.ref == 'refs/heads/main'
                environment: production
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - uses: actions/setup-dotnet@v4
                    with:
                      dotnet-version: '8.0.x'
                  - run: dotnet tool restore
                  - run: dbshift migrate --environment production --approver deploy-bot --yes
                    env:
                      DB_CONNECTION_STRING: ${{ secrets.PROD_DB_CONNECTION_STRING }}
            """.Replace("\r\n", "\n"));

        if (context.Json)
        {
            var allFiles = new List<object>();
            foreach (var f in created) allFiles.Add(new { file = f, skipped = false });
            foreach (var f in skipped) allFiles.Add(new { file = f, skipped = true });
            WriteJson(new
            {
                success = true,
                output = outputDir,
                createdFiles = created.Count,
                skippedFiles = skipped.Count,
                files = allFiles
            });
        }
        else
        {
            ConsoleHelper.PrintSuccess("Scaffolded DbShift project");
            ConsoleHelper.PrintSummary("Summary", new[]
            {
                ("location", outputDir, Theme.Text),
                ("provider", provider, Theme.Success),
                ("project", projectName, Theme.Text),
                ("files", $"{created.Count} created", created.Count > 0 ? Theme.Success : Theme.Muted),
            });

            ConsoleHelper.PrintList("Created", created);

            if (skipped.Count > 0)
            {
                ConsoleHelper.PrintWarning($"Skipped {skipped.Count} existing file(s) (use --force to overwrite):");
                foreach (var s in skipped)
                    ConsoleHelper.PrintStep("  " + s);
            }

            ConsoleHelper.PrintDivider();
            var nextSteps = new Panel(
                new Markup(
                    $"  {Theme.Step} [grey]Set [bold]$DB_CONNECTION_STRING[/] or edit [bold]Database/Config/migration.json[/][/]{Environment.NewLine}" +
                    $"  {Theme.Step} [grey]Run [bold]dbshift create --name YourMigration --type schema[/][/]{Environment.NewLine}" +
                    $"  {Theme.Step} [grey]Write your SQL in the generated file[/]{Environment.NewLine}" +
                    $"  {Theme.Step} [grey]Run [bold]dbshift migrate -c \"$DB_CONNECTION_STRING\"[/][/]"))
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse(Theme.Muted))
                .Header($" [{Theme.Warning}] Next steps [/]", Justify.Left)
                .Padding(1, 0);
            AnsiConsole.Write(nextSteps);
        }

        return Task.FromResult(0);
    }

    // ── Provider-specific SQL helpers ───────────────────────────────────
    private static string IdType(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "UNIQUEIDENTIFIER",
        "mysql" => "CHAR(36)",
        "sqlite" => "TEXT",
        _ => "UUID"
    };

    private static string DefaultId(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "DEFAULT NEWID()",
        "mysql" => "",
        "sqlite" => "DEFAULT (lower(hex(randomblob(16))))",
        _ => "DEFAULT gen_random_uuid()"
    };

    private static string BoolType(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "BIT",
        "mysql" => "TINYINT(1)",
        "sqlite" => "INTEGER",
        _ => "BOOLEAN"
    };

    private static string BoolTrue(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "1",
        "mysql" => "1",
        "sqlite" => "1",
        _ => "TRUE"
    };

    private static string TimestampType(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "DATETIME2",
        "mysql" => "DATETIME",
        "sqlite" => "TEXT",
        _ => "TIMESTAMPTZ"
    };

    private static string DefaultNow(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "DEFAULT GETUTCDATE()",
        "mysql" => "DEFAULT UTC_TIMESTAMP",
        "sqlite" => "",
        _ => "DEFAULT NOW()"
    };

    private static string UniqueIndex(string provider) => provider.ToLowerInvariant() switch
    {
        "sqlserver" => "UNIQUE NONCLUSTERED INDEX",
        _ => "UNIQUE INDEX"
    };
}
