using System.Drawing.Drawing2D;

namespace MiniDebit;

public sealed class FrameAppearance
{
    public bool Enabled { get; set; } = true;
    public string ColorHex { get; set; } = "";
    public double WidthPixels { get; set; } = 1;
    public int Style { get; set; }

    public void Normalize()
    {
        ColorHex = GraphAppearance.NormalizeColor(ColorHex, "");
        WidthPixels = double.IsFinite(WidthPixels) ? Math.Clamp(WidthPixels, .5, 8) : 1;
        Style = Math.Clamp(Style, 0, 2);
    }

    public Color Color(bool dark) => GraphAppearance.Resolve(ColorHex,
        dark ? System.Drawing.Color.FromArgb(42, 53, 70) : System.Drawing.Color.FromArgb(215, 226, 239));
    [System.Text.Json.Serialization.JsonIgnore]
    public float Inset => Enabled ? (float)WidthPixels : 0;
}

internal static class FrameRenderer
{
    public static void Draw(Graphics g, RectangleF bounds, FrameAppearance appearance, bool dark)
    {
        if (!appearance.Enabled) return;
        float width = (float)appearance.WidthPixels;
        using var pen = new Pen(appearance.Color(dark), width) { DashStyle = GraphAppearance.Dash(appearance.Style), LineJoin = LineJoin.Miter };
        g.DrawRectangle(pen, bounds.Left + width / 2, bounds.Top + width / 2,
            Math.Max(0, bounds.Width - width), Math.Max(0, bounds.Height - width));
    }
}

internal sealed class FramePreview(Func<FrameAppearance> frame, Func<GraphAppearance> graph,
    Func<RateAppearance> rates, Func<bool> dark) : Control
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        float dpi = DeviceDpi / 96f;
        g.ScaleTransform(dpi, dpi);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool isDark = dark();
        g.Clear(isDark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 249, 253));
        var border = frame(); var style = graph(); var band = rates();
        float w = Width / dpi, h = Height / dpi, inset = border.Inset;
        using var values = band.CreateFont(); using var captions = band.CreateFont(true);
        using var arrows = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
        float bandHeight = RateRenderer.Measure(values, captions, arrows, band.Compact).Height;
        float margin = inset + style.MarginPixels;
        var chart = new RectangleF(margin, margin, w - margin * 2, Math.Max(1, h - margin * 2 - bandHeight));
        GraphRenderer.Backdrop(g, chart, style, isDark);
        PointF[] Points(float[] samples) => samples.Select((sample, i) =>
            new PointF(chart.Left + i * chart.Width / (samples.Length - 1), chart.Bottom - sample * chart.Height)).ToArray();
        GraphRenderer.Curve(g, chart, Points([.05f, .12f, .08f, .35f, .15f, .06f, .25f, .09f]), style.Sending, style);
        GraphRenderer.Curve(g, chart, Points([.08f, .3f, .1f, .65f, .24f, .4f, .12f, .2f]), style.Reception, style);
        GraphRenderer.Average(g, chart, style, isDark, new TrafficAverage(18000, 9000, 60), 100000, false, RectangleF.Empty);
        RateRenderer.Draw(g, new RectangleF(inset, h - inset - bandHeight, w - 2 * inset, bandHeight),
            "22,1 Ko/s", "7,7 Ko/s", band, style, isDark, values, captions, arrows);
        FrameRenderer.Draw(g, new RectangleF(0, 0, w, h), border, isDark);
    }
}
