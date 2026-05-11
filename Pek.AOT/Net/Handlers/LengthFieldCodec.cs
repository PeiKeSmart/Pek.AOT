using Pek.Buffers;
using Pek.Data;
using Pek.IO;
using Pek.Messaging;
using Pek.Model;

namespace Pek.Net.Handlers;

/// <summary>长度字段编解码器</summary>
public class LengthFieldCodec : MessageCodec<IPacket>
{
    /// <summary>长度字段的偏移量</summary>
    public Int32 Offset { get; set; }

    /// <summary>长度字段占据的字节数，默认2</summary>
    public Int32 Size { get; set; } = 2;

    /// <summary>缓存过期时间，默认5000毫秒</summary>
    public Int32 Expire { get; set; } = 5_000;

    /// <summary>编码：在负载前插入长度字段</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">待编码负载</param>
    /// <returns>带长度头的完整包</returns>
    protected override Object? Encode(IHandlerContext context, IPacket message)
    {
        var dataLength = message.Total;

        var lengthBytes = Math.Abs(Size);
        if (Size == 0)
        {
            Span<Byte> encodedBuffer = stackalloc Byte[5];
            var encodedWriter = new SpanWriter(encodedBuffer);
            lengthBytes = encodedWriter.WriteEncodedInt(dataLength);
        }

        var packet = message.ExpandHeader(Offset + lengthBytes);
        var writer = new SpanWriter(packet.GetSpan()) { IsLittleEndian = Size > 0 };
        if (Offset > 0) writer.Fill(0, Offset);

        switch (Size)
        {
            case 0:
                writer.WriteEncodedInt(dataLength);
                break;
            case 1:
                writer.WriteByte((Byte)dataLength);
                break;
            case 2:
                writer.Write((UInt16)dataLength);
                break;
            case 4:
                writer.Write((UInt32)dataLength);
                break;
            case -2:
                writer.Write((UInt16)dataLength);
                break;
            case -4:
                writer.Write((UInt32)dataLength);
                break;
            default:
                throw new NotSupportedException($"不支持的 Size 值：{Size}");
        }

        return packet;
    }

    /// <summary>解码：按长度字段拆分完整数据包并返回负载</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="packet">原始数据包</param>
    /// <returns>拆分后的负载包</returns>
    protected override IEnumerable<IPacket>? Decode(IHandlerContext context, IPacket packet)
    {
        if (context.Owner is not IExtend extend) yield break;

        if (extend["Codec"] is not PacketCodec codec)
        {
#pragma warning disable CS0618
            extend["Codec"] = codec = new PacketCodec
            {
                Expire = Expire,
                GetLength = p => GetLength(p, Offset, Size),
                GetLength2 = p => GetLength(p, Offset, Size),
                MaxCache = MaxCache,
                Tracer = (context.Owner as ISocket)?.Tracer
            };
#pragma warning restore CS0618
        }

        var packets = codec.Parse(packet);
        for (var i = 0; i < packets.Count; i++)
        {
            var headerLength = Offset + Math.Abs(Size);
            if (Size == 0)
            {
                var span = packets[i].GetSpan();
                var reader = new SpanReader(span) { IsLittleEndian = true };
                reader.Advance(Offset);
                var position = reader.Position;
                _ = reader.ReadEncodedInt();
                headerLength = Offset + reader.Position - position;
            }

            yield return packets[i].Slice(headerLength, -1, true);
        }
    }

    /// <summary>关闭连接时清理缓存</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功关闭</returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        if (context.Owner is IExtend extend) extend["Codec"] = null;

        return base.Close(context, reason);
    }
}