using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using Pek.Collections;

namespace Pek.Net;

/// <summary>网络辅助方法</summary>
public static class NetHelper
{
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

    /// <summary>创建 TCP Socket</summary>
    /// <param name="ipv4">是否 IPv4</param>
    /// <returns>Socket</returns>
    internal static Socket CreateTcp(Boolean ipv4 = true) => new(ipv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

    /// <summary>创建 UDP Socket</summary>
    /// <param name="ipv4">是否 IPv4</param>
    /// <returns>Socket</returns>
    internal static Socket CreateUdp(Boolean ipv4 = true) => new(ipv4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
}