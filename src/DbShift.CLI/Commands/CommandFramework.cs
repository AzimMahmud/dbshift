namespace DbShift.CLI.Commands;

/// <summary>Describes a single command-line option for help generation and parsing.</summary>
public sealed record CommandOption(string LongName, char? ShortName, string Description, bool IsFlag, string? ValuePlaceholder = null);

/// <summary>Carries the parsed global and command-specific context to a command.</summary>
public sealed class CommandContext
{
    public required string CommandName { get; init; }
    public required IReadOnlyList<string> Arguments { get; init; }
    public required IReadOnlyDictionary<string, string?> Options { get; init; }

    public string EnvironmentName { get; init; } = "local";
    public string? Provider { get; init; }
    public string? ConnectionString { get; init; }
    public string? ConfigBasePath { get; init; }
    public bool UseInMemory { get; init; }
    public bool Json { get; init; }
    public bool Verbose { get; init; }
    public bool AssumeYes { get; init; }

    public string? GetOption(string name) => Options.TryGetValue(name, out var value) ? value : null;
    public string GetOption(string name, string defaultValue) => GetOption(name) ?? defaultValue;
    public int GetIntOption(string name, int defaultValue) => int.TryParse(GetOption(name), out var value) ? value : defaultValue;
    public bool GetFlag(string name) => Options.TryGetValue(name, out var value) && (value is null || value.Equals("true", StringComparison.OrdinalIgnoreCase));
    public bool HasOption(string name) => Options.ContainsKey(name);
}

/// <summary>Base class for every CLI command.</summary>
public abstract class Command
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string Category => "Migrations";
    public virtual string? UsageExample => null;
    public virtual IReadOnlyList<CommandOption> Options => Array.Empty<CommandOption>();
    public abstract Task<int> ExecuteAsync(CommandContext context);
}
