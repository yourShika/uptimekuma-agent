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
        Window = Color.FromArgb(246, 248, 250),
        Surface = Color.White,
        SurfaceAlt = Color.FromArgb(239, 244, 241),
        Border = Color.FromArgb(217, 225, 221),
        Text = Color.FromArgb(31, 41, 55),
        MutedText = Color.FromArgb(94, 109, 122),
        Accent = Color.FromArgb(92, 217, 115),
        AccentDark = Color.FromArgb(34, 143, 67),
        AccentSoft = Color.FromArgb(221, 247, 226),
        ButtonText = Color.FromArgb(20, 28, 35),
        Danger = Color.FromArgb(230, 72, 72),
        Warning = Color.FromArgb(245, 168, 45),
        Disabled = Color.FromArgb(225, 229, 232),
        GridHeader = Color.FromArgb(232, 240, 235),
        GridLine = Color.FromArgb(219, 228, 223)
    };

    public static ThemePalette Dark => new()
    {
        Name = "Dark",
        Window = Color.FromArgb(18, 24, 28),
        Surface = Color.FromArgb(27, 35, 40),
        SurfaceAlt = Color.FromArgb(35, 45, 50),
        Border = Color.FromArgb(54, 68, 74),
        Text = Color.FromArgb(232, 238, 241),
        MutedText = Color.FromArgb(165, 178, 184),
        Accent = Color.FromArgb(92, 217, 115),
        AccentDark = Color.FromArgb(46, 171, 82),
        AccentSoft = Color.FromArgb(37, 78, 51),
        ButtonText = Color.FromArgb(10, 22, 16),
        Danger = Color.FromArgb(255, 109, 109),
        Warning = Color.FromArgb(255, 194, 83),
        Disabled = Color.FromArgb(61, 69, 73),
        GridHeader = Color.FromArgb(38, 50, 55),
        GridLine = Color.FromArgb(54, 66, 72)
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
