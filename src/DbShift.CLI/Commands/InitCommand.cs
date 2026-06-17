using DbShift.CLI.Helpers;

namespace DbShift.CLI.Commands;

public sealed class InitCommand : CommandBase
{
    public override string Name => "init";
    public override string Description => "Create the migration tracking schema on the target database.";
    public override string Category => "Setup";
    public override string? UsageExample => "dbshift init --environment local";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("environment", 'e', "Target environment", false, "NAME"),
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
            ConsoleHelper.PrintHeader($"Initialising tracking schema on '{host.EnvironmentName}'");
        }

        var result = await ConsoleHelper.RunWithSpinner("Creating tracking tables", () => host.Executor.InitAsync());

        if (context.Json)
        {
            WriteJson(new { success = result.IsSuccess, created = result.CreatedObjects, error = result.ErrorMessage });
            return result.IsSuccess ? 0 : 1;
        }

        if (!result.IsSuccess)
        {
            return Fail(context, result.ErrorMessage ?? "Failed to create tracking schema.");
        }

        ConsoleHelper.PrintList("Created tables", result.CreatedObjects);
        ConsoleHelper.PrintSuccess("Tracking schema is ready.");
        return 0;
    }
}
