using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UptimeKumaTrayAgent.Utils;

public sealed class ModernCardPanel : Panel
{
    public ModernCardPanel()
    {
        DoubleBuffered = true;
        Margin = new Padding(8);
        Padding = new Padding(18);
    }

    public int Radius { get; set; } = 14;
    public Color FillColor { get; set; } = Color.White;
    public Color BorderColor { get; set; } = Color.Gainsboro;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        using var path = CreateRoundRect(bounds, Radius);
        using var fill = new SolidBrush(FillColor);
        using var border = new Pen(BorderColor);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Invalidate();
    }

    private static GraphicsPath CreateRoundRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class HiddenTabControl : TabControl
{
    protected override void WndProc(ref Message m)
    {
        const int tcmAdjustRect = 0x1328;
        if (m.Msg == tcmAdjustRect && !DesignMode)
        {
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }
}
