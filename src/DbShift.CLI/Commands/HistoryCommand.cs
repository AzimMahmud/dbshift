using DbShift.CLI.Helpers;

namespace DbShift.CLI.Commands;

public sealed class HistoryCommand : CommandBase
{
    public override string Name => "history";
    public override string Description => "Show the audit history for an environment.";
    public override string Category => "Inspection";
    public override string? UsageExample => "dbshift history --environment local --limit 25";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("environment", 'e', "Target environment", false, "NAME"),
        new CommandOption("limit", 'n', "Maximum entries to display", false, "N"),
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
            ConsoleHelper.PrintHeader($"Audit history for '{host.EnvironmentName}'");
        }

        var limit = context.GetIntOption("limit", 25);
        var entries = await ConsoleHelper.RunWithSpinner("Querying audit log", () => host.Executor.GetHistoryAsync(host.EnvironmentName, limit));

        if (context.Json)
        {
            WriteJson(new
            {
                success = true,
                environment = host.EnvironmentName,
                count = entries.Count,
                entries = entries.Select(e => new
                {
                    action = e.Action.ToString(),
                    performedBy = e.PerformedBy,
                    performedAtUtc = e.PerformedAtUtc,
                    details = e.Details
                })
            });
            return 0;
        }

        if (entries.Count == 0)
        {
            ConsoleHelper.PrintInfo("No audit entries recorded for this environment.");
            return 0;
        }

        ConsoleHelper.PrintMigrationTable("Audit entries",
            entries.Select(e => (e.PerformedAtUtc.ToString("yyyy-MM-dd HH:mm"), e.Action.ToString(), e.PerformedBy, "Logged", e.Details ?? string.Empty)));

        return 0;
    }
}
