using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;

using Pek.Collections;

namespace Pek.Net;

/// <summary>网络辅助方法</summary>
public static class NetHelper
{
    private static readonly Object _ipCacheLock = new();
    private static IPAddress[]? _cachedIPs;
    private static Int64 _cachedIPsExpire;

    /// <summary>设置 TCP KeepAlive 参数</summary>
    /// <param name="socket">Socket</param>
    /// <param name="isKeepAlive">是否启用</param>
    /// <param name="startTime">首次探测前等待秒数</param>
    /// <param name="interval">探测间隔秒数</param>
    public static void SetTcpKeepAlive(this Socket socket, Boolean isKeepAlive, Int32 startTime, Int32 interval)
    {
        if (socket == null) return;

        if (OperatingSystem.IsWindows())
        {
            UInt32 dummy = 0;
            var buffer = Pool.Shared.Rent(Marshal.SizeOf(dummy) * 3);
            try
            {
                BitConverter.GetBytes((UInt32)(isKeepAlive ? 1 : 0)).CopyTo(buffer, 0);
                BitConverter.GetBytes((UInt32)startTime * 1000).CopyTo(buffer, Marshal.SizeOf(dummy));
                BitConverter.GetBytes((UInt32)interval * 1000).CopyTo(buffer, Marshal.SizeOf(dummy) * 2);

                socket.IOControl(IOControlCode.KeepAliveValues, buffer, null);
            }
            finally
            {
                Pool.Shared.Return(buffer);
            }

            return;
        }

        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, isKeepAlive);
#if NETCOREAPP
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, startTime);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, interval);
#endif
    }

    /// <summary>根据地址族选择对应的任意地址</summary>
    /// <param name="address">原始地址</param>
    /// <param name="family">目标地址族</param>
    /// <returns>匹配的任意地址或原地址</returns>
    public static IPAddress GetRightAny(this IPAddress address, AddressFamily family)
    {
        if (!address.IsAny()) return address;

        return family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
    }

    /// <summary>判断是否任意地址</summary>
    /// <param name="address">地址</param>
    /// <returns>是否任意地址</returns>
    public static Boolean IsAny(this IPAddress address) => IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address);

    /// <summary>判断是否任意终结点</summary>
    /// <param name="endPoint">终结点</param>
    /// <returns>是否任意终结点</returns>
    public static Boolean IsAny(this EndPoint endPoint) => endPoint is IPEndPoint ip && (ip.Port == 0 || ip.Address.IsAny());

    /// <summary>判断是否 IPv4 地址</summary>
    /// <param name="address">地址</param>
    /// <returns>是否 IPv4</returns>
    public static Boolean IsIPv4(this IPAddress address) => address.AddressFamily == AddressFamily.InterNetwork;

    /// <summary>获取可用的 IP 地址</summary>
    /// <returns>按优先级排序的 IP 地址集合</returns>
    public static IEnumerable<IPAddress> GetIPs()
    {
        var candidates = new List<KeyValuePair<UnicastIPAddressInformation, Int32>>();
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.OperationalStatus != OperationalStatus.Up) continue;
            if (item.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown) continue;

            var properties = item.GetIPProperties();
            if (properties == null || properties.UnicastAddresses.Count == 0) continue;

            var gatewayCount = 0;
#if NET5_0_OR_GREATER
            if (!OperatingSystem.IsAndroid()) gatewayCount = properties.GatewayAddresses.Count;
#else
            gatewayCount = properties.GatewayAddresses.Count;
