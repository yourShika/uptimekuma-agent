using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace UptimeKumaTrayAgent.Utils;

public enum AgentTrayState
{
    Ok,
    Warning,
    Inactive
}

public static class AppIconFactory
{
    public static Icon CreateIcon(int size = 64)
    {
        using var bitmap = CreateBitmap(size);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public static Icon CreateIconForState(AgentTrayState state, int size = 64)
    {
        var color = state switch
        {
            AgentTrayState.Ok => Color.FromArgb(92, 217, 115),
            AgentTrayState.Warning => Color.FromArgb(255, 204, 70),
            AgentTrayState.Inactive => Color.FromArgb(235, 84, 84),
            _ => Color.FromArgb(92, 217, 115)
        };
        var deepColor = state switch
        {
            AgentTrayState.Ok => Color.FromArgb(34, 143, 67),
            AgentTrayState.Warning => Color.FromArgb(190, 132, 20),
            AgentTrayState.Inactive => Color.FromArgb(168, 44, 50),
            _ => Color.FromArgb(34, 143, 67)
        };

        using var bitmap = CreateBitmap(size, color, deepColor);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public static Bitmap CreateBitmap(int size = 64)
    {
        return CreateBitmap(size, Color.FromArgb(92, 217, 115), Color.FromArgb(34, 143, 67));
    }

    private static Bitmap CreateBitmap(int size, Color primary, Color secondary)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Transparent);

        var scale = size / 64f;
        using var shadow = new SolidBrush(Color.FromArgb(42, 0, 0, 0));
        using var green = new SolidBrush(primary);
        using var deepGreen = new SolidBrush(secondary);
        using var white = new SolidBrush(Color.White);
        using var pen = new Pen(Color.White, Math.Max(3f, 4.5f * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        graphics.FillEllipse(shadow, 5 * scale, 7 * scale, 54 * scale, 54 * scale);
        graphics.FillEllipse(green, 4 * scale, 3 * scale, 56 * scale, 56 * scale);
        graphics.FillEllipse(deepGreen, 13 * scale, 13 * scale, 38 * scale, 38 * scale);

        var points = new[]
        {
            new PointF(18 * scale, 32 * scale),
            new PointF(26 * scale, 40 * scale),
            new PointF(43 * scale, 22 * scale)
        };
        graphics.DrawLines(pen, points);

        using var glowPen = new Pen(Color.FromArgb(170, 255, 255, 255), Math.Max(1.5f, 2f * scale));
        graphics.DrawArc(glowPen, 11 * scale, 9 * scale, 42 * scale, 42 * scale, 210, 70);

        return bitmap;
    }
}
