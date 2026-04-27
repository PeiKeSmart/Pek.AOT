using System.Net;
using System.Net.Sockets;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Messaging;
using Pek.Model;
using Pek.Serialization;

namespace Pek.Net;

/// <summary>网络处理器上下文</summary>
public class NetHandlerContext : HandlerContext
{
    private static readonly Pool<NetHandlerContext> _pool = new();

    /// <summary>从池中借出上下文</summary>
    /// <returns>上下文实例</returns>
    public static NetHandlerContext Rent()
    {
        var context = _pool.Get();
        return context;
    }

    /// <summary>归还上下文到池</summary>
    /// <param name="context">上下文实例</param>
    public static void Return(NetHandlerContext? context)
    {
        if (context == null) return;

        context.Reset();
        _pool.Return(context);
    }

    /// <summary>远程连接</summary>
    public ISocketRemote? Session { get; set; }

    /// <summary>数据帧</summary>
    public IData? Data { get; set; }

    /// <summary>远程地址。因为 ProxyProtocol 协议存在，代理可能返回原始客户端地址</summary>
    public IPEndPoint? Remote { get; set; }

    /// <summary>Socket 事件参数</summary>
    public SocketAsyncEventArgs? EventArgs { get; set; }

    /// <summary>重置上下文，清理状态以便对象池复用</summary>
    public void Reset()
    {
        Session = null;
        Data = null;
        Remote = null;
        EventArgs = null;
        Owner = null;
        Pipeline = null;
        Items.Clear();
    }

    /// <summary>读取管道过滤后最终处理消息</summary>
    /// <param name="message">消息</param>
    public override void FireRead(Object message)
    {
        var data = Data ?? new ReceivedEventArgs();
        data.Message = message;

        if (data is ReceivedEventArgs eventArgs && eventArgs.Context == null)
            eventArgs.Context = this;

        if (Remote != null) data.Remote = Remote;

        if (message is DefaultMessage defaultMessage)
        {
            var raw = defaultMessage.GetRaw();
            if (raw != null) data.Packet = raw;
        }

        Session?.Process(data);
    }

    /// <summary>写入管道过滤后最终处理消息</summary>
    /// <param name="message">消息</param>
    /// <returns>写出结果</returns>
    public override Int32 FireWrite(Object message)
    {
        if (message == null) return -1;

        var session = Session;
        if (session == null) return -2;

        if (message is Byte[] buffer) return session.Send(buffer);
        if (message is IPacket packet) return session.Send(packet);
        if (message is String text) return session.Send(text.GetBytes());
        if (message is ISpanSerializable spanSerializable)
        {
            using var ownerPacket = spanSerializable.ToPacket();
            return session.Send(ownerPacket);
        }
        if (message is IAccessor accessor) return session.Send(accessor.ToPacket());

        if (message is IEnumerable<IPacket> packets)
        {
            var result = 0;
            foreach (var item in packets)
            {
                var count = session.Send(item);
                if (count < 0) break;

                result += count;
            }

            return result;
        }

        throw new XException("Unable to recognize message [{0}], possibly missing encoding processor", message.GetType().FullName);
    }
}