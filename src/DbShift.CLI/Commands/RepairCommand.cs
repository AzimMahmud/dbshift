using DbShift.CLI.Helpers;

namespace DbShift.CLI.Commands;

public sealed class RepairCommand : CommandBase
{
    public override string Name => "repair";
    public override string Description => "Repair the migration history (re-queue a failed migration).";
    public override string Category => "Execution";
    public override string? UsageExample => "dbshift repair --environment local";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("version", 'V', "Specific version to repair (omit to repair all failed migrations)", false, "VERSION")
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
        var label = string.IsNullOrWhiteSpace(version) ? "all failed migrations" : $"migration {version}";

        if (!context.Json)
        {
            ConsoleHelper.PrintHeader($"Repairing '{host.EnvironmentName}'");
        }

        var result = await ConsoleHelper.RunWithSpinner($"Repairing {label}", () => host.Executor.RepairAsync(host.EnvironmentName, version));

        if (context.Json)
        {
            WriteJson(new { success = result.IsSuccess, repaired = result.RepairedMigrations, error = result.ErrorMessage });
            return result.IsSuccess ? 0 : 1;
        }

        if (result.RepairedMigrations.Count > 0)
        {
            foreach (var repaired in result.RepairedMigrations)
            {
                ConsoleHelper.PrintSuccess($"Repaired migration '{repaired}'.");
            }
        }
        else
        {
            ConsoleHelper.PrintInfo("No failed migrations need repair.");
        }
        return result.IsSuccess ? 0 : 1;
    }
}
