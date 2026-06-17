using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DbShift.Core.Enums;
using DbShift.Core.ValueObjects;

namespace DbShift.Engine.Parsing;

/// <summary>
/// Parses Flyway-style migration scripts into <see cref="ParsedMigration"/> objects and
/// provides deterministic hashing, syntax validation and dependency extraction.
///
/// Supported filename conventions:
/// <list type="bullet">
/// <item><c>V001__Name.sql</c>           - versioned schema/data/patch migration.</item>
/// <item><c>V202601150001__Name.sql</c>  - timestamp based versioned migration.</item>
/// <item><c>R__Name.sql</c>              - repeatable migration.</item>
/// <item><c>U001__Name.sql</c>           - rollback migration.</item>
/// </list>
/// </summary>
public sealed partial class ScriptParser
{
    private const string VersionPrefix = "V";
    private const string RollbackPrefix = "U";
    private const string RepeatablePrefix = "R";
    private const string Separator = "__";

    [GeneratedRegex(@"^\s*--\s*Depends\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DependsRegex();

    [GeneratedRegex(@"^\s*--\s*Author\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex AuthorRegex();

    [GeneratedRegex(@"^\s*--\s*Description\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DescriptionRegex();

    /// <summary>Parses a single migration script from its file path and content.</summary>
    public ParsedMigration Parse(string filePath, string content)
    {
        var fileName = GetFileName(filePath);
        var category = GetCategory(filePath);

        var stem = fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^4]
            : fileName;

        var separatorIndex = stem.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= stem.Length - Separator.Length)
        {
            throw new FormatException(
                $"Migration filename '{fileName}' is invalid. Expected format '<prefix><version>__<name>.sql' " +
                "(e.g. V001__CreateTable.sql, R__RefreshView.sql, U001__Rollback_CreateTable.sql).");
        }

        var prefix = stem[..separatorIndex];
        var name = stem[(separatorIndex + Separator.Length)..];

        var (version, type, isRepeatable) = Classify(prefix, category);

        return new ParsedMigration
        {
            FilePath = filePath,
            FileName = fileName,
            Version = version,
            Name = name,
            Type = type,
            Category = category,
            IsRepeatable = isRepeatable,
            Hash = GenerateHash(content),
            Content = content,
            Dependencies = ExtractDependencies(content),
            Author = ExtractFirst(AuthorRegex(), content),
            Description = ExtractFirst(DescriptionRegex(), content)
        };
    }

    /// <summary>Computes a deterministic SHA-256 hex hash for the supplied script content.</summary>
    public string GenerateHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Returns false for empty or whitespace-only scripts, true otherwise.</summary>
    public bool ValidateSyntax(string content) => !string.IsNullOrWhiteSpace(content);

    /// <summary>Extracts the comma separated dependencies declared via <c>-- Depends:</c>.</summary>
    public string[] ExtractDependencies(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<string>();
        }

        var match = DependsRegex().Match(content);
        if (!match.Success)
        {
            return Array.Empty<string>();
        }

        return match.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static (string Version, MigrationType Type, bool IsRepeatable) Classify(string prefix, string category)
    {
        if (prefix.Length == 1 && prefix.Equals(RepeatablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (RepeatablePrefix, MigrationType.Repeatable, true);
        }

        if (prefix.StartsWith(RollbackPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (prefix[1..], MigrationType.Rollback, false);
        }

        if (prefix.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var type = category.ToLowerInvariant() switch
            {
                "data" => MigrationType.Data,
                "patch" => MigrationType.Patch,
                _ => MigrationType.Schema
            };
            return (prefix[1..], type, false);
        }

        throw new FormatException($"Unrecognised migration prefix '{prefix}'. Valid prefixes are V, U and R.");
    }

    private static string ExtractFirst(Regex regex, string content)
    {
        var match = regex.Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string GetFileName(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    private static string GetCategory(string filePath)
    {
        var normalized = filePath.Replace('\\', '/').TrimEnd('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[^2] : string.Empty;
    }
}
