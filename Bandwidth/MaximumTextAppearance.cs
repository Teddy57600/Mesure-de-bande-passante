namespace MiniDebit;

public sealed class MaximumTextAppearance
{
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSizePixels { get; set; } = 13;
    public int Style { get; set; } = (int)FontStyle.Bold;
    public string ColorHex { get; set; } = "";

    public void Normalize()
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily.Trim();
        FontSizePixels = double.IsFinite(FontSizePixels) ? Math.Clamp(FontSizePixels, 8, 24) : 9;
        Style &= 15;
        ColorHex = GraphAppearance.NormalizeColor(ColorHex, "");
    }

    public Font CreateFont()
    {
        float size = (float)Math.Clamp(FontSizePixels, 8, 24);
        try
        {
            using var family = new System.Drawing.FontFamily(FontFamily);
            var face = (FontStyle)(Style & 3);
            return new Font(family, size, family.IsStyleAvailable(face) ? (FontStyle)Style : (FontStyle)(Style & 12), GraphicsUnit.Pixel);
        }
        catch (ArgumentException) { return new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel); }
    }
}
