namespace MiniDebit;

public static class WindowPlacement
{
    public static Point Beside(Size size, Rectangle widget, IReadOnlyList<Rectangle> workingAreas, int gap)
    {
        Rectangle? fallback = null;
        long leastOverlap = long.MaxValue;
        foreach (var area in workingAreas)
        {
            Point[] candidates = [new(widget.Right + gap, widget.Top), new(widget.Left - size.Width - gap, widget.Top),
                new(widget.Left, widget.Top - size.Height - gap), new(widget.Left, widget.Bottom + gap),
                area.Location, new(area.Right - size.Width, area.Top), new(area.Left, area.Bottom - size.Height),
                new(area.Right - size.Width, area.Bottom - size.Height)];
            foreach (var candidate in candidates)
            {
                var placed = new Rectangle(Math.Clamp(candidate.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
                    Math.Clamp(candidate.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)), size.Width, size.Height);
                var protectedWidget = widget; protectedWidget.Inflate(gap, gap);
                if (area.Contains(placed) && !placed.IntersectsWith(protectedWidget)) return placed.Location;
                var intersection = Rectangle.Intersect(widget, placed);
                long overlap = (long)Math.Max(0, intersection.Width) * Math.Max(0, intersection.Height);
                if (overlap < leastOverlap) { fallback = placed; leastOverlap = overlap; }
            }
        }
        return fallback?.Location ?? widget.Location;
    }

    // All coordinates are physical screen pixels, including negative monitor origins.
    public static Rectangle Snap(Rectangle bounds, Rectangle workingArea, int distance)
    {
        int right = Math.Max(workingArea.Left, workingArea.Right - bounds.Width);
        int bottom = Math.Max(workingArea.Top, workingArea.Bottom - bounds.Height);
        int x = Math.Clamp(bounds.X, workingArea.Left, right);
        int y = Math.Clamp(bounds.Y, workingArea.Top, bottom);
        if (x - workingArea.Left <= distance) x = workingArea.Left;
        else if (right - x <= distance) x = right;
        if (y - workingArea.Top <= distance) y = workingArea.Top;
        else if (bottom - y <= distance) y = bottom;
        return new Rectangle(x, y, bounds.Width, bounds.Height);
    }
}

public sealed class WindowDrag
{
    private Point? grabOffset;

    public void Begin(Point pointer, Rectangle bounds) => grabOffset = new(pointer.X - bounds.Left, pointer.Y - bounds.Top);
    public void End() => grabOffset = null;

    public Rectangle BoundsAt(Point pointer, Rectangle proposed) => grabOffset is Point offset
        ? new Rectangle(pointer.X - offset.X, pointer.Y - offset.Y, proposed.Width, proposed.Height)
        : proposed;
}
