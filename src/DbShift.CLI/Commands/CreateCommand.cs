using System.Globalization;
using System.Text;
using DbShift.CLI.Helpers;
using DbShift.Core.Enums;

namespace DbShift.CLI.Commands;

public sealed class CreateCommand : CommandBase
{
    public override string Name => "create";
    public override string Description => "Scaffold a new migration script from a template.";
    public override string Category => "Setup";
    public override string? UsageExample => "dbshift create --name AddOrders --type schema --author jane";
    public override IReadOnlyList<CommandOption> Options => new[]
    {
        new CommandOption("name", null, "Migration name (PascalCase)", false, "NAME"),
        new CommandOption("type", 't', "Migration type: schema | data | patch | rollback | repeatable", false, "TYPE"),
        new CommandOption("author", 'a', "Author name to embed in the header", false, "NAME"),
        new CommandOption("description", 'd', "Short description to embed in the header", false, "TEXT"),
        new CommandOption("dir", null, "Override the output directory", false, "PATH"),
        new CommandOption("sequence", null, "Use a sequence version instead of a timestamp", true, null)
    };

    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var host = CreateHost(context);
        var name = context.GetOption("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(Fail(context, "The --name option is required."));
        }

        var (type, folder, prefix, templateName) = ResolveType(context.GetOption("type", "schema"));

        string version;
        string fileName;

        if (type == MigrationType.Repeatable)
        {
            version = "R";
            fileName = $"R__{name}.sql";
        }
        else
        {
            var useTimestamp = !context.GetFlag("sequence");
            version = useTimestamp
                ? DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                : NextSequence(host, folder, prefix);

            var namePart = type == MigrationType.Rollback ? $"Rollback_{name}" : name;
            fileName = $"{prefix}{version}__{namePart}.sql";
        }

        var displayFileName = type == MigrationType.Repeatable ? $"R__{name}" : $"{prefix}{version}__{(type == MigrationType.Rollback ? $"Rollback_{name}" : name)}";

        var outputDir = context.GetOption("dir") ?? Path.Combine(host.ScriptsPath, folder);
        Directory.CreateDirectory(outputDir);
        var fullPath = Path.Combine(outputDir, fileName);

        if (File.Exists(fullPath))
        {
            return Task.FromResult(Fail(context, $"A file already exists at '{fullPath}'."));
        }

        var templatePath = Path.Combine(host.BasePath, "Database", "Templates", templateName);
        var content = File.Exists(templatePath)
            ? RenderTemplate(File.ReadAllText(templatePath), name, context.GetOption("author") ?? Environment.UserName,
                context.GetOption("description") ?? name)
            : DefaultContent(name, context.GetOption("author") ?? Environment.UserName,
                context.GetOption("description") ?? name, type);

        File.WriteAllText(fullPath, content);

        if (context.Json)
        {
            WriteJson(new { success = true, version, path = fullPath, type = type.ToString() });
        }
        else
        {
            ConsoleHelper.PrintSuccess($"Created {type} migration '{displayFileName}'.");
            ConsoleHelper.PrintKeyValue("file", fullPath);
        }
        return Task.FromResult(0);
    }

    private static (MigrationType Type, string Folder, string Prefix, string Template) ResolveType(string type) => type.ToLowerInvariant() switch
    {
        "data" => (MigrationType.Data, "Data", "V", "data_migration.sql"),
        "patch" => (MigrationType.Patch, "Patch", "V", "patch_migration.sql"),
        "rollback" => (MigrationType.Rollback, "Rollback", "U", "rollback_migration.sql"),
        "repeatable" => (MigrationType.Repeatable, "Schema", "R", "repeatable_migration.sql"),
        _ => (MigrationType.Schema, "Schema", "V", "schema_migration.sql")
    };

    private static string NextSequence(CliHost host, string folder, string prefix)
    {
        var dir = Path.Combine(host.ScriptsPath, folder);
        var max = 0;
        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, $"{prefix}*__*.sql"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var digits = name[prefix.Length..].TakeWhile(char.IsDigit).ToArray();
                if (digits.Length > 0 && int.TryParse(new string(digits), out var n) && n < 100000 && n > max)
                {
                    max = n;
                }
            }
        }
        return (max + 1).ToString("000", CultureInfo.InvariantCulture);
    }

    private static string RenderTemplate(string template, string name, string author, string description)
        => template
            .Replace("{{NAME}}", name)
            .Replace("{{AUTHOR}}", author)
            .Replace("{{DATE}}", DateTime.UtcNow.ToString("yyyy-MM-dd"))
            .Replace("{{DESCRIPTION}}", description);

    private static string DefaultContent(string name, string author, string description, MigrationType type)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"-- Migration: {name}");
        sb.AppendLine($"-- Author: {author}");
        sb.AppendLine($"-- Created: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"-- Description: {description}");
        sb.AppendLine($"-- Type: {type}");
        sb.AppendLine();
        sb.AppendLine("-- TODO: add your SQL here");
        return sb.ToString();
    }
}
