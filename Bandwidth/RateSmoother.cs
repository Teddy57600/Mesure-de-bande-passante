namespace MiniDebit;

// Display-only filter. Measurements and persisted samples never pass through it.
public sealed class RateSmoother
{
    public const double ResponsiveThreshold = 1_000_000; // Decimal Mo/s, like the widget units.
    private bool initialized;
    private int previousLevel = -1;
    public int Level => previousLevel;
    public double Down { get; private set; }
    public double Up { get; private set; }

    public void Reset() { initialized = false; Down = Up = 0; }

    public void Update(Sample sample, int level, bool connected)
    {
        level = Math.Clamp(level, 0, 3);
        if (!connected || !double.IsFinite(sample.ElapsedSeconds) || sample.ElapsedSeconds <= 0)
        { Reset(); previousLevel = level; return; }
        double down = Clean(sample.Down), up = Clean(sample.Up);
        if (!initialized || level == 0 || level != previousLevel)
        { Down = down; Up = up; initialized = true; }
        else
        {
            Down = Smooth(Down, down, level, sample.ElapsedSeconds);
            Up = Smooth(Up, up, level, sample.ElapsedSeconds);
        }
        previousLevel = level;
    }

    private static double Clean(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
    private static double Smooth(double previous, double measured, int level, double elapsed)
    {
        // Including the previous display also makes the end of a large transfer responsive.
        bool highThroughput = Math.Max(previous, measured) >= ResponsiveThreshold;
        double seconds = highThroughput
            ? level switch { 1 => .35, 2 => .5, _ => .75 }
            : level switch { 1 => 5, 2 => 8, _ => 12 };
        double alpha = 1 - Math.Exp(-elapsed / seconds);
        double value = previous + alpha * (measured - previous);
        return measured == 0 && value < .5 ? 0 : value;
    }
}
