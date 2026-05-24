using Pek.Data;
using Pek.Model;

namespace Pek.Serialization;

/// <summary>二进制编码解码器</summary>
/// <typeparam name="T">目标实体类型</typeparam>
/// <remarks>
/// AOT 版本不再依赖上游 Binary.FastWrite/FastRead 的反射型实现，
/// 仅复用当前仓已经存在且可裁剪分析的二进制基础设施：
/// IPacket、Byte[]、IAccessor、ISpanSerializable 以及 SpanSerializer 支持的基础值类型。
/// </remarks>
public class BinaryCodec<T> : Handler
{
    /// <summary>使用7位编码整数。默认 true 使用</summary>
    /// <remarks>当前兼容实现仅支持 EncodedInt=true。</remarks>
    public Boolean EncodedInt { get; set; } = true;

    /// <summary>对象转二进制</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>编码后的对象</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        if (message is T entity) return Encode(entity);

        return message;
    }

    /// <summary>二进制转对象</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>解码后的对象</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is IPacket packet) return Decode(packet);

        return message;
    }

    private IPacket Encode(Object value)
    {
        EnsureEncodedInt();

        if (value is IPacket packet) return packet;
        if (value is Byte[] buffer) return new ArrayPacket(buffer);
        if (value is IAccessor accessor) return accessor.ToPacket();

        return SpanSerializer.Serialize(value);
    }

    private Object? Decode(IPacket packet)
    {
        EnsureEncodedInt();

        var type = typeof(T);
        if (type == typeof(IPacket) || type.IsAssignableFrom(packet.GetType())) return packet;

#pragma warning disable CS0618
        if (type == typeof(Packet))
            return packet is Packet existingPacket ? existingPacket : new Packet(packet.ReadBytes());
#pragma warning restore CS0618

        if (type == typeof(Byte[])) return packet.ReadBytes();
        if (typeof(IAccessor).IsAssignableFrom(type)) return type.AccessorRead(packet);

        return SpanSerializer.Deserialize(type, packet);
    }

    private void EnsureEncodedInt()
    {
        if (!EncodedInt)
            throw new NotSupportedException("Pek.AOT BinaryCodec 仅支持 EncodedInt=true；当前仓没有保留未编码长度前缀的等价二进制实现。");
    }
}