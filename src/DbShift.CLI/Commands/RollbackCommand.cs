using DbShift.CLI.Helpers;
using DbShift.Core.ValueObjects;

namespace DbShift.CLI.Commands;

public sealed class RollbackCommand : CommandBase
{
    public override string Name => "rollback";
    public override string Description => "Roll back one or more previously applied migrations.";
    public override string Category => "Execution";
    public override string? UsageExample => "dbshift rollback --environment local --count 1";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("environment", 'e', "Target environment", false, "NAME"),
        new CommandOption("version", 'V', "Specific version to roll back (default: last)", false, "VERSION"),
        new CommandOption("count", 'n', "Number of recent migrations to roll back", false, "N"),
        new CommandOption("executed-by", 'u', "User performing the rollback", false, "NAME"),
        new CommandOption("yes", 'y', "Skip interactive confirmation", true, null),
        new CommandOption("json", null, "Emit machine-readable JSON", true, null)
    };

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var host = CreateHost(context);
        var live = RequireLive(context, host);
        if (live != 0)
        {
            return live;
        }

        if (!context.Json)
        {
            ConsoleHelper.PrintHeader($"Rolling back migrations on '{host.EnvironmentName}'");
        }

        var status = await host.Executor.GetStatusAsync(host.EnvironmentName);
        if (status.Applied == 0)
        {
            if (context.Json)
            {
                WriteJson(new { success = true, rolledBack = 0 });
            }
            else
            {
                ConsoleHelper.PrintInfo("No applied migrations to roll back.");
            }
            return 0;
        }

        var request = new RollbackRequest
        {
            Version = context.GetOption("version", "last"),
            Count = context.GetIntOption("count", 1),
            Environment = host.EnvironmentName,
            ExecutedBy = context.GetOption("executed-by") ?? Environment.UserName
        };

        if (!context.Json)
        {
            var label = request.Version.Equals("last", StringComparison.OrdinalIgnoreCase)
                ? $"the last {request.Count} migration(s)"
                : $"dbshift '{request.Version}'";
            ConsoleHelper.PrintWarning($"This will roll back {label} on '{host.EnvironmentName}'.");
            if (!context.AssumeYes && !context.GetFlag("yes") && !ConsoleHelper.Confirm("Proceed with rollback?", false))
            {
                ConsoleHelper.PrintWarning("Rollback cancelled.");
                return 1;
            }
        }

        var result = await ConsoleHelper.RunWithSpinner("Rolling back migrations", () => host.Executor.RollbackAsync(request));

        if (context.Json)
        {
            WriteJson(new { success = result.IsSuccess, rolledBack = result.RolledBackMigrations, error = result.ErrorMessage });
            return result.IsSuccess ? 0 : 1;
        }

        if (result.RolledBackMigrations.Count > 0)
        {
            ConsoleHelper.PrintList("Rolled back", result.RolledBackMigrations);
        }
        if (result.IsSuccess)
        {
            ConsoleHelper.PrintSuccess($"Rolled back {result.RolledBackMigrations.Count} migration(s).");
            return 0;
        }
        ConsoleHelper.PrintError(result.ErrorMessage ?? "Rollback failed.");
        return 1;
    }
}
