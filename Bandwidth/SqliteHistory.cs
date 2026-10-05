using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiniDebit;

public sealed class HistorySettings
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 7;
    public void Normalize() => RetentionDays = Math.Clamp(RetentionDays, 1, 90);
    public static string DatabasePath => Path.Combine(Path.GetDirectoryName(Settings.FilePath)!, "traffic.sqlite");
}

public sealed record HistoryRow(Sample Sample, string AdapterId, string AdapterName, int IntervalMs);

// UI writes only to a bounded queue. All database calls belong to a single worker.
public sealed class HistoryRecorder : IDisposable
{
    private readonly string path;
    private readonly string session = Guid.NewGuid().ToString("N");
    private readonly int flushMilliseconds;
    private readonly BlockingCollection<HistoryRow> pending = new(256);
    private Task? worker;
    private bool enabled;
    private int retentionDays = 7;
    private string? error;
    private long stored;
    private long dropped;
    private bool stopped;
    public string? Error => Volatile.Read(ref error);
    public long Stored => Interlocked.Read(ref stored);
    public long Dropped => Interlocked.Read(ref dropped);
    public int Pending => pending.Count;
    public string Status => Error is not null ? $"Enregistrement arrêté : {Error}"
        : !enabled ? "Historique désactivé" : $"{Stored:N0} mesures enregistrées • {Dropped:N0} perdues";

    public HistoryRecorder(string? path = null, int flushMilliseconds = 30000)
    { this.path = path ?? HistorySettings.DatabasePath; this.flushMilliseconds = flushMilliseconds; }

