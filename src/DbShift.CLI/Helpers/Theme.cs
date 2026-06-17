using Spectre.Console;

namespace DbShift.CLI.Helpers;

public static class Theme
{
    // Brand palette -------------------------------------------------------
    public const string Primary = "#22D3EE";
    public const string Secondary = "#3B82F6";
    public const string Accent = "#8B5CF6";

    public const string Success = "#4ADE80";
    public const string Warning = "#FACC15";
    public const string Danger = "#F87171";
    public const string Info = "#38BDF8";
    public const string Muted = "#64748B";
    public const string Dim = "#475569";
    public const string Text = "#E2E8F0";

    // Spectre Color shortcuts --------------------------------------------
    public static Color PrimaryColor { get; } = Color.Cyan1;
    public static Color AccentColor { get; } = Color.Violet;
    public static Color SuccessColor { get; } = Color.Green1;
    public static Color WarningColor { get; } = Color.Yellow3;
    public static Color DangerColor { get; } = Color.Red1;
    public static Color MutedColor { get; } = Color.Grey50;

    // Reusable styles -----------------------------------------------------
    public static Style BrandStyle { get; } = new(Color.Cyan1, null, Decoration.Bold);
    public static Style MutedStyle { get; } = new(Color.Grey50);
    public static Style DimStyle { get; } = new(Color.Grey46);
    public static Style LabelStyle { get; } = new(Color.Cyan1, null, Decoration.Bold);
    public static Style ValueStyle { get; } = new(Color.White);
    public static Style HeadingStyle { get; } = new(Color.Cyan1, null, Decoration.Bold);

    // Glyphs --------------------------------------------------------------
    public const string Check = "[bold green]\u2713[/]";
    public const string Cross = "[bold red]\u2717[/]";
    public const string Warn = "[bold yellow]\u26A0[/]";
    public const string InfoGlyph = "[bold cyan]\u2139[/]";
    public const string Step = "[grey]\u2192[/]";
    public const string Bullet = "[cyan]\u2022[/]";
    public const string Arrow = "[grey]\u203A[/]";
    public const string Pointer = "[bold cyan]>[/]";
    public const string Separator = "[grey]\u2501[/]";
}
