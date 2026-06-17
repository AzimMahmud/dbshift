using System.Text.Json;
using DbShift.CLI.Helpers;
using DbShift.Core.ValueObjects;

namespace DbShift.CLI.Commands;

/// <summary>Shared helpers for commands: host wiring, environment/live gating and JSON output.</summary>
public abstract class CommandBase : Command
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    protected CliHost CreateHost(CommandContext ctx) => CliHost.Create(ctx);

    protected int Fail(CommandContext ctx, string message)
    {
        if (ctx.Json)
        {
            WriteJson(new { success = false, error = message });
            return 1;
        }
        ConsoleHelper.RenderException(new InvalidOperationException(message));
        return 1;
    }

    protected bool TryResolveEnvironment(CommandContext ctx, CliHost host, out EnvironmentConfiguration environment)
    {
        try
        {
            environment = host.ConfigLoader.LoadEnvironment(host.EnvironmentName);
            return true;
        }
        catch (Exception ex)
        {
            environment = null!;
            if (!ctx.Json)
            {
                ConsoleHelper.PrintError(ex.Message);
            }
            return false;
        }
    }

    protected int RequireLive(CommandContext ctx, CliHost host)
    {
        if (host.IsLive)
        {
            return 0;
        }
        return Fail(ctx, "This command requires a live database connection. Set a connection string via --connection-string, the DB_CONNECTION_STRING environment variable, or migration.json.");
    }

    protected void WriteJson(object value)
    {
        var options = JsonOptions;
        Console.Out.WriteLine(JsonSerializer.Serialize(value, value.GetType(), options));
    }
}
