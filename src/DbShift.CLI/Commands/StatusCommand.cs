using DbShift.CLI.Helpers;
using DbShift.Core.Enums;

namespace DbShift.CLI.Commands;

public sealed class StatusCommand : CommandBase
{
    public override string Name => "status";
    public override string Description => "Show the migration status for an environment.";
    public override string Category => "Inspection";
    public override string? UsageExample => "dbshift status --environment local";
    public override IReadOnlyList<CommandOption> Options => Array.Empty<CommandOption>();

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
            ConsoleHelper.PrintHeader($"Migration status for '{host.EnvironmentName}'");
        }

        var status = await ConsoleHelper.RunWithSpinner("Querying migration history",
            () => host.Executor.GetStatusAsync(host.EnvironmentName));

        if (context.Json)
        {
            WriteJson(new
            {
                success = true,
                environment = status.Environment,
                total = status.Total,
                applied = status.Applied,
                pending = status.Pending,
                failed = status.Failed,
                migrations = status.Records.Select(r => new
                {
                    version = r.Version,
                    name = r.Name,
                    type = r.Type.ToString(),
                    status = r.Status.ToString(),
                    executedAtUtc = r.ExecutedAtUtc,
                    executionTimeMs = r.ExecutionTimeMs
                })
            });
            return 0;
        }

        ConsoleHelper.PrintSummary("Summary", new[]
        {
            ("environment", status.Environment, Theme.Text),
            ("total", status.Total.ToString(), Theme.Text),
            ("applied", status.Applied.ToString(), Theme.Success),
            ("pending", status.Pending.ToString(), Theme.Warning),
            ("failed", status.Failed.ToString(), status.Failed == 0 ? Theme.Text : Theme.Danger)
        });

        if (status.Records.Count == 0)
        {
            ConsoleHelper.PrintInfo("No migrations have been recorded for this environment yet.");
            return 0;
        }

        ConsoleHelper.PrintMigrationTable("Migrations",
            status.Records.Select(r => (r.Version, r.Name, r.Type.ToString(), r.Status.ToString(),
                r.Status == MigrationStatus.Failed
                    ? (r.ErrorMessage ?? string.Empty)
                    : (r.ExecutedAtUtc == default ? string.Empty : r.ExecutedAtUtc.ToString("u")))));

        return 0;
    }
}
