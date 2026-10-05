using System.Net;
using System.Net.Sockets;

namespace MiniDebit;

public sealed record PublicIpv4Target(string AdapterId, IPAddress Address, int InterfaceIndex);

public sealed class PublicIpv4 : IDisposable
{
    private readonly Func<PublicIpv4Target, CancellationToken, Task<string?>> lookup;
    private PublicIpv4Target? target;
    private Task<(string? Address, string? Error)>? pending;
    private CancellationTokenSource? cancellation;
    private long nextAttempt;
    public string? Address { get; private set; }
    public string? LastError { get; private set; }
    public PublicIpv4(Func<PublicIpv4Target, CancellationToken, Task<string?>>? lookup = null) => this.lookup = lookup ?? LookupAsync;

    // The UI timer only consumes completed results; all network operations run in the background.
    public void Update(PublicIpv4Target? selected, bool enabled)
    {
        if (!enabled) selected = null;
        if (selected != target) { Cancel(); target = selected; Address = null; LastError = null; nextAttempt = 0; }
        if (target is null) return;
        long now = Environment.TickCount64;
        if (pending?.IsCompleted == true)
        {
            (Address, LastError) = pending.GetAwaiter().GetResult();
            pending = null; cancellation?.Dispose(); cancellation = null;
            nextAttempt = now + (Address is null ? 60_000 : 600_000);
        }
        if (pending is not null || now < nextAttempt) return;
        var current = target;
        cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var token = cancellation.Token;
        pending = Task.Run(async () =>
        {
            try { var result = Parse(await lookup(current, token)); return (Address: result, Error: result is null ? "Réponse IPv4 invalide" : null); }
            catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or OperationCanceledException)
            { return (Address: (string?)null, Error: (string?)ex.Message); }
        });
    }

    public static string? Parse(string? text)
    {
        var value = text?.Trim();
        if (value is null || value.Length > 15 || value.Split('.').Length != 4 ||
            !IPAddress.TryParse(value, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return null;
        var b = ip.GetAddressBytes();
        if (b[0] is 0 or 10 or 127 || b[0] >= 224 || (b[0] == 169 && b[1] == 254) ||
            (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] is >= 16 and <= 31) ||
            (b[0] == 100 && b[1] is >= 64 and <= 127)) return null;
        return ip.ToString();
    }

    public static async Task<string?> LookupAsync(PublicIpv4Target target, CancellationToken token)
    {
        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        handler.ConnectCallback = async (context, cancellation) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellation);
            foreach (var address in addresses)
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    // Windows IP_UNICAST_IF requires the interface index in network byte order.
                    socket.SetSocketOption(SocketOptionLevel.IP, (SocketOptionName)31, IPAddress.HostToNetworkOrder(target.InterfaceIndex));
                    socket.Bind(new IPEndPoint(target.Address, 0));
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellation);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (SocketException) { socket.Dispose(); }
                catch { socket.Dispose(); throw; }
            }
            throw new HttpRequestException("Connexion IPv4 indisponible sur cette carte.");
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        using var response = await client.GetAsync("https://api.ipify.org", HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token);
        var bytes = new byte[64];
        int count = 0, read;
        while (count < bytes.Length && (read = await stream.ReadAsync(bytes.AsMemory(count), token)) > 0) count += read;
        return Parse(System.Text.Encoding.ASCII.GetString(bytes, 0, count));
    }

    private void Cancel() { cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null; pending = null; }
    public void Dispose() => Cancel();
}
