using System.Diagnostics;
using DbShift.CLI.Helpers;
using DbShift.Core.ValueObjects;
using Spectre.Console;

namespace DbShift.CLI.Commands;

public sealed class MigrateCommand : CommandBase
{
    public override string Name => "migrate";
    public override string Description => "Apply pending migrations to the target environment.";
    public override string Category => "Execution";
    public override string? UsageExample => "dbshift migrate --environment production --approver jane@corp.com";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("executed-by", 'u', "User performing the deployment", false, "NAME"),
        new CommandOption("approver", null, "Approver identity (required for approval-gated environments)", false, "EMAIL"),
        new CommandOption("batch-size", 'b', "Override the migration batch size", false, "N"),
        new CommandOption("force", 'f', "Proceed even outside the deployment window", true, null)
    };

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var host = CreateHost(context);
        var live = RequireLive(context, host);
        if (live != 0)
        {
            return live;
        }

        if (!TryResolveEnvironment(context, host, out var environment))
        {
            return Fail(context, $"Environment '{host.EnvironmentName}' is not configured.");
        }

        if (!context.Json)
        {
            ConsoleHelper.PrintHeader($"Deploying migrations to '{host.EnvironmentName}'");
        }

        if (!context.GetFlag("force") && environment.DeploymentWindow is { Enabled: true } window)
        {
            if (!IsWithinDeploymentWindow(window, out var reason))
            {
                return Fail(context, $"Outside the configured deployment window. {reason} (override with --force)");
            }
        }

        var executedBy = context.GetOption("executed-by") ?? Environment.UserName;

        var planningContext = new MigrationContext { Environment = host.EnvironmentName, ExecutedBy = executedBy };
        var dryRun = await ConsoleHelper.RunWithSpinner("Computing pending migrations", () => host.Executor.DryRunAsync(planningContext));
        if (!dryRun.IsSuccess)
        {
            return Fail(context, dryRun.ErrorMessage ?? "Failed to compute the execution plan.");
        }

        var plan = dryRun.ExecutionPlan!;
        if (plan.TotalCount == 0)
        {
            if (context.Json)
            {
                WriteJson(new { success = true, applied = 0, message = "up to date" });
            }
            else
            {
                ConsoleHelper.PrintSuccess("The database is up to date - nothing to deploy.");
            }
            return 0;
        }

        if (!context.Json)
        {
            ConsoleHelper.PrintMigrationTable("Pending migrations",
                plan.Items.Select(i => (i.Version, i.Name, i.Type.ToString(), "Pending", i.HasRollback ? "rollback available" : "no rollback")));
        }

        var approver = await ResolveApproverAsync(context, environment);
        if (environment.Migration.RequireApproval && string.IsNullOrWhiteSpace(approver))
        {
            return Fail(context, "This environment requires approval. Provide --approver or confirm interactively.");
        }

        if (!context.Json && !context.AssumeYes)
        {
            if (!ConsoleHelper.Confirm($"Apply {plan.TotalCount} migration(s) to '{host.EnvironmentName}'?", false))
            {
                ConsoleHelper.PrintWarning("Deployment cancelled.");
                return 1;
            }
        }

        var deployContext = new MigrationContext
        {
            Environment = host.EnvironmentName,
            ExecutedBy = executedBy,
            BatchSize = context.HasOption("batch-size") ? context.GetIntOption("batch-size", 10) : null,
            StopOnFailure = true,
            Force = context.GetFlag("force")
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await ConsoleHelper.RunWithSpinner($"Applying {plan.TotalCount} migration(s)", () => host.Executor.DeployAsync(deployContext));
        stopwatch.Stop();

        if (context.Json)
        {
            WriteJson(new
            {
                success = result.IsSuccess,
                environment = host.EnvironmentName,
                applied = result.TotalApplied,
                appliedMigrations = result.AppliedMigrations,
                failedMigrations = result.FailedMigrations,
                elapsedMs = stopwatch.ElapsedMilliseconds,
                error = result.ErrorMessage
            });
            return result.IsSuccess ? 0 : 1;
        }

        ConsoleHelper.PrintSummary("Deployment result", new[]
        {
            ("environment", host.EnvironmentName, Theme.Text),
            ("applied", result.TotalApplied.ToString(), Theme.Success),
            ("failed", result.FailedMigrations.Count.ToString(), result.FailedMigrations.Count == 0 ? Theme.Text : Theme.Danger),
            ("elapsed", $"{stopwatch.Elapsed.TotalSeconds:0.00}s", Theme.Muted),
            ("approver", approver ?? "n/a", Theme.Muted)
        });

        if (result.AppliedMigrations.Count > 0)
        {
            ConsoleHelper.PrintList("Applied", result.AppliedMigrations);
        }
        if (result.FailedMigrations.Count > 0)
        {
            ConsoleHelper.PrintList("Failed", result.FailedMigrations);
        }

        if (result.IsSuccess)
        {
            ConsoleHelper.PrintSuccess($"Successfully applied {result.TotalApplied} migration(s).");
            return 0;
        }

        ConsoleHelper.PrintError(result.ErrorMessage ?? "Deployment completed with errors.");
        return 1;
    }

    private async Task<string?> ResolveApproverAsync(CommandContext context, EnvironmentConfiguration environment)
    {
        if (!environment.Migration.RequireApproval)
        {
            return null;
        }

        var approver = context.GetOption("approver");
        if (!string.IsNullOrWhiteSpace(approver))
        {
            return approver;
        }

        if (context.Json || context.AssumeYes)
        {
            return null;
        }

        approver = AnsiConsole.Ask<string>($"[bold {Theme.Primary}]Approver identity:[/] ");
        await Task.CompletedTask;
        return approver;
    }

    private static bool IsWithinDeploymentWindow(DeploymentWindow window, out string reason)
    {
        var now = DateTime.Now;
        reason = string.Empty;

        if (window.AllowedDays.Count > 0)
        {
            var today = now.DayOfWeek.ToString();
            if (!window.AllowedDays.Contains(today, StringComparer.OrdinalIgnoreCase))
            {
                reason = $"Today ({today}) is not an allowed day. Allowed: {string.Join(", ", window.AllowedDays)}.";
                return false;
            }
        }

        if (TimeSpan.TryParse(window.StartTime, out var start) && TimeSpan.TryParse(window.EndTime, out var end))
        {
            var time = now.TimeOfDay;
            if (time < start || time > end)
            {
                reason = $"Current time {now:HH:mm} is outside {window.StartTime}-{window.EndTime}.";
                return false;
            }
        }

        return true;
    }
}
