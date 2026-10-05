namespace MiniDebit;

public sealed class RateAppearance
{
    // Empty background follows the graph; empty text color chooses a readable contrast.
    public string BackgroundColor { get; set; } = "";
    public string TextColor { get; set; } = "";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSizePixels { get; set; } = 18;
    public int Style { get; set; } = (int)FontStyle.Bold;

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
    public static void Draw(Graphics g, RectangleF bounds, string down, string up, RateAppearance rates,
        GraphAppearance graph, bool dark, Font rateFont, Font labelFont, Font arrowFont)
    {
        using var fill = new SolidBrush(rates.Background(graph, dark));
        g.FillRectangle(fill, bounds);
        var text = rates.Text(graph, dark);
        var caption = rates.Text(graph, dark, true);
        float x = bounds.Left, y = bounds.Top, w = bounds.Width;
        using var divider = new Pen(Color.FromArgb(65, text));
        g.DrawLine(divider, x + 13, y, x + w - 13, y);
        g.DrawLine(divider, x + w / 2, y + 7, x + w / 2, y + 37);
        Text(g, "↓", arrowFont, graph.Reception, new RectangleF(x + 13, y + 5, 20, 27));
        Text(g, down, rateFont, text, new RectangleF(x + 35, y + 4, w / 2 - 45, 27));
        Text(g, "RÉCEPTION", labelFont, caption, new RectangleF(x + 35, y + 28, w / 2 - 45, 13));
        Text(g, "↑", arrowFont, graph.Sending, new RectangleF(x + w / 2 + 10, y + 5, 20, 27));
        Text(g, up, rateFont, text, new RectangleF(x + w / 2 + 32, y + 4, w / 2 - 42, 27));
        Text(g, "ENVOI", labelFont, caption, new RectangleF(x + w / 2 + 32, y + 28, w / 2 - 42, 13));
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
