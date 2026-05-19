using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Authentication;

using Pek.Caching;
using Pek.Collections;
using Pek.Extension;
using Pek.IO;
using Pek.Log;

namespace Pek.Net;

/// <summary>网络辅助方法</summary>
public static class NetHelper
{
    private static readonly ICache _cache = MemoryCache.Instance;
    private static readonly Object _ipCacheLock = new();
    private static IPAddress[]? _cachedIPs;
    private static Int64 _cachedIPsExpire;
    private static readonly String[] _excludes = ["Loopback", "VMware", "VBox", "Virtual", "Teredo", "Tunnel", "VPN", "VNIC", "IEEE", "Filter", "Npcap", "QoS", "Miniport", "Kernel Debug"];

    static NetHelper()
    {
        if (!Runtime.Unity)
        {
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        }
    }

    private static void OnNetworkAvailabilityChanged(Object? sender, NetworkAvailabilityEventArgs e) => ClearNetworkCaches();

    private static void OnNetworkAddressChanged(Object? sender, EventArgs e) => ClearNetworkCaches();

    private static void ClearNetworkCaches()
    {
        lock (_ipCacheLock)
        {
            _cachedIPs = null;
            _cachedIPsExpire = 0;
        }

        _cache.Remove("NetHelper:GetIPsWithCache", "NetHelper:ParseAddress:*");
    }

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

    /// <summary>分析地址，根据 IP 或域名得到首个 IP 地址，缓存 60 秒</summary>
    /// <param name="hostname">主机名或地址</param>
    /// <returns>IP 地址</returns>
    public static IPAddress? ParseAddress(this String hostname)
    {
        if (hostname.IsNullOrEmpty()) return null;

        var key = $"NetHelper:ParseAddress:{hostname}";
        if (_cache.TryGetValue<IPAddress>(key, out var address)) return address;

        address = NetUri.ParseAddress(hostname)?.FirstOrDefault();
        _cache.Set(key, address, 60);

        return address;
    }

    /// <summary>分析网络终结点</summary>
    /// <param name="address">地址，可以不带端口</param>
    /// <param name="defaultPort">默认端口</param>
    /// <returns>终结点</returns>
    public static IPEndPoint? ParseEndPoint(String address, Int32 defaultPort = 0)
    {
        if (String.IsNullOrEmpty(address)) return null;

        var index = address.IndexOf("://", StringComparison.Ordinal);
        if (index >= 0) address = address[(index + 3)..];

        var port = 0;
        index = address.LastIndexOf(':');
        IPAddress? ip = null;
        if (index > 0)
        {
            ip = address[..index].ParseAddress();
            port = Int32.Parse(address[(index + 1)..]);
        }
        else
        {
            ip = address.ParseAddress();
            port = defaultPort;
        }

        return ip == null ? null : new IPEndPoint(ip, port);
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

    /// <summary>判断是否本地地址</summary>
    /// <param name="address">地址</param>
    /// <returns>是否本地地址</returns>
    public static Boolean IsLocal(this IPAddress address) => IPAddress.IsLoopback(address) || GetIPsWithCache().Any(ip => ip.Equals(address));

    /// <summary>获取相对于指定远程地址的本地地址</summary>
    /// <param name="address">本地地址</param>
    /// <param name="remote">远程地址</param>
    /// <returns>相对地址</returns>
    public static IPAddress? GetRelativeAddress(this IPAddress address, IPAddress remote)
    {
        var current = address;
        if (current == null || !current.IsAny()) return current;

        if (IPAddress.IsLoopback(remote))
            return current.IsIPv4() ? IPAddress.Loopback : IPAddress.IPv6Loopback;

        foreach (var item in GetIPsWithCache())
        {
            if (item.AddressFamily == current.AddressFamily) return item;
        }

        return null;
    }

    /// <summary>获取相对于指定远程地址的本地终结点</summary>
    /// <param name="local">本地终结点</param>
    /// <param name="remote">远程地址</param>
    /// <returns>相对终结点</returns>
    public static IPEndPoint? GetRelativeEndPoint(this IPEndPoint local, IPAddress remote)
    {
        if (local == null || remote == null) return local;

        var address = local.Address.GetRelativeAddress(remote);
        return address == null ? local : new IPEndPoint(address, local.Port);
    }

    /// <summary>检查指定地址端口是否已被占用</summary>
    /// <param name="address">地址</param>
    /// <param name="protocol">协议</param>
    /// <param name="port">端口</param>
    /// <returns>是否已占用</returns>
    public static Boolean CheckPort(this IPAddress address, NetType protocol, Int32 port)
    {
        if (!Runtime.Windows) return false;

        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[]? endPoints = null;
            switch (protocol)
            {
                case NetType.Tcp:
                    endPoints = properties.GetActiveTcpListeners();
                    break;
                case NetType.Udp:
                    endPoints = properties.GetActiveUdpListeners();
                    break;
                default:
                    return false;
            }

            foreach (var item in endPoints)
            {
                if (item.Port == port && item.Address.Equals(address)) return true;
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        return false;
    }

    /// <summary>检查网络标识对应地址端口是否已被占用</summary>
    /// <param name="uri">网络标识</param>
    /// <returns>是否已占用</returns>
    public static Boolean CheckPort(this NetUri uri) => uri.Address.CheckPort(uri.Type, uri.Port);

    /// <summary>获取所有Tcp连接，带进程Id</summary>
    /// <returns>Tcp连接数组</returns>
    [Obsolete("请使用 GetAllTcpConnections(Int32 processId = -1)")]
    public static TcpConnectionInformation2[] GetAllTcpConnections() => GetAllTcpConnections(-1);

    /// <summary>获取所有Tcp连接，带进程Id</summary>
    /// <param name="processId">目标进程。默认-1未指定，获取所有进程的Tcp连接</param>
    /// <returns>Tcp连接数组</returns>
    public static TcpConnectionInformation2[] GetAllTcpConnections(Int32 processId = -1)
    {
        var result = !Runtime.Windows
            ? TcpConnectionInformation2.GetLinuxTcpConnections(processId)
            : TcpConnectionInformation2.GetWindowsTcpConnections();

        if (processId <= 0) return result;

        return result.Where(item => item.ProcessId == processId).ToArray();
    }

    /// <summary>获取活动网络接口信息</summary>
    /// <returns>活动接口信息</returns>
    public static IEnumerable<IPInterfaceProperties> GetActiveInterfaces()
    {
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.OperationalStatus != OperationalStatus.Up) continue;
            if (item.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown) continue;

            var properties = item.GetIPProperties();
            if (properties != null) yield return properties;
        }
    }

