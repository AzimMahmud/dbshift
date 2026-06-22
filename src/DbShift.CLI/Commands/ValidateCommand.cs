using DbShift.CLI.Helpers;
using DbShift.Core.ValueObjects;

namespace DbShift.CLI.Commands;

public sealed class ValidateCommand : CommandBase
{
    public override string Name => "validate";
    public override string Description => "Validate migration scripts (naming, syntax, duplicates, dependencies).";
    public override string Category => "Validation";
    public override string? UsageExample => "dbshift validate --environment local";
    public override IReadOnlyList<CommandOption> Options => Array.Empty<CommandOption>();

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var host = CreateHost(context);
        if (!context.Json)
        {
            ConsoleHelper.PrintHeader($"Validating migrations for '{host.EnvironmentName}'");
        }

        ValidationResult result;
        try
        {
            result = await ConsoleHelper.RunWithSpinner("Scanning and validating scripts",
                () => host.Executor.ValidateAsync(host.EnvironmentName));
        }
        catch (Exception ex)
        {
            return Fail(context, ex.Message);
        }

        if (context.Json)
        {
            WriteJson(new
            {
                success = result.IsValid,
                environment = host.EnvironmentName,
                scriptsChecked = result.ScriptsChecked,
                errors = result.Errors,
                warnings = result.Warnings
            });
            return result.IsValid ? 0 : 1;
        }

        ConsoleHelper.PrintSummary("Result", new[]
        {
            ("scripts checked", result.ScriptsChecked.ToString(), Theme.Text),
            ("errors", result.Errors.Count.ToString(), result.Errors.Count == 0 ? Theme.Success : Theme.Danger),
            ("warnings", result.Warnings.Count.ToString(), result.Warnings.Count == 0 ? Theme.Text : Theme.Warning),
            ("status", result.IsValid ? "VALID" : "INVALID", result.IsValid ? Theme.Success : Theme.Danger)
        });

        if (result.Errors.Count > 0)
        {
            ConsoleHelper.PrintList("Errors", result.Errors);
        }
        if (result.Warnings.Count > 0)
        {
            ConsoleHelper.PrintList("Warnings", result.Warnings);
        }

        if (result.IsValid)
        {
            ConsoleHelper.PrintSuccess("All migration scripts are valid.");
        }
        else
        {
            ConsoleHelper.PrintError($"{result.Errors.Count} validation error(s) found.");
        }
        return result.IsValid ? 0 : 1;
    }
}
