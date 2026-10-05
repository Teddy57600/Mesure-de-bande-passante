using System.Text.Json;

namespace MiniDebit;

public sealed class Settings
{
    public string AdapterId { get; set; } = "auto";
    public int IntervalMs { get; set; } = 1000;
    public int HistorySeconds { get; set; } = 60;
    public bool Bits { get; set; }
    public bool Dark { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartHidden { get; set; }
    public int OpacityPercent { get; set; } = 100;
    public double FixedScaleMbps { get; set; }
    public int Width { get; set; } = 340;
    public int Height { get; set; } = 176;
    public int? X { get; set; }
    public int? Y { get; set; }

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniDebit", "settings.json");

    public void Normalize()
    {
        AdapterId = string.IsNullOrWhiteSpace(AdapterId) ? "auto" : AdapterId;
        IntervalMs = Math.Clamp(IntervalMs, 250, 5000);
        HistorySeconds = Math.Clamp(HistorySeconds, 15, 600);
        OpacityPercent = Math.Clamp(OpacityPercent, 35, 100);
        FixedScaleMbps = double.IsFinite(FixedScaleMbps) ? Math.Clamp(FixedScaleMbps, 0, 100000) : 0;
        Width = Math.Clamp(Width, 270, 1000);
        Height = Math.Clamp(Height, 145, 600);
    }

    public static Settings Load(string? path = null)
    {
        try
        {
            var result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path ?? FilePath)) ?? new Settings();
            result.Normalize();
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return new Settings(); }
    }

    public void Save(string? path = null)
    {
        Normalize();
        var destination = path ?? FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        var temporary = destination + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, destination, true);
    }
}

public static class Units
{
    public static string Rate(double bytesPerSecond, bool bits)
    {
        double value = Math.Max(0, bytesPerSecond) * (bits ? 8 : 1);
        string[] units = bits ? ["bit/s", "kbit/s", "Mbit/s", "Gbit/s", "Tbit/s"] : ["o/s", "Ko/s", "Mo/s", "Go/s", "To/s"];
        var index = 0;
        while (value >= 1000 && index < units.Length - 1) { value /= 1000; index++; }
        return $"{value:0.0} {units[index]}";
    }

    public static string Volume(double bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        var index = 0;
        while (bytes >= 1000 && index < units.Length - 1) { bytes /= 1000; index++; }
        return $"{bytes:0.0} {units[index]}";
    }
}
