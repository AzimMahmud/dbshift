using DbShift.CLI.Commands;
using DbShift.CLI.Helpers;
using Spectre.Console;

var globalOptions = new[]
{
    new CommandOption("environment", 'e', "Target environment", false, "NAME"),
    new CommandOption("provider", 'p', "Database provider: postgresql | sqlserver | mysql | sqlite", false, "NAME"),
    new CommandOption("connection-string", 'c', "Database connection string override", false, "CONN"),
    new CommandOption("config", null, "Path to the repository root (config base)", false, "PATH"),
    new CommandOption("in-memory", null, "Force offline in-memory mode (no database)", true, null),
    new CommandOption("yes", 'y', "Skip interactive prompts", true, null),
    new CommandOption("verbose", 'v', "Verbose logging", true, null),
    new CommandOption("no-color", null, "Disable colored output", true, null),
    new CommandOption("json", null, "Emit machine-readable JSON", true, null),
    new CommandOption("help", 'h', "Show help", true, null),
    new CommandOption("version", null, "Show version information", true, null)
};

var commands = new List<Command>
{
    new NewCommand(),
    new InitCommand(),
    new ValidateCommand(),
    new StatusCommand(),
    new PlanCommand(),
    new MigrateCommand(),
    new RollbackCommand(),
    new RepairCommand(),
    new HistoryCommand(),
    new CreateCommand(),
    new InfoCommand()
};

var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["deploy"] = "migrate", ["apply"] = "migrate",
    ["dry-run"] = "plan", ["plan"] = "plan",
    ["audit"] = "history",
    ["scaffold"] = "new", ["init-project"] = "new",
    ["config"] = "info"
};

return await RunAsync(args, commands, aliases, globalOptions);

static async Task<int> RunAsync(string[] args, List<Command> commands, Dictionary<string, string> aliases, CommandOption[] globalOptions)
{
    try
    {
        var (commandName, positionals, options, parseError) = ParseArgs(args, commands, aliases, globalOptions);

        var noColor = options.TryGetValue("no-color", out _);
        if (noColor)
        {
            try
            {
                AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { ColorSystem = ColorSystemSupport.NoColors });
            }
            catch (Exception) { /* ignore - best effort */ }
        }

        var json = options.ContainsKey("json");
        ConsoleHelper.UiSuppressed = json;
        if (options.TryGetValue("version", out _) && string.IsNullOrEmpty(commandName))
        {
            ConsoleHelper.PrintVersionInfo();
            return 0;
        }

        if (!string.IsNullOrEmpty(parseError))
        {
            ConsoleHelper.RenderException(new ArgumentException(parseError));
            PrintUsageHint(commands);
            return 2;
        }

        if (string.IsNullOrEmpty(commandName))
        {
            PrintHelp(commands, globalOptions);
            return 0;
        }

        var command = commands.FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
        if (command is null)
        {
            ConsoleHelper.RenderException(new ArgumentException($"Unknown command '{commandName}'."));
            PrintUsageHint(commands);
            return 2;
        }

        if (options.ContainsKey("help"))
        {
            PrintCommandHelp(command, globalOptions);
            return 0;
        }

        var context = new CommandContext
        {
            CommandName = command.Name,
            Arguments = positionals,
            Options = options,
            EnvironmentName = options.TryGetValue("environment", out var env) && !string.IsNullOrWhiteSpace(env) ? env! : "local",
            Provider = options.TryGetValue("provider", out var prov) ? prov : null,
            ConnectionString = options.TryGetValue("connection-string", out var cs) ? cs : null,
            ConfigBasePath = options.TryGetValue("config", out var cfg) ? cfg : null,
            UseInMemory = options.ContainsKey("in-memory"),
            Json = json,
            Verbose = options.ContainsKey("verbose"),
            AssumeYes = options.ContainsKey("yes")
        };

        return await command.ExecuteAsync(context);
    }
    catch (Exception ex)
    {
        ConsoleHelper.RenderException(ex);
        return 1;
    }
}

