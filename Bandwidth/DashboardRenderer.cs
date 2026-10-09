using System.Drawing.Drawing2D;
using System.Globalization;

namespace MiniDebit;

internal static class DashboardRenderer
{
    public static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void Statistics(Graphics g, RectangleF bounds, GraphAppearance graph, bool dark,
        TrafficPeak peak, TrafficAverage average, bool bits)
    {
        int count = (graph.ShowMaximum ? 1 : 0) + (graph.ShowAverage ? 1 : 0);
        if (count == 0) return;
        float gap = 6, width = Math.Min(220, (bounds.Width - gap * (count - 1)) / count);
        float left = bounds.Left + (bounds.Width - width * count - gap * (count - 1)) / 2;
        using var font = graph.MaximumText.CreateFont();
        if (graph.ShowMaximum)
        {
            Badge(new RectangleF(left, bounds.Top, width, bounds.Height), "Max : ", Units.Rate(peak.Rate, bits),
                GraphAppearance.Resolve(graph.MaximumText.ColorHex, peak.Reception ? graph.Reception : graph.Sending));
            left += width + gap;
        }
        if (graph.ShowAverage)
            Badge(new RectangleF(left, bounds.Top, width, bounds.Height), bounds.Width < 360 ? "Moy. : " : "Moyenne : ",
                Units.Rate(average.Down + average.Up, bits), graph.Reception);

        void Badge(RectangleF rectangle, string caption, string value, Color color)
        {
            using var shape = Round(rectangle, rectangle.Height / 2);
            using var fill = new SolidBrush(Color.FromArgb(dark ? 35 : 16, color));
            g.FillPath(fill, shape);
            using var format = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces };
            float captionWidth = g.MeasureString(caption, font, int.MaxValue, format).Width;
            float valueWidth = g.MeasureString(value, font, int.MaxValue, format).Width;
            float total = captionWidth + valueWidth;
            var state = g.Save();
            g.SetClip(rectangle, CombineMode.Intersect);
            float fit = Math.Min(1, (rectangle.Width - 12) / Math.Max(1, total));
            g.TranslateTransform(rectangle.Left + (rectangle.Width - total * fit) / 2, rectangle.Top + (rectangle.Height - font.GetHeight(96) * fit) / 2);
            g.ScaleTransform(fit, fit);
            using var muted = new SolidBrush(dark ? Color.FromArgb(165, 181, 202) : Color.FromArgb(94, 115, 145));
            using var accent = new SolidBrush(color);
            g.DrawString(caption, font, muted, 0, 0, format);
            g.DrawString(value, font, accent, captionWidth, 0, format);
            g.Restore(state);
        }
    }

    private static string[] AxisLabels(double scale, bool bits, int ticks)
    {
        double maximum = scale * (bits ? 8 : 1);
        int unit = 0;
        while (maximum >= 1000 && unit < 4) { maximum /= 1000; unit++; }
        return Enumerable.Range(0, ticks + 1).Select(i =>
            (maximum * (ticks - i) / ticks).ToString("0.#", CultureInfo.CurrentCulture)).ToArray();
    }

    public static float AxisWidth(Graphics g, double scale, bool bits, Font font)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic);
        // Measure every tick: intermediate decimal values can exceed the top label.
        return (float)Math.Ceiling(AxisLabels(scale, bits, 4).Max(label =>
            g.MeasureString(label, font, int.MaxValue, format).Width)) + 3;
    }

    public static void Axes(Graphics g, RectangleF chart, double scale, bool bits, Color muted, Font font)
    {
        using var brush = new SolidBrush(muted);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        int ticks = chart.Height < 65 ? 2 : 4;
        var labels = AxisLabels(scale, bits, ticks);
        for (int i = 0; i <= ticks; i++)
        {
            g.DrawString(labels[i], font, brush, new RectangleF(0, chart.Top + chart.Height * i / ticks - 8, chart.Left - 3, 16), format);
        }
        using var axis = new Pen(Color.FromArgb(110, muted), .7f);
        g.DrawLine(axis, chart.Left, chart.Top, chart.Left, chart.Bottom);
        g.DrawLine(axis, chart.Left, chart.Bottom, chart.Right, chart.Bottom);
    }

    public static void Guides(Graphics g, RectangleF chart, GraphAppearance graph, TrafficPeak peak,
        TrafficAverage average, double scale)
    {
        if (scale <= 0) return;
        if (graph.ShowMaximum) Line(peak.Rate, peak.Reception ? graph.Reception : graph.Sending);
        if (graph.ShowAverage && average.Seconds > 0) Line(average.Down + average.Up, graph.Reception);
        void Line(double value, Color color)
        {
            if (value > scale) return;
            float y = chart.Bottom - (float)Math.Clamp(value / scale, 0, 1) * chart.Height;
            using var pen = new Pen(Color.FromArgb(180, color), 1) { DashPattern = [5, 4] };
            g.DrawLine(pen, chart.Left, y, chart.Right, y);
        }
    }
}
