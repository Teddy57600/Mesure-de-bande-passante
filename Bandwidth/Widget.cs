using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace MiniDebit;

public sealed class Widget : Form
{
    private readonly Settings settings;
    private readonly bool diagnostic;
    private readonly NetworkMonitor monitor = new();
    private readonly HistoryRecorder recorder;
    private readonly Queue<Sample> history = new();
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private readonly ToolTip tooltip = new();
    private readonly Font titleFont = new("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font smallFont = new("Segoe UI", 10, FontStyle.Regular, GraphicsUnit.Pixel);
    private Font rateFont = new("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
    private Font labelFont = new("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font arrowFont = new("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
    private Icon? ownedIcon;
    private Sample current;
    private int hover = -1;
    private bool pointerInside;
    private int tickCount;
    private bool resizing;
    private Point resizeOrigin;
    private Size resizeSize;
    private double scale = 125000;
    private TrafficPeak visiblePeak;
    public string MonitorStatus => monitor.Status;
    public int SampleCount { get; private set; }
    public double TotalDown => monitor.TotalDown;
    public double TotalUp => monitor.TotalUp;
    public string StorageStatus => recorder.Status;
    public long StoredSamples => recorder.Stored;
    public long DroppedSamples => recorder.Dropped;
    private float DpiScale => DeviceDpi / 96f;
    private float LogicalWidth => ClientSize.Width / DpiScale;
    private float LogicalHeight => ClientSize.Height / DpiScale;
    private bool HeaderVisible => pointerInside || resizing;
    private Color Background => settings.Dark ? Color.FromArgb(20, 26, 36) : Color.FromArgb(246, 249, 253);
    private Color Foreground => settings.Dark ? Color.FromArgb(233, 240, 250) : Color.FromArgb(27, 42, 61);
    private Color Muted => settings.Dark ? Color.FromArgb(145, 161, 184) : Color.FromArgb(94, 115, 141);
    private Color Grid => settings.Dark ? Color.FromArgb(42, 53, 70) : Color.FromArgb(215, 226, 239);
    private Color DownColor => settings.Graph.Reception;
    private Color UpColor => settings.Graph.Sending;

    public Widget(Settings settings, bool diagnostic = false, string? historyPath = null)
    {
        this.settings = settings;
        this.diagnostic = diagnostic;
        recorder = new HistoryRecorder(historyPath);
        Text = "MiniDébit";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(settings.Width, settings.Height);
        MinimumSize = new Size(270, 145);
        MaximumSize = new Size(1000, 600);
        ownedIcon = MakeIcon();
        Icon = ownedIcon;
        menu.Items.Add("Afficher / masquer", null, (_, _) => ToggleVisibility());
        menu.Items.Add("Paramètres…", null, (_, _) => OpenSettings());
        menu.Items.Add("Toujours au premier plan", null, (_, _) =>
        { settings.AlwaysOnTop = !settings.AlwaysOnTop; ApplySettings(); SaveSettings(); });
        menu.Items.Add("Remettre les compteurs à zéro", null, (_, _) => ResetMeasurements());
        menu.Items.Add("Exporter le graphique en CSV…", null, (_, _) => ExportCsv());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) => Close());
        menu.Opening += (_, _) => ((ToolStripMenuItem)menu.Items[2]).Checked = settings.AlwaysOnTop;
        ContextMenuStrip = menu;
        tray = new NotifyIcon { Icon = Icon, Text = "MiniDébit", ContextMenuStrip = menu, Visible = !diagnostic };
        tray.DoubleClick += (_, _) => { Show(); Activate(); };
        tooltip.SetToolTip(this, "Glisser pour déplacer • coin inférieur droit pour redimensionner\nClic droit : paramètres • double-clic : paramètres");
        ApplySettings();
        timer.Tick += (_, _) => Tick();
        Shown += (_, _) =>
        {
            PlaceWindow();
            Tick();
            timer.Start();
            if (settings.StartHidden && !diagnostic) Hide();
        };
        MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenSettings(); };
    }