static (string? CommandName, List<string> Positionals, Dictionary<string, string?> Options, string? Error) ParseArgs(
    string[] args, List<Command> commands, Dictionary<string, string> aliases, CommandOption[] globalOptions)
{
    var positionals = new List<string>();
    var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    var allDefs = new List<CommandOption>(globalOptions);

    string? commandName = null;
    var i = 0;
    while (i < args.Length)
    {
        var token = args[i];

        if (token == "--")
        {
            positionals.AddRange(args[(i + 1)..]);
            break;
        }

        if (token.StartsWith("--"))
        {
            var (name, inline) = SplitLong(token[2..]);
            var defs = ResolveDefs(commandName, commands, globalOptions);
            var def = defs.FirstOrDefault(d => d.LongName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (def is null && name.Equals("help", StringComparison.OrdinalIgnoreCase)) def = new CommandOption("help", 'h', "", true);
            if (def is null)
            {
                return (null, positionals, options, $"Unknown option '--{name}'.");
            }
            if (def.IsFlag)
            {
                options[def.LongName] = "true";
            }
            else if (inline is not null)
            {
                options[def.LongName] = inline;
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
            {
                options[def.LongName] = args[++i];
            }
            else
            {
                return (null, positionals, options, $"Option '--{name}' expects a value.");
            }
        }
        else if (token.StartsWith('-') && token.Length > 1)
        {
            var chars = token[1..].ToCharArray();
            var defs = ResolveDefs(commandName, commands, globalOptions);
            var consumedNext = false;
            for (var c = 0; c < chars.Length; c++)
            {
                var ch = chars[c];
                var def = defs.FirstOrDefault(d => d.ShortName == ch);
                if (def is null)
                {
                    return (null, positionals, options, $"Unknown option '-{ch}'.");
                }
                if (def.IsFlag)
                {
                    options[def.LongName] = "true";
                    continue;
                }
                var rest = new string(chars[(c + 1)..]);
                if (rest.Length > 0)
                {
                    options[def.LongName] = rest;
                    break;
                }
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    options[def.LongName] = args[i + 1];
                    consumedNext = true;
                    break;
                }
                return (null, positionals, options, $"Option '-{ch}' expects a value.");
            }
            if (consumedNext)
            {
                i++;
            }
        }
        else
        {
            if (commandName is null)
            {
                commandName = aliases.TryGetValue(token, out var aliased) ? aliased : token;
            }
            else
            {
                positionals.Add(token);
            }
        }
        i++;
    }

    return (commandName, positionals, options, null);
}

static List<CommandOption> ResolveDefs(string? commandName, List<Command> commands, CommandOption[] globalOptions)
{
    var defs = new List<CommandOption>(globalOptions);
    if (commandName is not null)
    {
        var command = commands.FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
        if (command is not null)
        {
            defs.AddRange(command.Options);
        }
    }
    return defs;
}

static (string Name, string? Inline) SplitLong(string body)
{
    var eq = body.IndexOf('=');
    return eq < 0 ? (body, null) : (body[..eq], body[(eq + 1)..]);
}

static void PrintUsageHint(List<Command> commands)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"  [grey]Usage:[/] [bold {Theme.Primary}]dbshift[/] [grey]<command>[/] [[options]]");
    AnsiConsole.MarkupLine($"  [grey]Commands:[/] {Markup.Escape(string.Join(", ", commands.Select(c => c.Name)))}");
    AnsiConsole.MarkupLine($"  [grey]Run[/] [bold {Theme.Primary}]dbshift --help[/] [grey]for full details.[/]");
    AnsiConsole.WriteLine();
}

