using Pek.Buffers;
using Pek.Collections;
using Pek.Data;
using Pek;

namespace Pek.Messaging;

/// <summary>数据类型标记</summary>
public enum DataKinds : Byte
{
    /// <summary>字符串</summary>
    String = 0,

    /// <summary>二进制数据包</summary>
    Packet = 1,

    /// <summary>二进制对象</summary>
    Binary = 2,

    /// <summary>Json对象</summary>
    Json = 3,
}

/// <summary>标准消息</summary>
public class DefaultMessage : Message
{
    private static readonly Pool<DefaultMessage> _pool = new();
    private IPacket? _raw;

    /// <summary>标记位</summary>
    public Byte Flag { get; set; } = (Byte)DataKinds.Packet;

    /// <summary>序列号</summary>
    public Int32 Sequence { get; set; }

    /// <summary>从池中借出消息实例</summary>
    /// <returns>消息实例</returns>
    public static DefaultMessage Rent() => _pool.Get();

    /// <summary>归还消息实例</summary>
    /// <param name="message">消息实例</param>
    public static void Return(DefaultMessage? message)
    {
        if (message == null) return;

        message.Reset();
        _pool.Return(message);
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (disposing) _raw = null;
    }

    /// <summary>根据请求创建响应消息</summary>
    /// <returns>响应消息</returns>
    public override IMessage CreateReply()
    {
        if (Reply) throw new InvalidOperationException("Cannot create response message based on response message");

        var message = CreateInstance() as DefaultMessage ?? new DefaultMessage();
        message.Flag = Flag;
        message.Reply = true;
        message.Sequence = Sequence;

        return message;
    }

    /// <summary>创建当前类型的新实例</summary>
    /// <returns>消息实例</returns>
    protected override Message CreateInstance()
    {
        if (GetType() == typeof(DefaultMessage)) return Rent();

        return base.CreateInstance();
    }

    /// <summary>从数据包中读取消息</summary>
    /// <param name="packet">原始数据包</param>
    /// <returns>是否成功解析</returns>
    public override Boolean Read(IPacket packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        _raw = packet;

        var count = packet.Total;
        if (count < 4) throw new ArgumentOutOfRangeException(nameof(packet), "The length of the packet header is less than 4 bytes");

        var header = count >= 8 ? packet.ReadBytes(0, 8) : packet.ReadBytes(0, 4);

        Reply = false;
        Error = false;
        OneWay = false;

        Flag = (Byte)(header[0] & 0b0011_1111);
        var mode = header[0] >> 6;
        switch (mode)
        {
            case 0:
                Reply = false;
                break;
            case 1:
                OneWay = true;
                break;
            case 2:
                Reply = true;
                break;
            case 3:
                Reply = true;
                Error = true;
                break;
        }

        Sequence = header[1];

        var size = 4;
        var len = header[2] | (header[3] << 8);
        if (len == 0xFFFF)
        {
            size = 8;
            if (count < size) throw new ArgumentOutOfRangeException(nameof(packet), "The length of the packet header is less than 8 bytes");

            len = header.AsSpan(size - 4, 4).ToArray().ToInt();
        }

        if (size + len > count) throw new ArgumentOutOfRangeException(nameof(packet), $"The packet length {count} is less than {size + len} bytes");

        Payload = packet.Slice(size, len, true);
        return true;
    }

    /// <summary>尝试从数据包中读取消息</summary>
    /// <param name="packet">原始数据包</param>
    /// <param name="message">解析出的消息</param>
    /// <returns>是否成功</returns>
    public static Boolean TryRead(IPacket packet, out DefaultMessage? message)
    {
        message = null;
        if (packet == null || packet.Total < 4) return false;

        try
        {
            message = new DefaultMessage();
            return message.Read(packet);
        }
        catch
        {
            message = null;
            return false;
        }
    }

    /// <summary>把消息转为数据包</summary>
    /// <returns>序列化后的数据包</returns>
    public override IPacket ToPacket()
    {
        var body = Payload;
        var length = body?.Total ?? 0;

        var size = length < 0xFFFF ? 4 : 8;
        var packet = body.ExpandHeader(size);
        var header = packet.GetSpan();

        var flag = Flag & 0b0011_1111;
        if (Reply) flag |= 0x80;
        if (Error || OneWay) flag |= 0x40;
        header[0] = (Byte)flag;
        header[1] = (Byte)(Sequence & 0xFF);

        if (length < 0xFFFF)
        {
            header[2] = (Byte)(length & 0xFF);
            header[3] = (Byte)(length >> 8);
        }
        else
        {
            header[2] = 0xFF;
            header[3] = 0xFF;

            var writer = new SpanWriter(header) { IsLittleEndian = true };
            writer.Advance(4);
            writer.Write(length);
        }

        return packet;
    }

    /// <summary>重置消息状态</summary>
    public override void Reset()
    {
        base.Reset();

        Flag = (Byte)DataKinds.Packet;
        Sequence = 0;
        _raw = null;
    }

    /// <summary>获取完整消息长度</summary>
    /// <param name="packet">数据包</param>
    /// <returns>完整消息长度，0表示数据不足</returns>
    public static Int32 GetLength(IPacket packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));
        return GetLength(packet.Total >= 8 ? packet.ReadBytes(0, 8) : packet.ReadBytes(0, Math.Min(packet.Total, 4)));
    }

    /// <summary>获取完整消息长度</summary>
    /// <param name="span">数据片段</param>
    /// <returns>完整消息长度，0表示数据不足</returns>
    public static Int32 GetLength(ReadOnlySpan<Byte> span)
    {
        if (span.Length < 4) return 0;

        var reader = new SpanReader(span) { IsLittleEndian = true };
        reader.Advance(2);

        var length = reader.ReadUInt16();
        if (length < 0xFFFF) return 4 + length;
        if (span.Length < 8) return 0;

        return 8 + reader.ReadInt32();
    }

    /// <summary>获取原始报文</summary>
    /// <returns>原始数据包</returns>
    public IPacket? GetRaw() => _raw;

    /// <summary>返回文本表示</summary>
    public override String ToString() => $"{Flag:X2} Seq={Sequence:X2} {Payload}";
}