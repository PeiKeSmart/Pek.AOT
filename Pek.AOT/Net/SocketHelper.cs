using System.Net;
using System.Net.Sockets;
using System.Text;

using Pek.Collections;

namespace Pek.Net;

/// <summary>Socket 扩展</summary>
public static class SocketHelper
{
    /// <summary>异步发送数据</summary>
    /// <param name="socket">套接字</param>
    /// <param name="buffer">缓冲区</param>
    /// <returns>发送字节数</returns>
    public static Task<Int32> SendAsync(this Socket socket, Byte[] buffer)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        return Task<Int32>.Factory.FromAsync((Byte[] buf, AsyncCallback callback, Object? state) =>
        {
            return socket.BeginSend(buf, 0, buf.Length, SocketFlags.None, callback, state);
        }, socket.EndSend, buffer, null);
    }

    /// <summary>异步向指定远端发送数据</summary>
    /// <param name="socket">套接字</param>
    /// <param name="buffer">缓冲区</param>
    /// <param name="remote">远程终结点</param>
    /// <returns>发送字节数</returns>
    public static Task<Int32> SendToAsync(this Socket socket, Byte[] buffer, IPEndPoint remote)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (remote == null) throw new ArgumentNullException(nameof(remote));

        return Task<Int32>.Factory.FromAsync((Byte[] buf, IPEndPoint ep, AsyncCallback callback, Object? state) =>
        {
            return socket.BeginSendTo(buf, 0, buf.Length, SocketFlags.None, ep, callback, state);
        }, socket.EndSendTo, buffer, remote, null);
    }

    /// <summary>发送数据流</summary>
    /// <param name="socket">套接字</param>
    /// <param name="stream">数据流</param>
    /// <param name="remoteEndPoint">远程终结点</param>
    /// <returns>当前套接字</returns>
    public static Socket Send(this Socket socket, Stream stream, IPEndPoint? remoteEndPoint = null)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        remoteEndPoint ??= socket.RemoteEndPoint as IPEndPoint;
        if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));

        var buffer = Pool.Shared.Rent(1472);
        try
        {
            while (true)
            {
                var count = stream.Read(buffer, 0, buffer.Length);
                if (count <= 0) break;

                socket.SendTo(buffer, 0, count, SocketFlags.None, remoteEndPoint);
                if (count < buffer.Length) break;
            }
        }
        finally
        {
            Pool.Shared.Return(buffer);
        }

        return socket;
    }

    /// <summary>向指定目的地发送字节数组</summary>
    /// <param name="socket">套接字</param>
    /// <param name="buffer">缓冲区</param>
    /// <param name="remoteEndPoint">远程终结点</param>
    /// <returns>当前套接字</returns>
    public static Socket Send(this Socket socket, Byte[] buffer, IPEndPoint? remoteEndPoint = null)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        remoteEndPoint ??= socket.RemoteEndPoint as IPEndPoint;
        if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));

        socket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, remoteEndPoint);
        return socket;
    }

    /// <summary>向指定目的地发送字符串</summary>
    /// <param name="socket">套接字</param>
    /// <param name="message">消息内容</param>
    /// <param name="encoding">文本编码</param>
    /// <param name="remoteEndPoint">远程终结点</param>
    /// <returns>当前套接字</returns>
    public static Socket Send(this Socket socket, String message, Encoding? encoding = null, IPEndPoint? remoteEndPoint = null)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (message == null) throw new ArgumentNullException(nameof(message));

        encoding ??= Encoding.UTF8;
        return socket.Send(encoding.GetBytes(message), remoteEndPoint);
    }

    /// <summary>广播数据包</summary>
    /// <param name="socket">套接字</param>
    /// <param name="buffer">缓冲区</param>
    /// <param name="port">广播端口</param>
    /// <returns>当前套接字</returns>
    public static Socket Broadcast(this Socket socket, Byte[] buffer, Int32 port)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        if (socket.LocalEndPoint is IPEndPoint ip && ip.Address.AddressFamily != AddressFamily.InterNetwork)
            throw new NotSupportedException("IPv6 does not support broadcasting!");

        if (!socket.EnableBroadcast) socket.EnableBroadcast = true;

        socket.SendTo(buffer, 0, buffer.Length, SocketFlags.None, new IPEndPoint(IPAddress.Broadcast, port));
        return socket;
    }

    /// <summary>广播字符串</summary>
    /// <param name="socket">套接字</param>
    /// <param name="message">消息内容</param>
    /// <param name="port">广播端口</param>
    /// <returns>当前套接字</returns>
    public static Socket Broadcast(this Socket socket, String message, Int32 port)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        return socket.Broadcast(Encoding.UTF8.GetBytes(message), port);
    }

    /// <summary>接收字符串</summary>
    /// <param name="socket">套接字</param>
    /// <param name="encoding">文本编码</param>
    /// <returns>接收的字符串</returns>
    public static String ReceiveString(this Socket socket, Encoding? encoding = null)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));

        EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        var buffer = Pool.Shared.Rent(1460);
        try
        {
            var count = socket.ReceiveFrom(buffer, ref endPoint);
            if (count <= 0) return String.Empty;

            encoding ??= Encoding.UTF8;
            return encoding.GetString(buffer, 0, count);
        }
        finally
        {
            Pool.Shared.Return(buffer);
        }
    }

    /// <summary>检查并开启广播</summary>
    /// <param name="socket">套接字</param>
    /// <param name="address">目标地址</param>
    /// <returns>当前套接字</returns>
    internal static Socket CheckBroadcast(this Socket socket, IPAddress address)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (address == null) throw new ArgumentNullException(nameof(address));

        var buffer = address.GetAddressBytes();
        if (buffer.Length == 4 && buffer[3] == 255)
        {
            if (!socket.EnableBroadcast) socket.EnableBroadcast = true;
        }

        return socket;
    }

    /// <summary>关闭连接</summary>
    /// <param name="socket">套接字</param>
    /// <param name="reuseAddress">是否允许地址重用</param>
    public static void Shutdown(this Socket socket, Boolean reuseAddress = false)
    {
        if (socket == null) return;
        if (IsClosed(socket)) return;

        try
        {
            if (socket.Connected)
            {
                try
                {
                    socket.Disconnect(reuseAddress);
                }
                catch
                {
                }

                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
            }
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch
        {
        }

        try
        {
            socket.Close();
        }
        catch
        {
        }
    }

    private static Boolean IsClosed(Socket socket)
    {
        try
        {
            return socket.SafeHandle?.IsClosed ?? true;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    /// <summary>Socket 是否未被正常关闭</summary>
    /// <param name="socketEventArgs">异步事件参数</param>
    /// <returns>是否属于常见关闭错误</returns>
    internal static Boolean IsNotClosed(this SocketAsyncEventArgs socketEventArgs) => socketEventArgs.SocketError is SocketError.OperationAborted or SocketError.Interrupted or SocketError.NotSocket;

    /// <summary>根据异步事件获取可输出异常，屏蔽常见关闭异常</summary>
    /// <param name="socketEventArgs">异步事件参数</param>
    /// <returns>异常对象</returns>
    internal static Exception? GetException(this SocketAsyncEventArgs socketEventArgs)
    {
        if (socketEventArgs == null) return null;

        if (socketEventArgs.SocketError is SocketError.ConnectionReset or SocketError.OperationAborted or SocketError.Interrupted or SocketError.NotSocket)
            return null;

        return socketEventArgs.ConnectByNameError ?? new SocketException((Int32)socketEventArgs.SocketError);
    }
}