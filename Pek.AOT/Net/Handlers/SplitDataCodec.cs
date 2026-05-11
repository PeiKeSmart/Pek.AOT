using Pek.Data;
using Pek.Messaging;
using Pek.Model;

namespace Pek.Net.Handlers;

/// <summary>按指定分隔字节处理粘包的处理器</summary>
public class SplitDataCodec : Handler
{
    /// <summary>分割字节数据，默认 CRLF</summary>
    public Byte[] SplitData { get; set; } = [0x0D, 0x0A];

    /// <summary>最大缓存待处理数据，默认1024字节</summary>
    public Int32 MaxCacheDataLength { get; set; } = 1024;

    /// <summary>写入数据时在末尾追加分割字节</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">待发送消息</param>
    /// <returns>追加分隔符后的消息</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        if (message is IPacket packet) message = packet.Append(SplitData);

        return base.Write(context, message);
    }

    /// <summary>读取数据</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理后的消息</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is not IPacket packet) return base.Read(context, message);

        var list = Decode(context, packet);
        if (list == null) return null;

        foreach (var item in list)
        {
            base.Read(context, item);
        }

        return null;
    }

    /// <summary>连接关闭时清理缓存</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功关闭</returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        if (context.Owner is IExtend extend) extend["Codec"] = null;

        return base.Close(context, reason);
    }

    private IList<IPacket>? Decode(IHandlerContext context, IPacket packet)
    {
        if (context.Owner is not IExtend extend) return null;

        if (extend["Codec"] is not PacketCodec codec)
        {
#pragma warning disable CS0618
            extend["Codec"] = codec = new PacketCodec
            {
                MaxCache = MaxCacheDataLength,
                GetLength = GetLineLength,
                GetLength2 = GetLineLength,
                Tracer = (context.Owner as ISocket)?.Tracer
            };
#pragma warning restore CS0618
        }

        return codec.Parse(packet);
    }

    private Int32 GetLineLength(IPacket packet)
    {
        var index = packet.GetSpan().IndexOf(SplitData);
        if (index < 0) return 0;

        return index + SplitData.Length;
    }

    private Int32 GetLineLength(ReadOnlySpan<Byte> span)
    {
        var index = span.IndexOf(SplitData);
        if (index < 0) return 0;

        return index + SplitData.Length;
    }
}