static void PrintHelp(List<Command> commands, CommandOption[] globalOptions)
{
    ConsoleHelper.PrintBanner();

    var quickStart = new Panel(
        new Markup(
            $"  [bold]dbshift new[/]              [grey]Scaffold a new database project[/]{Environment.NewLine}" +
            $"  [bold]dbshift create -n Foo[/]     [grey]Create a migration script[/]{Environment.NewLine}" +
            $"  [bold]dbshift migrate -c \"$DB\"[/]  [grey]Apply migrations to a database[/]"))
        .Border(BoxBorder.Rounded)
        .BorderStyle(Style.Parse(Theme.Muted))
        .Header($" [{Theme.Primary}] Quick start [/]", Justify.Left)
        .Padding(1, 0);
    AnsiConsole.Write(quickStart);
    AnsiConsole.WriteLine();

    AnsiConsole.Write(new Rule($"[grey]Usage:[/] [bold {Theme.Primary}]dbshift[/] [grey]<command>[/] [[options]]")
        .RuleStyle(Style.Parse(Theme.Muted))
        .LeftJustified());
    AnsiConsole.WriteLine();

    var table = new Table().Border(TableBorder.Rounded).BorderStyle(Style.Parse(Theme.Muted)).Expand()
        .AddColumn(new TableColumn($"[{Theme.Warning}]Command[/]").LeftAligned())
        .AddColumn(new TableColumn($"[{Theme.Warning}]Description[/]").LeftAligned());

    foreach (var category in commands.GroupBy(c => c.Category))
    {
        table.AddRow(new Markup($"[dim]{new string('\u2500', 3)} {Markup.Escape(category.Key)} {new string('\u2500', 3)}[/]"), new Markup(""));
        foreach (var command in category)
        {
            table.AddRow($"  [bold {Theme.Primary}]{command.Name}[/]", $"[white]{Markup.Escape(command.Description)}[/]");
        }
    }
    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    var global = new Table().Border(TableBorder.Rounded).BorderStyle(Style.Parse(Theme.Muted)).Expand()
        .AddColumn(new TableColumn($"[{Theme.Warning}]Global Option[/]").LeftAligned())
        .AddColumn(new TableColumn($"[{Theme.Warning}]Description[/]").LeftAligned());

    foreach (var option in globalOptions)
    {
        var flag = option.ShortName is null ? $"--{option.LongName}" : $"-{option.ShortName}, --{option.LongName}";
        global.AddRow($"[bold {Theme.Primary}]{flag}[/]", $"[grey]{Markup.Escape(option.Description)}[/]");
    }
    AnsiConsole.Write(global);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"  [grey]Run[/] [bold {Theme.Primary}]dbshift <command> --help[/] [grey]for command-specific options.[/]");
    AnsiConsole.WriteLine();
}

static void PrintCommandHelp(Command command, CommandOption[] globalOptions)
{
    ConsoleHelper.PrintBanner();
    AnsiConsole.MarkupLine($"  [bold {Theme.Primary}]{command.Name}[/] [grey]- {Markup.Escape(command.Description)}[/]");

    if (command.UsageExample is not null)
    {
        var examplePanel = new Panel(new Markup($"[bold {Theme.Primary}]{Markup.Escape(command.UsageExample)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse(Theme.Muted))
            .Header($" [{Theme.Warning}] Example [/]", Justify.Left)
            .Padding(1, 0);
        AnsiConsole.Write(examplePanel);
    }
    AnsiConsole.WriteLine();

    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var allOptions = new List<CommandOption>();
    foreach (var option in command.Options.Concat(globalOptions))
    {
        if (seen.Add(option.LongName))
        {
            allOptions.Add(option);
        }
    }

    var table = new Table().Border(TableBorder.Rounded).BorderStyle(Style.Parse(Theme.Muted)).Expand()
        .AddColumn(new TableColumn($"[{Theme.Warning}]Option[/]").LeftAligned())
        .AddColumn(new TableColumn($"[{Theme.Warning}]Description[/]").LeftAligned());

    foreach (var option in allOptions)
    {
        var flag = option.ShortName is null ? $"--{option.LongName}" : $"-{option.ShortName}, --{option.LongName}";
        table.AddRow($"[bold {Theme.Primary}]{flag}[/]", $"[grey]{Markup.Escape(option.Description)}[/]");
    }
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}