#endif

            foreach (var addressInfo in properties.UnicastAddresses)
            {
                var factor = gatewayCount * 10 + 5;
                var address = addressInfo.Address;
                if (address.IsIPv4())
                {
                    factor++;
                    if (address.GetAddressBytes()[0] == 169) factor--;
                }
                else
                {
                    if (address.IsIPv4MappedToIPv6) continue;
                    if (address.IsIPv6LinkLocal) factor--;
                    if (address.IsIPv6Multicast) continue;
                    if (address.IsIPv6SiteLocal) continue;
#if NET6_0_OR_GREATER
                    if (address.IsIPv6UniqueLocal) factor -= 2;
#endif
                }

#if NET5_0_OR_GREATER
                try
                {
                    if (OperatingSystem.IsWindows() && addressInfo.DuplicateAddressDetectionState != DuplicateAddressDetectionState.Preferred)
                        continue;
                }
                catch
                {
                }
#endif

                candidates.Add(new KeyValuePair<UnicastIPAddressInformation, Int32>(addressInfo, factor));
            }
        }

        candidates.Sort(static (left, right) => right.Value.CompareTo(left.Value));

        var hashes = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var list = new List<IPAddress>(candidates.Count);
        foreach (var item in candidates)
        {
            var address = item.Key.Address;
            if (hashes.Add(address.ToString())) list.Add(address);
        }

        return list;
    }

    /// <summary>获取本机可用 IP 地址，缓存 60 秒</summary>
    /// <returns>IP 地址数组</returns>
    public static IPAddress[] GetIPsWithCache()
    {
        var now = Runtime.TickCount64;
        var addrs = _cachedIPs;
        if (addrs != null && _cachedIPsExpire > now) return addrs;

        lock (_ipCacheLock)
        {
            addrs = _cachedIPs;
            if (addrs != null && _cachedIPsExpire > now) return addrs;

            addrs = [.. GetIPs()];
            _cachedIPs = addrs;
            _cachedIPsExpire = now + 60_000;
            return addrs;
        }
    }

    /// <summary>获取本地第一个 IPv4 地址。一般是网关所在网卡的 IP 地址</summary>
    /// <returns>IPv4 地址</returns>
    public static IPAddress? MyIP()
    {
        foreach (var ip in GetIPsWithCache())
        {
            if (ip.IsIPv4() && !IPAddress.IsLoopback(ip) && ip.GetAddressBytes()[0] != 169) return ip;
        }

        return null;
    }

    /// <summary>获取本地第一个 IPv6 地址</summary>
    /// <returns>IPv6 地址</returns>
    public static IPAddress? MyIPv6()
    {
        foreach (var ip in GetIPsWithCache())
        {
            if (!ip.IsIPv4() && !IPAddress.IsLoopback(ip)) return ip;
        }

        return null;
    }

    /// <summary>根据本地网络标识创建客户端</summary>
    /// <param name="local">本地网络标识</param>
    /// <returns>Socket客户端</returns>
    public static ISocketClient CreateClient(this NetUri local)
    {
        if (local == null) throw new ArgumentNullException(nameof(local));

        return local.Type switch
        {
            NetType.Tcp => new TcpSession { Local = local },
            NetType.Udp => new UdpServer { Local = local },
            _ => throw new NotSupportedException($"The {local.Type} protocol is not supported"),
        };
    }

    /// <summary>根据远程网络标识创建客户端</summary>
    /// <param name="remote">远程网络标识</param>
    /// <returns>Socket客户端</returns>
    public static ISocketClient CreateRemote(this NetUri remote)
    {
        if (remote == null) throw new ArgumentNullException(nameof(remote));

        return remote.Type switch
        {
            NetType.Tcp => new TcpSession { Remote = remote },
            NetType.Udp => new UdpServer { Remote = remote },
            NetType.Http => new TcpSession
            {
                Remote = remote,
                SslProtocol = remote.Port == 443 ? SslProtocols.Tls12 : SslProtocols.None,
            },
            NetType.WebSocket => new WebSocketClient
            {
                Remote = remote,
                Uri = new Uri(remote.ToString()),
                SslProtocol = remote.Port == 443 ? SslProtocols.Tls12 : SslProtocols.None,
            },
            _ => throw new NotSupportedException($"The {remote.Type} protocol is not supported"),
        };
    }

    /// <summary>根据 Uri 创建客户端</summary>
    /// <param name="uri">资源地址</param>
    /// <returns>Socket客户端</returns>
    public static ISocketClient CreateRemote(this Uri uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        return uri.Scheme switch
        {
            "wss" => new WebSocketClient(uri) { SslProtocol = SslProtocols.Tls12 },
            "ws" => new WebSocketClient(uri),
            _ => throw new NotSupportedException($"The {uri.Scheme} protocol is not supported"),
        };
    }

    /// <summary>创建 TCP Socket</summary>
    /// <param name="ipv4">是否 IPv4</param>
    /// <returns>Socket</returns>
    internal static Socket CreateTcp(Boolean ipv4 = true) => new(ipv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

    /// <summary>创建 UDP Socket</summary>
    /// <param name="ipv4">是否 IPv4</param>
    /// <returns>Socket</returns>
    internal static Socket CreateUdp(Boolean ipv4 = true) => new(ipv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
}