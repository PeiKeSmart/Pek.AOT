using Pek.Data;
using Pek.Extension;
using Pek.Serialization;

namespace Pek.Caching;

/// <summary>Redis Json 编码器</summary>
public class RedisJsonEncoder : IPacketEncoder
{
    /// <summary>解码出错时抛出异常。默认 false 表示返回默认值</summary>
    public Boolean ThrowOnError { get; set; }

    /// <summary>对象转数据包</summary>
    /// <param name="value">对象</param>
    /// <returns>数据包</returns>
    public virtual IPacket? Encode(Object? value)
    {
        if (value == null) return null;
        if (value is IPacket packet) return packet;
        if (value is Byte[] buffer) return new ArrayPacket(buffer);
        if (value is IAccessor accessor) return accessor.ToPacket();

        var type = value.GetType();
        var typeCode = Type.GetTypeCode(type);
        var text = typeCode switch
        {
            TypeCode.Object => value.ToJson(),
            TypeCode.String => value as String,
            TypeCode.DateTime => ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss.fff"),
            _ => value + String.Empty,
        };

        return new ArrayPacket(text.GetBytes());
    }

    /// <summary>数据包转对象</summary>
    /// <param name="data">数据包</param>
    /// <param name="type">目标类型</param>
    /// <returns>对象</returns>
    public virtual Object? Decode(IPacket data, Type type)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (type == null) throw new ArgumentNullException(nameof(type));

        try
        {
            if (type == typeof(IPacket) || type.IsAssignableFrom(data.GetType())) return data;
#pragma warning disable CS0618
            if (type == typeof(Packet)) return data is Packet packet ? packet : new Packet(data.ReadBytes());
#pragma warning restore CS0618
            if (type == typeof(Byte[])) return data.ReadBytes();
            if (typeof(IAccessor).IsAssignableFrom(type)) return type.AccessorRead(data);

            var text = data.ToStr();
            if (Type.GetTypeCode(type) == TypeCode.String) return text;
            if (Type.GetTypeCode(type) != TypeCode.Object)
            {
                if (type == typeof(Boolean) && text == "OK") return true;
                return System.Convert.ChangeType(text, type);
            }

            return text.ToJsonEntity(type);
        }
        catch
        {
            if (ThrowOnError) throw;
            return null;
        }
    }
}
