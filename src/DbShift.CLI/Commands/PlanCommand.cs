using DbShift.CLI.Helpers;

namespace DbShift.CLI.Commands;

public sealed class PlanCommand : CommandBase
{
    public override string Name => "plan";
    public override string Description => "Compute and display the pending migration execution plan (dry-run).";
    public override string Category => "Inspection";
    public override string? UsageExample => "dbshift plan --environment local";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("environment", 'e', "Target environment", false, "NAME"),
        new CommandOption("executed-by", 'u', "User performing the run", false, "NAME"),
        new CommandOption("json", null, "Emit machine-readable JSON", true, null)
    };

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var host = CreateHost(context);
        if (!context.Json)
        {
            ConsoleHelper.PrintHeader($"Execution plan for '{host.EnvironmentName}'");
        }

        var contextObj = new Core.ValueObjects.MigrationContext
        {
            Environment = host.EnvironmentName,
            ExecutedBy = context.GetOption("executed-by") ?? Environment.UserName
        };

        var dryRun = await ConsoleHelper.RunWithSpinner("Computing pending migrations",
            () => host.Executor.DryRunAsync(contextObj));

        if (!dryRun.IsSuccess)
        {
            return Fail(context, dryRun.ErrorMessage ?? "Failed to compute execution plan.");
        }

        var plan = dryRun.ExecutionPlan!;

        if (context.Json)
        {
            WriteJson(new
            {
                success = true,
                environment = plan.Environment,
                count = plan.TotalCount,
                migrations = plan.Items.Select(i => new
                {
                    version = i.Version,
                    name = i.Name,
                    type = i.Type.ToString(),
                    category = i.Category,
                    hash = i.Hash,
                    hasRollback = i.HasRollback
                })
            });
            return 0;
        }

        if (plan.TotalCount == 0)
        {
            ConsoleHelper.PrintSuccess("The database is up to date - no pending migrations.");
            return 0;
        }

        ConsoleHelper.PrintInfo($"{plan.TotalCount} pending migration(s) will be applied in order:");
        ConsoleHelper.PrintMigrationTable("Pending migrations",
            plan.Items.Select(i => (i.Version, i.Name, i.Type.ToString(), "Pending", i.HasRollback ? "rollback available" : "no rollback")));

        return 0;
    }
}
