using System.Drawing;

namespace UptimeKumaTrayAgent.Utils;

public sealed class ThemePalette
{
    public string Name { get; init; } = "Light";
    public Color Window { get; init; }
    public Color Surface { get; init; }
    public Color SurfaceAlt { get; init; }
    public Color Border { get; init; }
    public Color Text { get; init; }
    public Color MutedText { get; init; }
    public Color Accent { get; init; }
    public Color AccentDark { get; init; }
    public Color AccentSoft { get; init; }
    public Color ButtonText { get; init; }
    public Color Danger { get; init; }
    public Color Warning { get; init; }
    public Color Disabled { get; init; }
    public Color GridHeader { get; init; }
    public Color GridLine { get; init; }

    public static ThemePalette Light => new()
    {
        Name = "Light",
        Window = Color.FromArgb(248, 250, 252),
        Surface = Color.White,
        SurfaceAlt = Color.FromArgb(241, 245, 249),
        Border = Color.FromArgb(226, 232, 240),
        Text = Color.FromArgb(15, 23, 42),
        MutedText = Color.FromArgb(100, 116, 139),
        Accent = Color.FromArgb(16, 185, 129),
        AccentDark = Color.FromArgb(4, 120, 87),
        AccentSoft = Color.FromArgb(209, 250, 229),
        ButtonText = Color.White,
        Danger = Color.FromArgb(239, 68, 68),
        Warning = Color.FromArgb(245, 158, 11),
        Disabled = Color.FromArgb(226, 232, 240),
        GridHeader = Color.FromArgb(248, 250, 252),
        GridLine = Color.FromArgb(226, 232, 240)
    };

    public static ThemePalette Dark => new()
    {
        Name = "Dark",
        Window = Color.FromArgb(11, 14, 17),
        Surface = Color.FromArgb(18, 24, 29),
        SurfaceAlt = Color.FromArgb(24, 31, 37),
        Border = Color.FromArgb(45, 55, 66),
        Text = Color.FromArgb(226, 232, 240),
        MutedText = Color.FromArgb(148, 163, 184),
        Accent = Color.FromArgb(16, 185, 129),
        AccentDark = Color.FromArgb(52, 211, 153),
        AccentSoft = Color.FromArgb(6, 78, 59),
        ButtonText = Color.White,
        Danger = Color.FromArgb(248, 113, 113),
        Warning = Color.FromArgb(251, 191, 36),
        Disabled = Color.FromArgb(51, 65, 85),
        GridHeader = Color.FromArgb(15, 23, 42),
        GridLine = Color.FromArgb(45, 55, 66)
    };
}

public static class ThemeModes
{
    public static readonly string[] All = { "Light", "Dark" };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Light";
        }

        return All.FirstOrDefault(mode => string.Equals(mode, value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Light";
    }

    public static ThemePalette PaletteFor(string? value)
    {
        return string.Equals(Normalize(value), "Dark", StringComparison.OrdinalIgnoreCase)
            ? ThemePalette.Dark
            : ThemePalette.Light;
    }
}
