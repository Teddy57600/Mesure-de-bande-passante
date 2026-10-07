using System.Diagnostics;
using System.Text.Json;

namespace MiniDebit;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var capture = Argument(args, "--capture");
        var captureHover = Argument(args, "--capture-hover");
        var captureSettings = Argument(args, "--capture-settings");
        var captureGraphSettings = Argument(args, "--capture-graph-settings");
        var captureRateSettings = Argument(args, "--capture-rate-settings");
        var captureHistorySettings = Argument(args, "--capture-history-settings");
        var captureFrameSettings = Argument(args, "--capture-frame-settings");
        var captureMaximumSettings = Argument(args, "--capture-max-settings");
        var diagnosticSettings = Argument(args, "--diagnostic-settings");
        var historyPath = Argument(args, "--history-db");
        var report = Argument(args, "--smoke-report");
        if (captureSettings is not null || captureGraphSettings is not null || captureRateSettings is not null || captureHistorySettings is not null || captureFrameSettings is not null || captureMaximumSettings is not null)
        {
            var captureConfiguration = diagnosticSettings is null ? new Settings() : Settings.Load(diagnosticSettings);
            captureConfiguration.Rates.Compact = args.Contains("--compact");
            using var dialog = new SettingsForm(captureConfiguration, SystemIcons.Application);
            if (captureGraphSettings is not null) dialog.SelectGraphTab();
            if (captureRateSettings is not null) dialog.SelectRatesTab();
            if (captureHistorySettings is not null) dialog.SelectHistoryTab();
            if (captureFrameSettings is not null) dialog.SelectFrameTab();
            if (captureMaximumSettings is not null) dialog.SelectMaximumTextTab();
            var settingsCapturePath = captureMaximumSettings ?? captureFrameSettings ?? captureHistorySettings ?? captureRateSettings ?? captureGraphSettings ?? captureSettings!;
            using var close = new System.Windows.Forms.Timer { Interval = 1000 };
            close.Tick += (_, _) =>
            {
                close.Stop();
                Directory.CreateDirectory(Path.GetDirectoryName(settingsCapturePath)!);
                using var bitmap = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height));
                bitmap.Save(settingsCapturePath, System.Drawing.Imaging.ImageFormat.Png);
                dialog.Close();
            };
            close.Start();
            Application.Run(dialog);
            return 0;
        }
        using var mutex = new Mutex(true, @"Local\MiniDebit.Widget", out var first);
        if (!first && capture is null && captureHover is null && report is null)
        {
            MessageBox.Show("MiniDébit est déjà ouvert. Retrouvez son icône près de l’horloge.", "MiniDébit");
            return 0;
        }
        bool diagnostic = capture is not null || captureHover is not null || report is not null;
        var settings = diagnostic ? diagnosticSettings is null ? new Settings() : Settings.Load(diagnosticSettings) : Settings.Load();
        if (diagnostic)
        {
            settings.AlwaysOnTop = false;
            settings.Rates.Compact = args.Contains("--compact");
            settings.Storage.Enabled = historyPath is not null;
        }
        using var window = new Widget(settings, diagnostic, diagnostic ? historyPath : null);
        if (diagnostic)
        {
            var process = Process.GetCurrentProcess();
            var startCpu = process.TotalProcessorTime;
            var watch = Stopwatch.StartNew();
            using var warmup = new System.Windows.Forms.Timer { Interval = 2000 };
            warmup.Tick += (_, _) => { warmup.Stop(); startCpu = process.TotalProcessorTime; watch.Restart(); };
            int duration = args.Contains("--long-benchmark") ? 72000 : report is not null ? 32000 : 8000;
            using var finish = new System.Windows.Forms.Timer { Interval = duration };
            finish.Tick += (_, _) =>
            {
                finish.Stop();
                try
                {
                    if (args.Contains("--demo-chart")) window.SeedPreview();
                    if (capture is not null) window.CapturePreview(capture, false);
                    if (captureHover is not null) window.CapturePreview(captureHover, true);
                    if (report is not null)
                    {
                        process.Refresh();
                        File.WriteAllText(report, JsonSerializer.Serialize(new
                        {
                            Adapter = window.MonitorStatus, window.ExternalIpv4, window.ExternalIpv4Error, window.SampleCount,
                            window.TotalDown, window.TotalUp,
                            window.StoredSamples, window.DroppedSamples, window.StorageStatus,
                            WorkingSetMiB = process.WorkingSet64 / 1048576.0,
                            PrivateMiB = process.PrivateMemorySize64 / 1048576.0,
                            CpuPercentOneCore = (process.TotalProcessorTime - startCpu).TotalSeconds / watch.Elapsed.TotalSeconds * 100,
                            ObservationSecondsAfterWarmup = watch.Elapsed.TotalSeconds
                        }, new JsonSerializerOptions { WriteIndented = true }));
                    }
                }
                catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "capture-error.txt"), ex.ToString()); Environment.ExitCode = 1; }
                window.Close();
            };
            warmup.Start();
            finish.Start();
            Application.Run(window);
            return Environment.ExitCode;
        }
        Application.Run(window);
        return 0;
    }

    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : null;
    }
}