    public void Configure(HistorySettings settings)
    {
        enabled = settings.Enabled;
        Volatile.Write(ref retentionDays, settings.RetentionDays);
        if (enabled && worker is null)
            worker = Task.Factory.StartNew(WriteLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void Record(Sample sample, string adapterId, string adapterName, int intervalMs)
    {
        if (!enabled || stopped || sample.ElapsedSeconds <= 0) return;
        if (Error is not null) { Interlocked.Increment(ref dropped); return; }
        try
        {
            if (!pending.TryAdd(new HistoryRow(sample, adapterId, adapterName, intervalMs))) Interlocked.Increment(ref dropped);
        }
        catch (InvalidOperationException) { Interlocked.Increment(ref dropped); }
    }

    private void WriteLoop()
    {
        var batch = new List<HistoryRow>(128);
        try
        {
            using var database = new TrafficDatabase(path);
            var lastFlush = Stopwatch.StartNew();
            var lastPrune = DateTime.MinValue;
            int previousRetention = 0;
            while (!pending.IsCompleted)
            {
                int wait = batch.Count == 0 ? Timeout.Infinite : Math.Max(1, flushMilliseconds - (int)lastFlush.ElapsedMilliseconds);
                if (pending.TryTake(out var row, wait))
                {
                    if (batch.Count == 0) lastFlush.Restart();
                    batch.Add(row);
                }
                if (batch.Count >= 128 || (batch.Count > 0 && lastFlush.ElapsedMilliseconds >= flushMilliseconds)) Flush();
            }
            Flush();

            void Flush()
            {
                if (batch.Count == 0) return;
                int days = Volatile.Read(ref retentionDays);
                if (lastPrune != DateTime.UtcNow.Date || days != previousRetention)
                {
                    database.Prune(DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds());
                    lastPrune = DateTime.UtcNow.Date; previousRetention = days;
                }
                database.Insert(batch, session);
                Interlocked.Add(ref stored, batch.Count);
                batch.Clear(); lastFlush.Restart();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Volatile.Write(ref error, ex.Message);
            pending.CompleteAdding();
            Interlocked.Add(ref dropped, batch.Count);
            while (pending.TryTake(out _)) Interlocked.Increment(ref dropped);
        }
    }

    public void Dispose()
    {
        if (stopped) return;
        stopped = true; pending.CompleteAdding();
        // Only shutdown waits for the final partial batch; never the measurement timer.
        if (worker is null || worker.Wait(TimeSpan.FromSeconds(2))) pending.Dispose();
    }
}

public sealed class TrafficDatabase : IDisposable
{
    private nint handle;
    private nint insert;
    public TrafficDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int result = Native.Open(path, out handle, 2 | 4 | 0x8000, null); // READWRITE | CREATE | NOMUTEX: owned by one thread
        try
        {
            Check(result);
            Native.BusyTimeout(handle, 500);
            Execute("PRAGMA journal_mode=TRUNCATE; PRAGMA synchronous=FULL; PRAGMA cache_size=-256; PRAGMA mmap_size=0; PRAGMA journal_size_limit=0; PRAGMA temp_store=FILE;");
            Execute("CREATE TABLE IF NOT EXISTS measurements (id INTEGER PRIMARY KEY, timestamp_utc_ms INTEGER NOT NULL, session_id TEXT NOT NULL, adapter_id TEXT NOT NULL, adapter_name TEXT NOT NULL, interval_ms INTEGER NOT NULL, elapsed_seconds REAL NOT NULL, received_bytes REAL NOT NULL, sent_bytes REAL NOT NULL, down_bytes_per_second REAL NOT NULL, up_bytes_per_second REAL NOT NULL); CREATE INDEX IF NOT EXISTS measurements_time ON measurements(timestamp_utc_ms); PRAGMA user_version=1;");
            Check(Native.Prepare(handle, "INSERT INTO measurements(timestamp_utc_ms,session_id,adapter_id,adapter_name,interval_ms,elapsed_seconds,received_bytes,sent_bytes,down_bytes_per_second,up_bytes_per_second) VALUES (?,?,?,?,?,?,?,?,?,?)", -1, out insert, 0));
        }
        catch { Dispose(); throw; }
    }

    public void Insert(IReadOnlyList<HistoryRow> rows, string session)
    {
        Execute("BEGIN IMMEDIATE;");
        try
        {
            foreach (var row in rows)
            {
                var sample = row.Sample;
                Check(Native.BindInt64(insert, 1, sample.Time.ToUnixTimeMilliseconds()));
                BindText(2, session); BindText(3, row.AdapterId); BindText(4, row.AdapterName);
                Check(Native.BindInt64(insert, 5, row.IntervalMs));
                Check(Native.BindDouble(insert, 6, sample.ElapsedSeconds));
                Check(Native.BindDouble(insert, 7, sample.ReceivedBytes)); Check(Native.BindDouble(insert, 8, sample.SentBytes));
                Check(Native.BindDouble(insert, 9, sample.Down)); Check(Native.BindDouble(insert, 10, sample.Up));
                int result = Native.Step(insert);
                Native.Reset(insert); Native.ClearBindings(insert);
                if (result != 101) Check(result);
            }
            Execute("COMMIT;");
        }
        catch { try { Execute("ROLLBACK;"); } catch (InvalidOperationException) { } throw; }
    }

    public void Prune(long cutoff)
    {
        Check(Native.Prepare(handle, "DELETE FROM measurements WHERE timestamp_utc_ms < ?", -1, out var statement, 0));
        try { Check(Native.BindInt64(statement, 1, cutoff)); int result = Native.Step(statement); if (result != 101) Check(result); }
        finally { Native.Finalize(statement); }
    }

    public double Scalar(string sql)
    {
        Check(Native.Prepare(handle, sql, -1, out var statement, 0));
        try { int result = Native.Step(statement); if (result == 100) return Native.ColumnDouble(statement, 0); if (result != 101) Check(result); return 0; }
        finally { Native.Finalize(statement); }
    }

    public string ScalarText(string sql)
    {
        Check(Native.Prepare(handle, sql, -1, out var statement, 0));
        try { int result = Native.Step(statement); if (result == 100) return Marshal.PtrToStringUTF8(Native.ColumnText(statement, 0)) ?? ""; if (result != 101) Check(result); return ""; }
        finally { Native.Finalize(statement); }
    }

    private void Execute(string sql) => Check(Native.Exec(handle, sql, 0, 0, 0));
    private void BindText(int index, string value) => Check(Native.BindText(insert, index, value, -1, new nint(-1)));
    private void Check(int result)
    {
        if (result != 0) throw new InvalidOperationException($"SQLite ({result}) : {Marshal.PtrToStringUTF8(Native.Error(handle))}");
    }
    public void Dispose()
    {
        if (insert != 0) { Native.Finalize(insert); insert = 0; }
        if (handle != 0) { Native.Close(handle); handle = 0; }
    }

    private static class Native
    {
        private const string Library = "winsqlite3.dll";
        [DllImport(Library, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)] internal static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out nint db, int flags, [MarshalAs(UnmanagedType.LPUTF8Str)] string? vfs);
        [DllImport(Library, EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl)] internal static extern int Close(nint db);
        [DllImport(Library, EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)] internal static extern int BusyTimeout(nint db, int ms);
        [DllImport(Library, EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)] internal static extern int Exec(nint db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, nint callback, nint context, nint error);
        [DllImport(Library, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)] internal static extern int Prepare(nint db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out nint statement, nint tail);
        [DllImport(Library, EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)] internal static extern int BindInt64(nint statement, int index, long value);
        [DllImport(Library, EntryPoint = "sqlite3_bind_double", CallingConvention = CallingConvention.Cdecl)] internal static extern int BindDouble(nint statement, int index, double value);
        [DllImport(Library, EntryPoint = "sqlite3_bind_text", CallingConvention = CallingConvention.Cdecl)] internal static extern int BindText(nint statement, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int length, nint destructor);
        [DllImport(Library, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)] internal static extern int Step(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_reset", CallingConvention = CallingConvention.Cdecl)] internal static extern int Reset(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_clear_bindings", CallingConvention = CallingConvention.Cdecl)] internal static extern int ClearBindings(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)] internal static extern int Finalize(nint statement);
        [DllImport(Library, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)] internal static extern nint Error(nint db);
        [DllImport(Library, EntryPoint = "sqlite3_column_double", CallingConvention = CallingConvention.Cdecl)] internal static extern double ColumnDouble(nint statement, int index);
        [DllImport(Library, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)] internal static extern nint ColumnText(nint statement, int index);
    }
}
