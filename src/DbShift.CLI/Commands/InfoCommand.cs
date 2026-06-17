using DbShift.CLI.Helpers;

namespace DbShift.CLI.Commands;

public sealed class InfoCommand : CommandBase
{
    public override string Name => "info";
    public override string Description => "Show configuration, environments and repository details.";
    public override string Category => "Inspection";
    public override string? UsageExample => "dbshift info";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("json", null, "Emit machine-readable JSON", true, null)
    };

    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var host = CreateHost(context);

        if (context.Json)
        {
            WriteJson(new
            {
                success = true,
                basePath = host.BasePath,
                scriptsPath = host.ScriptsPath,
                isLive = host.IsLive,
                config = host.Config is null ? null : new
                {
                    host.Config.Provider,
                    host.Config.Version,
                    host.Config.TrackingTable,
                    host.Config.BatchSize,
                    host.Config.CommandTimeoutSeconds,
                    requireApproval = host.Config.RequireApprovalEnvironments
                },
                environments = host.ConfigLoader.GetAvailableEnvironments()
            });
            return Task.FromResult(0);
        }

        ConsoleHelper.PrintHeader("DbShift configuration");

        if (host.Config is null)
        {
            ConsoleHelper.PrintWarning("No migration.json found - running with defaults.");
        }
        else
        {
            ConsoleHelper.PrintSummary("Configuration", new[]
            {
                ("provider", host.Config.Provider, Theme.Text),
                ("config version", host.Config.Version, Theme.Text),
                ("tracking table", $"{host.Config.TrackingSchema}.{host.Config.TrackingTable}", Theme.Text),
                ("batch size", host.Config.BatchSize.ToString(), Theme.Text),
                ("command timeout", $"{host.Config.CommandTimeoutSeconds}s", Theme.Text),
                ("approval envs", string.Join(", ", host.Config.RequireApprovalEnvironments), Theme.Warning)
            });
        }

        ConsoleHelper.PrintSummary("Runtime", new[]
        {
            ("base path", host.BasePath, Theme.Muted),
            ("scripts path", host.ScriptsPath, Theme.Muted),
            ("mode", host.IsLive ? $"live ({host.ProviderName})" : "offline (in-memory)", host.IsLive ? Theme.Success : Theme.Warning),
            ("environment", host.EnvironmentName, Theme.Text)
        });

        var environments = host.ConfigLoader.GetAvailableEnvironments();
        ConsoleHelper.PrintList("Environments", environments.Count == 0 ? new[] { "(none found)" } : environments);

        return Task.FromResult(0);
    }
}
