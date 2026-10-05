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
        var report = Argument(args, "--smoke-report");
        if (captureSettings is not null || captureGraphSettings is not null || captureRateSettings is not null)
        {
            var captureConfiguration = new Settings();
            captureConfiguration.Rates.Compact = args.Contains("--compact");
            using var dialog = new SettingsForm(captureConfiguration, SystemIcons.Application);
            if (captureGraphSettings is not null) dialog.SelectGraphTab();
            if (captureRateSettings is not null) dialog.SelectRatesTab();
            var settingsCapturePath = captureRateSettings ?? captureGraphSettings ?? captureSettings!;
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
        var settings = diagnostic ? new Settings { AlwaysOnTop = false } : Settings.Load();
        if (diagnostic) settings.Rates.Compact = args.Contains("--compact");
        using var window = new Widget(settings, diagnostic);
        if (diagnostic)
        {
            var process = Process.GetCurrentProcess();
            var startCpu = process.TotalProcessorTime;
            var watch = Stopwatch.StartNew();
            using var warmup = new System.Windows.Forms.Timer { Interval = 2000 };
            warmup.Tick += (_, _) => { warmup.Stop(); startCpu = process.TotalProcessorTime; watch.Restart(); };
            using var finish = new System.Windows.Forms.Timer { Interval = report is not null ? 32000 : 8000 };
            finish.Tick += (_, _) =>
            {
                finish.Stop();
                try
                {
                    if (capture is not null) window.CapturePreview(capture, false);
                    if (captureHover is not null) window.CapturePreview(captureHover, true);
                    if (report is not null)
                    {
                        process.Refresh();
                        File.WriteAllText(report, JsonSerializer.Serialize(new
                        {
                            Adapter = window.MonitorStatus, window.SampleCount,
                            window.TotalDown, window.TotalUp,
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
