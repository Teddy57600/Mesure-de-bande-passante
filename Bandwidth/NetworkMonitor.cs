using System.Diagnostics;
using System.Net.NetworkInformation;

namespace MiniDebit;

public sealed record AdapterChoice(string Id, string Name)
{
    public override string ToString() => Name;
}
public readonly record struct Counters(string Id, long Received, long Sent);
public readonly record struct Sample(DateTimeOffset Time, double Down, double Up);
public readonly record struct TrafficPeak(double Rate, bool Reception)
{
    public static TrafficPeak InWindow(IEnumerable<Sample> samples, DateTimeOffset now, int seconds)
    {
        var start = now.AddSeconds(-seconds);
        var peak = new TrafficPeak(0, true);
        foreach (var sample in samples)
        {
            if (sample.Time < start || sample.Time > now) continue;
            if (sample.Down > peak.Rate) peak = new(sample.Down, true);
            if (sample.Up > peak.Rate) peak = new(sample.Up, false);
        }
        return peak;
    }
}

// Compute differences per adapter: adding/removing an interface never creates a false spike.
public sealed class RateAccumulator
{
    private Dictionary<string, Counters> previous = new();
    public double TotalDown { get; private set; }
    public double TotalUp { get; private set; }

    public Sample Update(IEnumerable<Counters> readings, double seconds, DateTimeOffset now)
    {
        var current = readings.ToDictionary(c => c.Id);
        double received = 0, sent = 0;
        foreach (var (id, counter) in current)
        {
            if (!previous.TryGetValue(id, out var old)) continue;
            // A reset or interface restart establishes a new baseline for that counter.
            if (counter.Received >= old.Received) received += counter.Received - old.Received;
            if (counter.Sent >= old.Sent) sent += counter.Sent - old.Sent;
        }
        previous = current;
        if (!(seconds > 0) || !double.IsFinite(seconds)) return new(now, 0, 0);
        TotalDown += received;
        TotalUp += sent;
        return new(now, received / seconds, sent / seconds);
    }

    public void Reset()
    {
        previous.Clear();
        TotalDown = TotalUp = 0;
    }
}

public sealed class NetworkMonitor
{
    private NetworkInterface[] adapters = [];
    private long lastRefresh;
    private long lastSample;
    private string selection = "auto";
    private readonly RateAccumulator accumulator = new();
    public string Status { get; private set; } = "Initialisation…";
    public double TotalDown => accumulator.TotalDown;
    public double TotalUp => accumulator.TotalUp;

    public static AdapterChoice[] Choices() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .Select(n => new AdapterChoice(n.Id, $"{n.Name} — {n.Description}"))
        .OrderBy(n => n.Name).ToArray();

    public void Reset() { accumulator.Reset(); lastSample = 0; lastRefresh = 0; }

    public Sample Poll(string adapterId)
    {
        var now = Stopwatch.GetTimestamp();
        if (selection != adapterId) { Reset(); selection = adapterId; }
        try
        {
            if (lastRefresh == 0 || Stopwatch.GetElapsedTime(lastRefresh, now).TotalSeconds >= 5)
            {
                adapters = NetworkInterface.GetAllNetworkInterfaces();
                lastRefresh = now;
            }
            var active = adapters.Where(n => n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
            NetworkInterface[] selected;
            if (adapterId == "all") selected = active;
            else if (adapterId != "auto") selected = active.Where(n => n.Id == adapterId).ToArray();
            else
            {
                // Keep auto selection stable and favor a physical interface with a default gateway.
                var primary = active.OrderByDescending(n => n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                    .ThenByDescending(n => n.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any) && !g.Address.Equals(System.Net.IPAddress.IPv6Any)))
                    .ThenBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
                selected = primary is null ? [] : [primary];
            }
            var counters = new List<Counters>(selected.Length);
            foreach (var adapter in selected)
            {
                try
                {
                    var stats = adapter.GetIPStatistics();
                    counters.Add(new(adapter.Id, stats.BytesReceived, stats.BytesSent));
                }
                catch (NetworkInformationException) { }
            }
            Status = counters.Count == 0 ? "Aucune interface active" : adapterId == "all" ? $"{counters.Count} interfaces • cumul" : selected[0].Name;
            var elapsed = lastSample == 0 ? 0 : Stopwatch.GetElapsedTime(lastSample, now).TotalSeconds;
            lastSample = now;
            return accumulator.Update(counters, elapsed, DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is NetworkInformationException or PlatformNotSupportedException)
        {
            Status = "Mesure indisponible";
            lastRefresh = 0;
            lastSample = now;
            return accumulator.Update([], 0, DateTimeOffset.Now);
        }
    }
}
