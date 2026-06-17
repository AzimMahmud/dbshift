using DbShift.CLI.Helpers;

namespace DbShift.CLI.Commands;

public sealed class RepairCommand : CommandBase
{
    public override string Name => "repair";
    public override string Description => "Repair the migration history (re-queue a failed migration).";
    public override string Category => "Execution";
    public override string? UsageExample => "dbshift repair --environment local --version 001";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("environment", 'e', "Target environment", false, "NAME"),
        new CommandOption("version", 'V', "Version to repair", false, "VERSION"),
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

        var version = context.GetOption("version");
        if (string.IsNullOrWhiteSpace(version))
        {
            return Fail(context, "The --version option is required.");
        }

        if (!context.Json)
        {
            ConsoleHelper.PrintHeader($"Repairing '{host.EnvironmentName}'");
        }

        var result = await ConsoleHelper.RunWithSpinner($"Repairing dbshift {version}", () => host.Executor.RepairAsync(host.EnvironmentName, version));

        if (context.Json)
        {
            WriteJson(new { success = result.IsSuccess, repaired = result.RepairedMigrations, error = result.ErrorMessage });
            return result.IsSuccess ? 0 : 1;
        }

        if (result.RepairedMigrations.Count > 0)
        {
            foreach (var repaired in result.RepairedMigrations)
            {
                ConsoleHelper.PrintSuccess($"Repaired dbshift '{repaired}'.");
            }
        }
        else
        {
            ConsoleHelper.PrintInfo("No failed migrations needed repair.");
        }
        return result.IsSuccess ? 0 : 1;
    }
}
