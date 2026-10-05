using System.Drawing.Drawing2D;
using System.Globalization;

namespace MiniDebit;

public sealed class GraphAppearance
{
    public const string DefaultDown = "#45CCE6";
    public const string DefaultUp = "#FFAE5A";
    public string DownColor { get; set; } = DefaultDown;
    public string UpColor { get; set; } = DefaultUp;
    // Empty means follow the selected light/dark theme.
    public string BackgroundColor { get; set; } = "";
    public string GridColor { get; set; } = "";
    public double LineWidth { get; set; } = 1.4;
    public int LineStyle { get; set; }
    public bool ShowGrid { get; set; } = true;
    public int GridStyle { get; set; } = 1;
    public bool FillArea { get; set; } = true;
    public int FillOpacityPercent { get; set; } = 14;
    public bool ShowMaximum { get; set; } = true;

    public void Normalize()
    {
        DownColor = NormalizeColor(DownColor, DefaultDown);
        UpColor = NormalizeColor(UpColor, DefaultUp);
        BackgroundColor = NormalizeColor(BackgroundColor, "");
        GridColor = NormalizeColor(GridColor, "");
        LineWidth = double.IsFinite(LineWidth) ? Math.Clamp(LineWidth, 0.5, 6) : 1.4;
        LineStyle = Math.Clamp(LineStyle, 0, 2);
        GridStyle = Math.Clamp(GridStyle, 0, 2);
        FillOpacityPercent = Math.Clamp(FillOpacityPercent, 0, 100);
    }

    internal static string NormalizeColor(string? value, string fallback) =>
        value is { Length: 7 } && value[0] == '#' && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out _) ? value.ToUpperInvariant() : fallback;

    public static Color Resolve(string? value, Color fallback) =>
        value is { Length: 7 } && value[0] == '#' && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out var rgb) ? Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255) : fallback;

    public Color Reception => Resolve(DownColor, Color.FromArgb(69, 204, 230));
    public Color Sending => Resolve(UpColor, Color.FromArgb(255, 174, 90));
    public Color Background(bool dark) => Resolve(BackgroundColor, dark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 249, 253));
    public Color Grid(bool dark) => Resolve(GridColor, dark ? Color.FromArgb(42, 53, 70) : Color.FromArgb(215, 226, 239));
    public static DashStyle Dash(int style) => style switch { 1 => DashStyle.Dot, 2 => DashStyle.Dash, _ => DashStyle.Solid };
}

internal static class GraphRenderer
{
    public static void Maximum(Graphics g, RectangleF chart, GraphAppearance appearance, bool dark,
        TrafficPeak peak, double scale, bool bits)
    {
        if (!appearance.ShowMaximum || chart.Height <= 0 || scale <= 0) return;
        float y = chart.Bottom - (float)Math.Clamp(peak.Rate / scale, 0, 1) * chart.Height;
        var color = peak.Reception ? appearance.Reception : appearance.Sending;
        using var line = new Pen(Color.FromArgb(190, color), 1) { DashStyle = DashStyle.Dash };
        g.DrawLine(line, chart.Left, y, chart.Right, y);
        string label = $"Max {(peak.Reception ? "↓" : "↑")} : {Units.Rate(peak.Rate, bits)}";
        if (peak.Rate > scale) label += " (hors échelle)";
        using var font = new Font("Segoe UI", 9, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        float width = Math.Min(chart.Width - 4, g.MeasureString(label, font).Width + 8);
        var textBounds = new RectangleF(chart.Left + (chart.Width - width) / 2, y - 14, width, 12);
        using var background = new SolidBrush(appearance.Background(dark));
        using var text = new SolidBrush(color);
        g.FillRectangle(background, textBounds);
        g.DrawString(label, font, text, textBounds, format);
    }

    public static void Backdrop(Graphics g, RectangleF chart, GraphAppearance appearance, bool dark)
    {
        using var background = new SolidBrush(appearance.Background(dark));
        g.FillRectangle(background, chart);
        if (!appearance.ShowGrid) return;
        using var grid = new Pen(appearance.Grid(dark), .6f) { DashStyle = GraphAppearance.Dash(appearance.GridStyle) };
        for (int i = 0; i <= 4; i++)
        {
            float y = chart.Top + chart.Height * i / 4;
            g.DrawLine(grid, chart.Left, y, chart.Right, y);
        }
        for (int i = 0; i <= 8; i++)
        {
            float x = chart.Left + chart.Width * i / 8;
            g.DrawLine(grid, x, chart.Top, x, chart.Bottom);
        }
    }

    public static void Curve(Graphics g, RectangleF chart, PointF[] points, Color color, GraphAppearance appearance)
    {
        if (points.Length < 2) return;
        var state = g.Save();
        try
        {
            // Thick strokes must stay inside the chart, including peaks at its maximum.
            g.SetClip(chart, CombineMode.Intersect);
            if (appearance.FillArea && appearance.FillOpacityPercent > 0)
            {
                using var path = new GraphicsPath();
                path.AddLines(points);
                path.AddLine(points[^1], new PointF(points[^1].X, chart.Bottom));
                path.AddLine(new PointF(points[^1].X, chart.Bottom), new PointF(points[0].X, chart.Bottom));
                path.CloseFigure();
                using var fill = new SolidBrush(Color.FromArgb((int)Math.Round(appearance.FillOpacityPercent * 2.55), color));
                g.FillPath(fill, path);
            }
            using var line = new Pen(color, (float)appearance.LineWidth) { DashStyle = GraphAppearance.Dash(appearance.LineStyle), LineJoin = LineJoin.Round };
            g.DrawLines(line, points);
        }
        finally { g.Restore(state); }
    }
}

internal sealed class GraphPreview : Control
{
    private readonly Func<GraphAppearance> appearance;
    private readonly Func<bool> dark;
    public GraphPreview(Func<GraphAppearance> appearance, Func<bool> dark)
    {
        this.appearance = appearance;
        this.dark = dark;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        float dpi = DeviceDpi / 96f;
        g.ScaleTransform(dpi, dpi);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var chart = new RectangleF(0, 0, Width / dpi - 1, Height / dpi - 1);
        var style = appearance();
        GraphRenderer.Backdrop(g, chart, style, dark());
        PointF[] Points(float[] values) => values.Select((value, index) =>
            new PointF(chart.Left + index * chart.Width / (values.Length - 1), chart.Bottom - value * chart.Height)).ToArray();
        GraphRenderer.Curve(g, chart, Points([.04f, .06f, .08f, .3f, .12f, .07f, .1f, .24f, .06f, .08f, .16f, .04f, .07f]), style.Sending, style);
        GraphRenderer.Curve(g, chart, Points([.07f, .12f, .06f, .62f, .24f, .16f, .08f, .44f, .18f, .09f, .8f, .18f, .1f]), style.Reception, style);
        GraphRenderer.Maximum(g, chart, style, dark(), new TrafficPeak(80000, true), 100000, false);
    }
}
