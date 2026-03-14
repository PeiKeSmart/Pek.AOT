using System.Globalization;

using Pek.Extension;
using Pek.Serialization;

namespace Pek.Data;

/// <summary>数据包编码器接口</summary>
public interface IPacketEncoder
{
    /// <summary>将对象编码为数据包</summary>
    /// <param name="value">要编码的对象，支持基础类型、数据包、字节数组和访问器</param>
    /// <returns>编码后的数据包，输入为null时返回null</returns>
    IPacket? Encode(Object? value);

    /// <summary>将数据包解码为指定类型的对象</summary>
    /// <param name="data">要解码的数据包</param>
    /// <param name="type">目标对象类型</param>
    /// <returns>解码后的对象，失败时根据配置返回null或抛出异常</returns>
    Object? Decode(IPacket data, Type type);
}

/// <summary>数据包编码器扩展方法</summary>
public static class PacketEncoderExtensions
{
    /// <summary>将数据包解码为指定类型的对象（泛型版本）</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="encoder">编码器实例</param>
    /// <param name="data">要解码的数据包</param>
    /// <returns>解码后的强类型对象</returns>
    public static T? Decode<T>(this IPacketEncoder encoder, IPacket data) => (T?)encoder.Decode(data, typeof(T));
}

/// <summary>默认数据包编码器</summary>
public class DefaultPacketEncoder : IPacketEncoder
{
    #region 属性
    /// <summary>JSON序列化主机，用于处理复杂对象类型</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>解码出错时是否抛出异常</summary>
    public Boolean ThrowOnError { get; set; }
    #endregion

    #region 编码方法
    /// <summary>将对象编码为数据包</summary>
    /// <param name="value">要编码的对象</param>
    /// <returns>编码后的数据包</returns>
    public virtual IPacket? Encode(Object? value)
    {
        if (value == null) return null;
        if (value is IPacket packet) return packet;
        if (value is Byte[] buffer) return new ArrayPacket(buffer);
        if (value is ISpanSerializable span) return span.ToPacket();
        if (value is IAccessor accessor) return accessor.ToPacket();

        var stringValue = OnEncode(value);
        if (stringValue == null) return null;

        return new ArrayPacket(stringValue.GetBytes());
    }

    /// <summary>将对象编码为字符串</summary>
    /// <param name="value">要编码的对象，已确保非null</param>
    /// <returns>编码后的字符串</returns>
    protected virtual String? OnEncode(Object value)
    {
        var type = value.GetType();
        if (type == typeof(Guid)) return value.ToString();
        if (type == typeof(TimeSpan)) return ((TimeSpan)value).ToString("c", CultureInfo.InvariantCulture);
        if (type == typeof(DateTimeOffset)) return ((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture);

        var typeCode = Type.GetTypeCode(type);

        return typeCode switch
        {
            TypeCode.Object => JsonHost.Write(value),
            TypeCode.String => value as String,
            TypeCode.DateTime => ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            _ => System.Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }
    #endregion

    #region 解码方法
    /// <summary>将数据包解码为指定类型的对象</summary>
    /// <param name="data">要解码的数据包</param>
    /// <param name="type">目标类型</param>
    /// <returns>解码后的对象</returns>
    public virtual Object? Decode(IPacket data, Type type)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (type == null) throw new ArgumentNullException(nameof(type));

        try
        {
            return DecodeInternal(data, type);
        }
        catch
        {
            if (ThrowOnError) throw;
            return null;
        }
    }

    /// <summary>内部解码实现</summary>
    /// <param name="data">数据包</param>
    /// <param name="type">目标类型</param>
    /// <returns>解码后的对象</returns>
    protected virtual Object? DecodeInternal(IPacket data, Type type)
    {
        if (type == typeof(IPacket) || type.IsAssignableFrom(data.GetType())) return data;

#pragma warning disable CS0618
        if (type == typeof(Packet))
            return data is Packet existingPacket ? existingPacket : new Packet(data.ReadBytes());
#pragma warning restore CS0618

        if (type == typeof(Byte[])) return data.ReadBytes();
        if (typeof(ISpanSerializable).IsAssignableFrom(type)) return SpanSerializer.Deserialize(type, data);
        if (typeof(IAccessor).IsAssignableFrom(type)) return type.AccessorRead(data);
        if (data.Total == 0 && IsNullableType(type)) return null;

        var stringValue = data.ToStr();
        return OnDecode(stringValue, type);
    }

    /// <summary>将字符串解码为指定类型的对象</summary>
    /// <param name="value">字符串值</param>
    /// <param name="type">目标类型</param>
    /// <returns>解码后的对象</returns>
    protected virtual Object? OnDecode(String value, Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        var typeCode = Type.GetTypeCode(actualType);

        if (typeCode == TypeCode.String) return value;
        if (IsBaseType(actualType)) return ChangeBaseValue(value, actualType);

        return JsonHost.Read(value, type);
    }

    private static Boolean IsNullableType(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    private static Boolean IsBaseType(Type type)
    {
        if (type.IsEnum) return true;
        if (type == typeof(Guid) || type == typeof(TimeSpan) || type == typeof(DateTimeOffset)) return true;

        return Type.GetTypeCode(type) != TypeCode.Object;
    }

    private static Object? ChangeBaseValue(String value, Type type)
    {
        if (type.IsEnum) return Enum.Parse(type, value, true);
        if (type == typeof(Guid)) return Guid.Parse(value);
        if (type == typeof(TimeSpan)) return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => Boolean.Parse(value),
            TypeCode.Char => Char.Parse(value),
            TypeCode.SByte => SByte.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Byte => Byte.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Int16 => Int16.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.UInt16 => UInt16.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Int32 => Int32.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.UInt32 => UInt32.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Int64 => Int64.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.UInt64 => UInt64.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Single => Single.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Double => Double.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.Decimal => Decimal.Parse(value, CultureInfo.InvariantCulture),
            TypeCode.DateTime => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TypeCode.String => value,
            _ => throw new NotSupportedException($"Type {type.FullName} is not supported by DefaultPacketEncoder."),
        };
    }
    #endregion
}