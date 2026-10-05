namespace MiniDebit;

public sealed class RateAppearance
{
    // Empty background follows the graph; empty text color chooses a readable contrast.
    public string BackgroundColor { get; set; } = "";
    public string TextColor { get; set; } = "";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSizePixels { get; set; } = 18;
    public int Style { get; set; } = (int)FontStyle.Bold;
    public bool Compact { get; set; }

    public void Normalize()
    {
        BackgroundColor = GraphAppearance.NormalizeColor(BackgroundColor, "");
        TextColor = GraphAppearance.NormalizeColor(TextColor, "");
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily.Trim();
        FontSizePixels = double.IsFinite(FontSizePixels) ? Math.Clamp(FontSizePixels, 8, 24) : 18;
        Style &= 15;
    }

    public Color Background(GraphAppearance graph, bool dark) => GraphAppearance.Resolve(BackgroundColor, graph.Background(dark));
    public Color Text(GraphAppearance graph, bool dark, bool caption = false)
    {
        bool lightBackground = Background(graph, dark).GetBrightness() > .5;
        var automatic = caption
            ? lightBackground ? Color.FromArgb(94, 115, 141) : Color.FromArgb(145, 161, 184)
            : lightBackground ? Color.FromArgb(27, 42, 61) : Color.FromArgb(233, 240, 250);
        return GraphAppearance.Resolve(TextColor, automatic);
    }

    public Font CreateFont(bool caption = false)
    {
        float size = (float)(caption ? Math.Clamp(FontSizePixels * .5, 7, 12) : Math.Clamp(FontSizePixels, 8, 24));
        try
        {
            using var family = new System.Drawing.FontFamily(FontFamily);
            var face = (FontStyle)(Style & 3);
            return new Font(family, size, family.IsStyleAvailable(face) ? (FontStyle)Style : (FontStyle)(Style & 12), GraphicsUnit.Pixel);
        }
        catch (ArgumentException) { return new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel); }
    }
}

internal static class RateRenderer
{
    internal readonly record struct Layout(float Height, float NumberTop, float NumberHeight, float CaptionTop, float CaptionHeight);
    public static Layout Measure(Font numbers, Font captions, Font arrows, bool compact)
    {
        // Measure at 96 DPI because the drawing surface scales these logical pixels.
        float numberHeight = (float)Math.Ceiling(Math.Max(numbers.GetHeight(96), arrows.GetHeight(96)));
        float captionHeight = (float)Math.Ceiling(captions.GetHeight(96));
        if (compact)
            return new(numberHeight + captionHeight + 4, 2, numberHeight, 2 + numberHeight, captionHeight);
        numberHeight = Math.Max(27, numberHeight);
        captionHeight = Math.Max(13, captionHeight);
        float captionTop = Math.Max(28, 4 + numbers.GetHeight(96));
        return new(Math.Max(48, captionTop + captionHeight + 7), 4, numberHeight, captionTop, captionHeight);
    }

    public static void Draw(Graphics g, RectangleF bounds, string down, string up, RateAppearance rates,
        GraphAppearance graph, bool dark, Font rateFont, Font labelFont, Font arrowFont)
    {
        using var fill = new SolidBrush(rates.Background(graph, dark));
        g.FillRectangle(fill, bounds);
        var text = rates.Text(graph, dark);
        var caption = rates.Text(graph, dark, true);
        float x = bounds.Left, y = bounds.Top, w = bounds.Width;
        var layout = Measure(rateFont, labelFont, arrowFont, rates.Compact);
        using var divider = new Pen(Color.FromArgb(65, text));
        g.DrawLine(divider, x + 13, y, x + w - 13, y);
        g.DrawLine(divider, x + w / 2, y + (rates.Compact ? 2 : 7), x + w / 2, y + layout.Height - (rates.Compact ? 2 : 11));
        Text(g, "↓", arrowFont, graph.Reception, new RectangleF(x + 13, y + layout.NumberTop, 20, layout.NumberHeight));
        Text(g, down, rateFont, text, new RectangleF(x + 35, y + layout.NumberTop, w / 2 - 45, layout.NumberHeight));
        Text(g, "RÉCEPTION", labelFont, caption, new RectangleF(x + 35, y + layout.CaptionTop, w / 2 - 45, layout.CaptionHeight));
        Text(g, "↑", arrowFont, graph.Sending, new RectangleF(x + w / 2 + 10, y + layout.NumberTop, 20, layout.NumberHeight));
        Text(g, up, rateFont, text, new RectangleF(x + w / 2 + 32, y + layout.NumberTop, w / 2 - 42, layout.NumberHeight));
        Text(g, "ENVOI", labelFont, caption, new RectangleF(x + w / 2 + 32, y + layout.CaptionTop, w / 2 - 42, layout.CaptionHeight));
    }

    private static void Text(Graphics g, string value, Font font, Color color, RectangleF bounds)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(value, font, brush, bounds, format);
    }
}

internal sealed class RatePreview : Control
{
    private readonly Func<RateAppearance> rates;
    private readonly Func<GraphAppearance> graph;
    private readonly Func<bool> dark;
    public RatePreview(Func<RateAppearance> rates, Func<GraphAppearance> graph, Func<bool> dark)
    {
        this.rates = rates; this.graph = graph; this.dark = dark;
        DoubleBuffered = true; ResizeRedraw = true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float dpi = DeviceDpi / 96f;
        e.Graphics.ScaleTransform(dpi, dpi);
        var style = rates();
        using var numbers = style.CreateFont();
        using var captions = style.CreateFont(true);
        using var arrows = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
        RateRenderer.Draw(e.Graphics, new RectangleF(0, 0, Width / dpi, Height / dpi), "22,1 Ko/s", "7,7 Ko/s",
            style, graph(), dark(), numbers, captions, arrows);
    }
}