    /// <summary>获取可用 DHCP 地址</summary>
    /// <returns>DHCP 地址集合</returns>
    public static IEnumerable<IPAddress> GetDhcps()
    {
        var list = new List<IPAddress>();
        foreach (var item in GetActiveInterfaces())
        {
#if NET5_0_OR_GREATER
            if (item != null && !OperatingSystem.IsMacOS() && item.DhcpServerAddresses.Count > 0)
#else
            if (item != null && item.DhcpServerAddresses.Count > 0)
#endif
            {
                foreach (var address in item.DhcpServerAddresses)
                {
                    if (list.Contains(address)) continue;
                    list.Add(address);

                    yield return address;
                }
            }
        }
    }

    /// <summary>获取可用 DNS 地址</summary>
    /// <returns>DNS 地址集合</returns>
    public static IEnumerable<IPAddress> GetDns()
    {
        var list = new List<IPAddress>();
        foreach (var item in GetActiveInterfaces())
        {
            if (item == null || item.DnsAddresses.Count <= 0) continue;

            foreach (var address in item.DnsAddresses)
            {
                if (list.Contains(address)) continue;
                list.Add(address);

                yield return address;
            }
        }
    }

    /// <summary>获取可用网关地址</summary>
    /// <returns>网关地址集合</returns>
    public static IEnumerable<IPAddress> GetGateways()
    {
        var list = new List<IPAddress>();
        foreach (var item in GetActiveInterfaces())
        {
            if (item == null || item.GatewayAddresses.Count <= 0) continue;

            foreach (var gateway in item.GatewayAddresses)
            {
                if (list.Contains(gateway.Address)) continue;
                list.Add(gateway.Address);

                yield return gateway.Address;
            }
        }
    }

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

    /// <summary>获取可用多播地址</summary>
    /// <returns>多播地址集合</returns>
    public static IEnumerable<IPAddress> GetMulticasts()
    {
        var list = new List<IPAddress>();
        foreach (var item in GetActiveInterfaces())
        {
            if (item == null || item.MulticastAddresses.Count <= 0) continue;

            foreach (var address in item.MulticastAddresses)
            {
                if (list.Contains(address.Address)) continue;
                list.Add(address.Address);

                yield return address.Address;
            }
        }
    }

    /// <summary>获取所有物理网卡 MAC 地址</summary>
    /// <returns>MAC 地址集合</returns>
    public static IEnumerable<Byte[]> GetMacs()
    {
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown) continue;
            if (_excludes.Any(exclude => item.Description.Contains(exclude))) continue;
            if (Runtime.Windows && item.Speed < 1_000_000) continue;

