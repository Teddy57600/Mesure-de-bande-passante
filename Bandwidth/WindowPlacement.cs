namespace MiniDebit;

public static class WindowPlacement
{
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
