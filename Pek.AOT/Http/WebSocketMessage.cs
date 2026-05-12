using System.Buffers.Binary;
using System.Text;

using Pek.Buffers;
using Pek.Data;
using Pek.Extension;

namespace Pek.Http;

/// <summary>WebSocket消息类型</summary>
public enum WebSocketMessageType
{
    /// <summary>附加数据</summary>
    Data = 0,

    /// <summary>文本数据</summary>
    Text = 1,

    /// <summary>二进制数据</summary>
    Binary = 2,

    /// <summary>连接关闭</summary>
    Close = 8,

    /// <summary>心跳</summary>
    Ping = 9,

    /// <summary>心跳响应</summary>
    Pong = 10,
}

/// <summary>WebSocket消息</summary>
/// <remarks>
/// <para>解析时默认复用输入数据包的底层缓冲区，不做深拷贝。</para>
/// <para>如果消息需要跨线程、延迟处理或长期缓存，应先复制 <see cref="Payload"/>。</para>
/// </remarks>
public class WebSocketMessage : IDisposable
{
    #region 属性
    /// <summary>消息是否结束</summary>
    public Boolean Fin { get; set; }

    /// <summary>消息类型</summary>
    public WebSocketMessageType Type { get; set; }

    /// <summary>掩码</summary>
    public Byte[]? MaskKey { get; set; }

    /// <summary>负载数据</summary>
    public IPacket? Payload { get; set; }

    /// <summary>关闭状态</summary>
    public Int32 CloseStatus { get; set; }

    /// <summary>关闭状态描述</summary>
    public String? StatusDescription { get; set; }
    #endregion

    #region 构造
    /// <summary>释放消息</summary>
    public void Dispose()
    {
        Payload.TryDispose();
        Payload = null;
    }
    #endregion

    #region 方法
    /// <summary>读取消息</summary>
    /// <param name="pk">数据包</param>
    /// <returns>是否读取成功</returns>
    public Boolean Read(IPacket pk)
    {
        if (pk == null || pk.Total < 2) return false;

        var reader = new SpanReader(pk) { IsLittleEndian = false };

        var first = reader.ReadByte();
        Fin = (first & 0x80) != 0;
        Type = (WebSocketMessageType)(first & 0x0F);
        if (!Fin) return false;

        var second = reader.ReadByte();
        var hasMask = (second & 0x80) != 0;

        Int64 length = (Int64)(second & 0x7F);
        if (length == 126)
        {
            if (reader.Available < 2) return false;
            length = reader.ReadUInt16();
        }
        else if (length == 127)
        {
            if (reader.Available < 8) return false;
            length = reader.ReadInt64();
        }

        if (length < 0 || length > Int32.MaxValue) return false;

        var need = (hasMask ? 4 : 0) + length;
        if (reader.Available < need) return false;

        if (!hasMask)
        {
            Payload = reader.ReadPacket((Int32)length);
        }
        else
        {
            var masks = new Byte[4];
            reader.Read(masks);
            MaskKey = masks;

            Payload = reader.ReadPacket((Int32)length);
            var data = Payload.GetSpan();
            for (var i = 0; i < length; i++)
            {
                data[i] = (Byte)(data[i] ^ masks[i % 4]);
            }
        }

        if (Type == WebSocketMessageType.Close && Payload != null && Payload.Total >= 2)
        {
            var data = Payload.GetSpan();
            CloseStatus = BinaryPrimitives.ReadUInt16BigEndian(data[..2]);
            StatusDescription = data[2..].ToStr();
        }

        return true;
    }

    /// <summary>转换为数据包</summary>
    /// <returns>数据包</returns>
    public virtual IPacket ToPacket()
    {
        var body = Payload;
        var length = body == null ? 0 : body.Total;
        var masks = MaskKey;

        if (Type == WebSocketMessageType.Close)
        {
            length = 2;
            if (!StatusDescription.IsNullOrEmpty()) length += Encoding.UTF8.GetByteCount(StatusDescription);
        }

        var headerSize = length switch
        {
            < 126 => 2,
            < 0xFFFF => 4,
            _ => 10,
        };
        if (masks != null) headerSize += masks.Length;
        if (Type == WebSocketMessageType.Close) headerSize += length;

        var packet = body.ExpandHeader(headerSize);
        var writer = new SpanWriter(packet) { IsLittleEndian = false };

        writer.WriteByte((Byte)(0x80 | (Byte)Type));

        if (masks == null)
        {
            if (length < 126)
            {
                writer.WriteByte((Byte)length);
            }
            else if (length <= 0xFFFF)
            {
                writer.WriteByte(126);
                writer.Write((Int16)length);
            }
            else
            {
                writer.WriteByte(127);
                writer.Write((Int64)length);
            }
        }
        else
        {
            if (length < 126)
            {
                writer.WriteByte((Byte)(length | 0x80));
            }
            else if (length <= 0xFFFF)
            {
                writer.WriteByte(126 | 0x80);
                writer.Write((Int16)length);
            }
            else
            {
                writer.WriteByte(127 | 0x80);
                writer.Write((Int64)length);
            }

            writer.Write(masks);

            if (body != null)
            {
                var data = body.GetSpan();
                for (var i = 0; i < length; i++)
                {
                    data[i] = (Byte)(data[i] ^ masks[i % 4]);
                }
            }
        }

        if (body != null && body.Length > 0)
            return packet;

        if (Type == WebSocketMessageType.Close)
        {
            writer.Write((Int16)CloseStatus);
            if (!StatusDescription.IsNullOrEmpty()) writer.Write(StatusDescription, -1);

            packet.Next = null;
        }

        return packet.Slice(0, writer.Position, true);
    }
    #endregion
}