            var properties = item.GetIPProperties();
            var addresses = properties.UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address)
                .ToArray();
            if (addresses.Length > 0 && addresses.All(IPAddress.IsLoopback)) continue;

            var mac = item.GetPhysicalAddress()?.GetAddressBytes();
            if (mac != null && mac.Length == 6) yield return mac;
        }
    }

    /// <summary>获取网关所在网卡的 MAC 地址</summary>
    /// <returns>MAC 地址</returns>
    public static Byte[]? GetMac()
    {
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (_excludes.Any(exclude => item.Description.Contains(exclude))) continue;
            if (Runtime.Windows && item.Speed < 1_000_000) continue;

            var properties = item.GetIPProperties();
            var addresses = properties.UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address)
                .ToArray();
            if (addresses.All(IPAddress.IsLoopback)) continue;

            addresses = properties.GatewayAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.Address)
                .ToArray();
            if (addresses.Length == 0) continue;

            var mac = item.GetPhysicalAddress()?.GetAddressBytes();
            if (mac != null && mac.Length == 6) return mac;
        }

        return null;
    }

    /// <summary>远程唤醒指定 MAC 地址的计算机</summary>
    /// <param name="macs">MAC 地址集合</param>
    public static void Wake(params String[] macs)
    {
        if (macs == null || macs.Length <= 0) return;

        foreach (var item in macs)
        {
            Wake(item);
        }
    }

    private static void Wake(String mac)
    {
        mac = mac.Replace("-", null).Replace(":", null);

        var buffer = Pool.Shared.Rent(mac.Length / 2);
        var packet = Pool.Shared.Rent(6 + 16 * buffer.Length);
        try
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = Byte.Parse(mac.Substring(index * 2, 2), NumberStyles.HexNumber);
            }

            for (var index = 0; index < 6; index++)
            {
                packet[index] = 0xFF;
            }

            for (Int32 index = 6, bufferIndex = 0; index < packet.Length; index++, bufferIndex++)
            {
                if (bufferIndex >= buffer.Length) bufferIndex = 0;

                packet[index] = buffer[bufferIndex];
            }

            using var client = new UdpClient { EnableBroadcast = true };
            client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 7));
        }
        finally
        {
            Pool.Shared.Return(packet);
            Pool.Shared.Return(buffer);
        }
    }

    [DllImport("Iphlpapi.dll")]
    private static extern Int32 SendARP(UInt32 destip, UInt32 srcip, Byte[] mac, ref Int32 length);

    /// <summary>根据 IP 地址获取 MAC 地址</summary>
    /// <param name="ip">IP 地址</param>
    /// <returns>MAC 地址</returns>
    public static Byte[]? GetMac(this IPAddress ip)
    {
        var length = 16;
        var buffer = new Byte[16];

        if (Runtime.Windows)
        {
            var result = SendARP(ip.GetAddressBytes().ToUInt32(), 0, buffer, ref length);
            if (result != 0 || length <= 0) return null;
            if (length != buffer.Length) buffer = buffer.ReadBytes(0, length);
        }
        else
        {
            foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (_excludes.Any(exclude => item.Description.Contains(exclude))) continue;
                if (Runtime.Windows && item.Speed < 1_000_000) continue;

                var properties = item.GetIPProperties();
                var addresses = properties.UnicastAddresses
                    .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(address => address.Address)
                    .ToArray();
                if (addresses.All(IPAddress.IsLoopback)) continue;

                foreach (var info in properties.UnicastAddresses)
                {
                    if (!info.Address.Equals(ip)) continue;
                    buffer = item.GetPhysicalAddress()?.GetAddressBytes() ?? buffer;
                }

                if (buffer.Length == 6) return buffer;
            }
        }

        return buffer;
    }

    /// <summary>IP 地址解析器</summary>
    public static IIPResolver? IpResolver { get; set; }

    /// <summary>获取 IP 地址对应的物理位置</summary>
    /// <param name="address">IP 地址</param>
    /// <returns>物理位置</returns>
    public static String? GetAddress(this IPAddress address)
    {
        if (address.IsAny()) return "任意地址";
        if (IPAddress.IsLoopback(address)) return "本地环回";
        if (address.IsLocal()) return "本机地址";

        return IpResolver?.GetAddress(address);
    }

    /// <summary>把字符串形式的 IP 地址转换为物理位置</summary>
    /// <param name="address">IP 地址或网络地址字符串</param>
    /// <returns>物理位置</returns>
    public static String IPToAddress(this String address)
    {
        if (address.IsNullOrEmpty()) return String.Empty;

        var index = address.IndexOf("://", StringComparison.Ordinal);
        if (index >= 0) address = address[(index + 3)..];

        index = address.IndexOf(',');
        if (index >= 0) address = address.Split(',').First();

        if (address.Replace("::", null).Contains(':')) address = address[..address.LastIndexOf(':')];

        return !IPAddress.TryParse(address, out var ip) ? String.Empty : (ip.GetAddress() ?? String.Empty);
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