    private void PlaceWindow()
    {
        var area = Screen.PrimaryScreen!.WorkingArea;
        var point = settings.X.HasValue && settings.Y.HasValue
            ? new Point(settings.X.Value, settings.Y.Value)
            : new Point(area.Right - Width - 24, area.Bottom - Height - 24);
        area = Screen.FromPoint(point).WorkingArea;
        Location = new Point(Math.Clamp(point.X, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(point.Y, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }

    private void ApplySettings()
    {
        settings.Normalize();
        recorder.Configure(settings.Storage);
        var numbers = settings.Rates.CreateFont();
        var captions = settings.Rates.CreateFont(true);
        rateFont.Dispose(); labelFont.Dispose();
        rateFont = numbers; labelFont = captions;
        TopMost = settings.AlwaysOnTop;
        Opacity = settings.OpacityPercent / 100.0;
        timer.Interval = settings.IntervalMs;
        BackColor = Background;
        Invalidate();
    }

    private void Tick()
    {
        current = monitor.Poll(settings.AdapterId);
        recorder.Record(current, monitor.ActiveAdapterKey, monitor.Status, settings.IntervalMs);
        SampleCount++;
        history.Enqueue(current);
        int maxSamples = (int)Math.Ceiling(settings.HistorySeconds * 1000.0 / settings.IntervalMs) + 2;
        while (history.Count > maxSamples || (history.Count > 0 && (current.Time - history.Peek().Time).TotalSeconds > settings.HistorySeconds))
            history.Dequeue();
        visiblePeak = TrafficPeak.InWindow(history, current.Time, settings.HistorySeconds);
        if (settings.FixedScaleMbps > 0) scale = settings.FixedScaleMbps * 125000;
        else
        {
            scale = NiceCeiling(Math.Max(1000, visiblePeak.Rate) * 1.12);
        }
        if (++tickCount % 2 == 0)
        {
            var text = $"MiniDébit · ↓ {Units.Rate(current.Down, settings.Bits)} · ↑ {Units.Rate(current.Up, settings.Bits)}";
            tray.Text = text.Length > 63 ? text[..63] : text;
            tooltip.SetToolTip(this, $"{monitor.Status}\nSession : ↓ {Units.Volume(monitor.TotalDown)}   ↑ {Units.Volume(monitor.TotalUp)}\nÉchelle : {Units.Rate(scale, settings.Bits)} • Période : {Units.Duration(settings.HistorySeconds)}\nClic droit ou double-clic : paramètres");
        }
        if (Visible) Invalidate();
    }

    internal static double NiceCeiling(double value)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(1, value))));
        var ratio = value / power;
        return (ratio <= 1 ? 1 : ratio <= 2 ? 2 : ratio <= 5 ? 5 : 10) * power;
    }

    internal void CapturePreview(string path, bool hovered)
    {
        bool previous = pointerInside;
        try
        {
            if (hovered) OnMouseEnter(EventArgs.Empty); else OnMouseLeave(EventArgs.Empty);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
            DrawToBitmap(bitmap, ClientRectangle);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        finally
        {
            if (previous) OnMouseEnter(EventArgs.Empty); else OnMouseLeave(EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.ScaleTransform(DpiScale, DpiScale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float w = LogicalWidth, h = LogicalHeight;
        using var back = new SolidBrush(Background);
        g.FillRectangle(back, 0, 0, w, h);
        using var border = new Pen(Grid);
        g.DrawRectangle(border, .5f, .5f, w - 1, h - 1);
        if (HeaderVisible)
        {
            using var accent = new SolidBrush(DownColor);
            g.FillEllipse(accent, 13, 14, 6, 6);
            DrawText(g, "MINIDÉBIT", titleFont, Foreground, new RectangleF(25, 8, 105, 20));
            DrawText(g, monitor.Status, smallFont, Muted, new RectangleF(13, 30, w - 26, 16));
            DrawButton(g, 0, "⚙", w - 81);
            DrawButton(g, 1, "−", w - 55);
            DrawButton(g, 2, "×", w - 29);
        }
        float margin = settings.Graph.MarginPixels + 1; // Keep the window border even at zero margin.
        float rateHeight = RateRenderer.Measure(rateFont, labelFont, arrowFont, settings.Rates.Compact).Height;
        float headerHeight = HeaderVisible ? 62 : 0;
        // Preserve room for the graph in small widgets with large fonts and margins.
        float verticalMargin = Math.Min(settings.Graph.MarginPixels, Math.Max(0, (h - headerHeight - rateHeight - 18) / 2));
        float chartTop = headerHeight + 1 + verticalMargin;
        var chart = new RectangleF(margin, chartTop, w - 2 * margin, Math.Max(1, h - chartTop - rateHeight - 1 - verticalMargin));
        if (HeaderVisible)
        {
            DrawText(g, Units.Rate(scale, settings.Bits), smallFont, Muted, new RectangleF(13, 47, 160, 14));
            DrawText(g, Units.Duration(settings.HistorySeconds), smallFont, Muted, new RectangleF(w - 84, 47, 71, 14), StringAlignment.Far);
        }
        GraphRenderer.Backdrop(g, chart, settings.Graph, settings.Dark);
        DrawSeries(g, chart, false, UpColor);
        DrawSeries(g, chart, true, DownColor);
        GraphRenderer.Maximum(g, chart, settings.Graph, settings.Dark, visiblePeak, scale, settings.Bits);
        RateRenderer.Draw(g, new RectangleF(1, h - rateHeight - 1, w - 2, rateHeight), Units.Rate(current.Down, settings.Bits),
            Units.Rate(current.Up, settings.Bits), settings.Rates, settings.Graph, settings.Dark, rateFont, labelFont, arrowFont);
        using var grip = new Pen(Muted, 1);
        g.DrawLine(grip, w - 8, h - 4, w - 4, h - 8);
        g.DrawLine(grip, w - 12, h - 4, w - 4, h - 12);
    }

    private static void DrawText(Graphics g, string value, Font font, Color color, RectangleF bounds, StringAlignment align = StringAlignment.Near)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(value, font, brush, bounds, format);
    }

    private void DrawButton(Graphics g, int index, string glyph, float x)
    {
        if (hover == index)
        {
            using var brush = new SolidBrush(index == 2 ? Color.FromArgb(175, 58, 74) : Grid);
            g.FillRectangle(brush, x, 7, 24, 22);
        }
        DrawText(g, glyph, titleFont, Muted, new RectangleF(x, 6, 24, 22), StringAlignment.Center);
    }

    private void DrawSeries(Graphics g, RectangleF chart, bool download, Color color)
    {
        if (history.Count < 2) return;
        var points = new PointF[history.Count];
        int i = 0;
        foreach (var sample in history)
        {
            float x = chart.Right - (float)((current.Time - sample.Time).TotalSeconds / settings.HistorySeconds * chart.Width);
            double rate = download ? sample.Down : sample.Up;
            float y = chart.Bottom - (float)Math.Clamp(rate / scale, 0, 1) * chart.Height;
            points[i++] = new PointF(Math.Clamp(x, chart.Left, chart.Right), y);
        }
        GraphRenderer.Curve(g, chart, points, color, settings.Graph);
    }

    private int ButtonAt(Point point)
    {
        if (!HeaderVisible) return -1;
        var x = point.X / DpiScale;
        var y = point.Y / DpiScale;
        if (y < 6 || y > 30) return -1;
        for (int i = 0; i < 3; i++)
        {
            var left = LogicalWidth - 81 + i * 26;
            if (x >= left && x <= left + 24) return i;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!pointerInside) { pointerInside = true; Invalidate(); }
        if (resizing)
        {
            var position = MousePosition;
            Size = new Size(Math.Clamp(resizeSize.Width + position.X - resizeOrigin.X, MinimumSize.Width, MaximumSize.Width),
                Math.Clamp(resizeSize.Height + position.Y - resizeOrigin.Y, MinimumSize.Height, MaximumSize.Height));
            return;
        }
        var next = ButtonAt(e.Location);
        if (next != hover) { hover = next; Invalidate(); }
        Cursor = e.X > Width - 15 * DpiScale && e.Y > Height - 15 * DpiScale ? Cursors.SizeNWSE : next >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        pointerInside = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        pointerInside = false;
        hover = -1;
        Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        pointerInside = Visible && ClientRectangle.Contains(PointToClient(MousePosition));
        hover = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        int button = ButtonAt(e.Location);
        if (button == 0) OpenSettings();
        else if (button == 1) Hide();
        else if (button == 2) Close();
        else if (e.Clicks == 1)
        {
            if (e.X > Width - 15 * DpiScale && e.Y > Height - 15 * DpiScale)
            {
                resizing = true; resizeOrigin = MousePosition; resizeSize = Size; Capture = true;
                return;
            }
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 2, 0); // WM_NCLBUTTONDOWN / HTCAPTION
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!resizing) return;
        resizing = false; Capture = false;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            resizing = false;
            pointerInside = ClientRectangle.Contains(PointToClient(MousePosition));
            Invalidate();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Oemcomma)) { OpenSettings(); return true; }
        if (keyData == Keys.Escape) { Hide(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ToggleVisibility() { if (Visible) Hide(); else { Show(); Activate(); } }
    private void ResetMeasurements() { monitor.Reset(); history.Clear(); current = default; Tick(); }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings, ownedIcon!, () => recorder.Status);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (dialog.MeasurementChanged) ResetMeasurements();
        ApplySettings();
        SaveSettings();
    }

    private void SaveSettings()
    {
        if (diagnostic) return;
        settings.X = Left;
        settings.Y = Top;
        settings.Width = (int)Math.Round(LogicalWidth);
        settings.Height = (int)Math.Round(LogicalHeight);
        try { settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, $"Impossible d’enregistrer les paramètres :\n{ex.Message}", "MiniDébit", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ExportCsv()
    {
        using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"MiniDebit-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var csv = new StringBuilder("horodatage;reception_octets_par_seconde;envoi_octets_par_seconde\r\n");
        foreach (var sample in history)
            csv.AppendLine($"{sample.Time:O};{sample.Down.ToString("F3", CultureInfo.InvariantCulture)};{sample.Up.ToString("F3", CultureInfo.InvariantCulture)}");
        try { File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, ex.Message, "Export impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
        recorder.Dispose();
        SaveSettings();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose(); tray.Dispose(); menu.Dispose(); tooltip.Dispose();
            recorder.Dispose();
            titleFont.Dispose(); smallFont.Dispose(); rateFont.Dispose(); labelFont.Dispose(); arrowFont.Dispose(); ownedIcon?.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Icon MakeIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(20, 26, 36));
            using var down = new Pen(GraphAppearance.Resolve(GraphAppearance.DefaultDown, Color.Cyan), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var up = new Pen(GraphAppearance.Resolve(GraphAppearance.DefaultUp, Color.Orange), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(down, [new(5, 19), new(10, 25), new(15, 19)]);
            g.DrawLine(down, 10, 7, 10, 24);
            g.DrawLines(up, [new(18, 13), new(23, 7), new(28, 13)]);
            g.DrawLine(up, 23, 8, 23, 25);
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint handle);
}
