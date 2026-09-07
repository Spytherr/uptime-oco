using System.Net;
using System.Net.Sockets;

namespace uptime_oco;

public static class OutboundHttpHandlerFactory
{
    private static readonly IPAddress AwsIpv6MetadataAddress = IPAddress.Parse("fd00:ec2::254");

    public static SocketsHttpHandler Create(bool allowPrivateNetworks)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            ConnectCallback = (context, cancellationToken) =>
                ConnectAsync(context.DnsEndPoint, allowPrivateNetworks, cancellationToken)
        };
    }

    internal static bool IsAddressAllowed(IPAddress address, bool allowPrivateNetworks)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return !IsAlwaysBlocked(address)
            && (allowPrivateNetworks || !IsPrivate(address));
    }

    private static async ValueTask<Stream> ConnectAsync(
        DnsEndPoint endpoint,
        bool allowPrivateNetworks,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
        var allowedAddresses = InterleaveAddressFamilies(addresses
            .Where(address => IsAddressAllowed(address, allowPrivateNetworks))
            .Distinct()
            .ToArray());

        if (allowedAddresses.Count == 0)
        {
            throw new HttpRequestException(
                $"Outbound connections to host '{endpoint.Host}' are blocked by network policy.");
        }

        using var connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var attempts = allowedAddresses
            .Select((address, index) => ConnectToAddressAsync(
                address,
                endpoint.Port,
                TimeSpan.FromMilliseconds(index * 250),
                connectCancellation.Token))
            .ToList();
        SocketException? lastError = null;

        while (attempts.Count > 0)
        {
            var completedAttempt = await Task.WhenAny(attempts);
            attempts.Remove(completedAttempt);

            try
            {
                var stream = await completedAttempt;
                connectCancellation.Cancel();
                await DisposeRemainingStreamsAsync(attempts);
                return stream;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                connectCancellation.Cancel();
                await DisposeRemainingStreamsAsync(attempts);
                throw;
            }
            catch (SocketException ex)
            {
                lastError = ex;
            }
        }

        throw new HttpRequestException(
            $"Unable to connect to host '{endpoint.Host}'.",
            lastError);
    }

    private static async Task<Stream> ConnectToAddressAsync(
        IPAddress address,
        int port,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async Task DisposeRemainingStreamsAsync(IEnumerable<Task<Stream>> attempts)
    {
        foreach (var attempt in attempts)
        {
            try
            {
                (await attempt).Dispose();
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }

    private static List<IPAddress> InterleaveAddressFamilies(IPAddress[] addresses)
    {
        if (addresses.Length == 0)
        {
            return [];
        }

        var firstFamily = addresses[0].AddressFamily;
        var preferred = new Queue<IPAddress>(addresses.Where(address => address.AddressFamily == firstFamily));
        var alternate = new Queue<IPAddress>(addresses.Where(address => address.AddressFamily != firstFamily));
        var result = new List<IPAddress>(addresses.Length);

        while (preferred.Count > 0 || alternate.Count > 0)
        {
            if (preferred.TryDequeue(out var preferredAddress))
            {
                result.Add(preferredAddress);
            }

            if (alternate.TryDequeue(out var alternateAddress))
            {
                result.Add(alternateAddress);
            }
        }

        return result;
    }

    private static bool IsAlwaysBlocked(IPAddress address)
    {
        if (address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast)
            || address.Equals(AwsIpv6MetadataAddress))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0
                || bytes[0] >= 224
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0
                || bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2
                || bytes[0] == 192 && bytes[1] == 88 && bytes[2] == 99
                || bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)
                || bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100
                || bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113;
        }

        return bytes[0] == 0xff
            || bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80
            || bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || bytes[0] == 100 && bytes[1] is >= 64 and <= 127
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        return (bytes[0] & 0xfe) == 0xfc
            || bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0;
    }
}
