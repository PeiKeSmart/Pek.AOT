using System.Collections.Concurrent;

using Pek.Buffers;
using Pek.Collections;
using Pek.Data;

namespace Pek.Serialization;

/// <summary>高性能Span二进制序列化器</summary>
public static class SpanSerializer
{
    private static readonly ConcurrentDictionary<Type, Func<Object>> _factories = new();
    private static readonly DateTime _dt1970 = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>协议头部预留空间。序列化时在缓冲区前方预留该字节数，默认32</summary>
    public static Int32 HeaderReserve { get; set; } = 32;

    /// <summary>注册工厂</summary>
    /// <typeparam name="T">目标类型</typeparam>
    public static void RegisterFactory<T>() where T : class, ISpanSerializable, new() => RegisterFactory<T>(() => new T());

    /// <summary>注册工厂</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="factory">工厂</param>
    public static void RegisterFactory<T>(Func<T> factory) where T : class, ISpanSerializable
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _factories[typeof(T)] = () => factory() ?? throw new InvalidOperationException($"SpanSerializer factory returned null for {typeof(T).FullName}");
    }

    /// <summary>序列化对象到数据包</summary>
    /// <param name="value">目标对象</param>
    /// <param name="bufferSize">初始缓冲区大小</param>
    /// <returns>数据包，调用方负责Dispose</returns>
    public static IOwnerPacket Serialize(Object value, Int32 bufferSize = 4096)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var reserve = HeaderReserve;
        var packet = new OwnerPacket(bufferSize);
        var stream = Pool.MemoryStream.Get();
        stream.Position = reserve;
        var writer = new SpanWriter(packet.GetSpan()[reserve..], stream);

        WriteObject(ref writer, value, value.GetType());

        if (writer.TotalWritten == writer.WrittenCount)
        {
            var count = writer.WrittenCount;
            Pool.MemoryStream.Return(stream);
            return (packet.Slice(reserve, count) as IOwnerPacket)!;
        }

        writer.Flush();
        packet.Dispose();

        stream.Position = reserve;
        return new OwnerPacket(stream);
    }

    /// <summary>将ISpanSerializable对象序列化为数据包</summary>
    /// <param name="value">目标对象</param>
    /// <param name="bufferSize">初始缓冲区大小</param>
    /// <param name="reserve">头部预留空间</param>
    /// <returns>数据包，调用方负责释放</returns>
    public static IOwnerPacket ToPacket(this ISpanSerializable value, Int32 bufferSize = 8192, Int32 reserve = 32)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var packet = new OwnerPacket(bufferSize);
        var stream = Pool.MemoryStream.Get();
        stream.Position = reserve;
        var writer = new SpanWriter(packet.GetSpan()[reserve..], stream);

        value.Write(ref writer);

        if (writer.TotalWritten == writer.WrittenCount)
        {
            var count = writer.WrittenCount;
            Pool.MemoryStream.Return(stream);
            return (packet.Slice(reserve, count) as IOwnerPacket)!;
        }

        writer.Flush();
        packet.Dispose();

        stream.Position = reserve;
        return new OwnerPacket(stream);
    }

    /// <summary>序列化为带帧头的数据包</summary>
    /// <param name="value">目标对象</param>
    /// <param name="headerSize">帧头大小</param>
    /// <param name="bufferSize">初始缓冲区大小</param>
    /// <returns>数据包</returns>
    public static IOwnerPacket ToFrame(this ISpanSerializable value, Int32 headerSize, Int32 bufferSize = 8192)
    {
        if (headerSize <= 0) throw new ArgumentOutOfRangeException(nameof(headerSize));

        var body = value.ToPacket(bufferSize, headerSize);
        return new OwnerPacket((OwnerPacket)body, headerSize);
    }

    /// <summary>从二进制数据反序列化填充ISpanSerializable对象</summary>
    /// <param name="value">目标对象</param>
    /// <param name="data">数据</param>
    /// <returns>实际消费的字节数</returns>
    public static Int32 FromSpan(this ISpanSerializable value, ReadOnlySpan<Byte> data)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var reader = new SpanReader(data);
        value.Read(ref reader);
        return reader.Position;
    }

    /// <summary>从数据包反序列化填充ISpanSerializable对象</summary>
    /// <param name="value">目标对象</param>
    /// <param name="data">数据包</param>
    /// <returns>实际消费的字节数</returns>
    public static Int32 FromPacket(this ISpanSerializable value, IPacket data)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (data == null) throw new ArgumentNullException(nameof(data));

        var reader = new SpanReader(data);
        value.Read(ref reader);
        return reader.Position;
    }

    /// <summary>序列化对象到指定Span</summary>
    /// <param name="value">目标对象</param>
    /// <param name="buffer">目标缓冲区</param>
    /// <returns>写入字节数</returns>
    public static Int32 Serialize(Object value, Span<Byte> buffer)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var writer = new SpanWriter(buffer);
        WriteObject(ref writer, value, value.GetType());
        return writer.WrittenCount;
    }

    /// <summary>反序列化字节数组为对象</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="data">字节数据</param>
    /// <returns>对象实例</returns>
    public static T Deserialize<T>(ReadOnlySpan<Byte> data) => (T)Deserialize(typeof(T), data);

    /// <summary>反序列化字节数组为对象</summary>
    /// <param name="type">目标类型</param>
    /// <param name="data">字节数据</param>
    /// <returns>对象实例</returns>
    public static Object Deserialize(Type type, ReadOnlySpan<Byte> data)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var reader = new SpanReader(data);
        return ReadObject(ref reader, type);
    }

    /// <summary>从数据包反序列化为指定类型的对象</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="data">数据包</param>
    /// <returns>对象实例</returns>
    public static T Deserialize<T>(IPacket data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        return (T)Deserialize(typeof(T), data);
    }

    /// <summary>从数据包反序列化为对象</summary>
    /// <param name="type">目标类型</param>
    /// <param name="data">数据包</param>
    /// <returns>对象实例</returns>
    public static Object Deserialize(Type type, IPacket data)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (data.Next == null)
        {
            var reader = new SpanReader(data.GetSpan());
            return ReadObject(ref reader, type);
        }

        return Deserialize(type, (ReadOnlySpan<Byte>)data.ReadBytes());
    }

    /// <summary>写入单个值</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="value">值</param>
    /// <param name="type">值类型</param>
    public static void WriteValue(ref SpanWriter writer, Object? value, Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        var needsNullFlag = !actualType.IsValueType || Nullable.GetUnderlyingType(type) != null || actualType == typeof(Byte[]);
        if (needsNullFlag)
        {
            if (value == null || value == DBNull.Value)
            {
                writer.Write((Byte)0);
                return;
            }

            writer.Write((Byte)1);
        }

        WriteDirectValue(ref writer, value, actualType);
    }

    /// <summary>读取单个值</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="type">值类型</param>
    /// <returns>值</returns>
    public static Object? ReadValue(ref SpanReader reader, Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        var needsNullFlag = !actualType.IsValueType || Nullable.GetUnderlyingType(type) != null || actualType == typeof(Byte[]);
        if (needsNullFlag && reader.ReadByte() == 0) return null;

        return ReadDirectValue(ref reader, actualType);
    }

    /// <summary>写入引用类型对象（含null标记）</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="writer">Span写入器</param>
    /// <param name="value">对象</param>
    public static void Write<T>(ref SpanWriter writer, T? value) where T : class
    {
        if (value == null)
        {
            writer.Write((Byte)0);
            return;
        }

        writer.Write((Byte)1);
        WriteObject(ref writer, value, typeof(T));
    }

    /// <summary>读取引用类型对象（含null标记）</summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="reader">Span读取器</param>
    /// <returns>对象</returns>
    public static T? Read<T>(ref SpanReader reader) where T : class
    {
        if (reader.ReadByte() == 0) return null;

        return (T)ReadObject(ref reader, typeof(T));
    }

    /// <summary>写入对象成员到SpanWriter</summary>
    /// <param name="writer">Span写入器</param>
    /// <param name="value">对象</param>
    /// <param name="type">对象类型</param>
    public static void WriteObject(ref SpanWriter writer, Object value, Type type)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        if (type == null) throw new ArgumentNullException(nameof(type));

        if (value is ISpanSerializable serializable)
        {
            serializable.Write(ref writer);
            return;
        }

        WriteDirectValue(ref writer, value, type);
    }

    /// <summary>从SpanReader读取对象成员</summary>
    /// <param name="reader">Span读取器</param>
    /// <param name="type">对象类型</param>
    /// <returns>对象实例</returns>
    public static Object ReadObject(ref SpanReader reader, Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        if (typeof(ISpanSerializable).IsAssignableFrom(type))
        {
            if (!_factories.TryGetValue(type, out var factory))
                throw new InvalidOperationException($"SpanSerializable type {type.FullName} is not registered. Call RegisterFactory before using non-generic deserialization.");

            var obj = factory();
            if (obj is not ISpanSerializable serializable)
                throw new InvalidOperationException($"Registered factory for {type.FullName} did not produce an ISpanSerializable instance.");

            serializable.Read(ref reader);
            return obj;
        }

        return ReadDirectValue(ref reader, type) ?? throw new InvalidOperationException($"Unable to deserialize type {type.FullName}.");
    }

    private static void WriteDirectValue(ref SpanWriter writer, Object? value, Type type)
    {
        if (type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            WriteDirectValue(ref writer, System.Convert.ChangeType(value, underlyingType), underlyingType);
            return;
        }

        var code = Type.GetTypeCode(type);
        switch (code)
        {
            case TypeCode.Boolean:
                writer.Write((Byte)(value is Boolean booleanValue && booleanValue ? 1 : 0));
                break;
            case TypeCode.SByte:
                writer.Write(unchecked((Byte)(value is SByte sbyteValue ? sbyteValue : System.Convert.ToSByte(value))));
                break;
            case TypeCode.Byte:
                writer.Write(value is Byte byteValue ? byteValue : System.Convert.ToByte(value));
                break;
            case TypeCode.Char:
                writer.Write((UInt16)(value is Char charValue ? charValue : System.Convert.ToChar(value)));
                break;
            case TypeCode.Int16:
                writer.Write(value is Int16 int16Value ? int16Value : System.Convert.ToInt16(value));
                break;
            case TypeCode.UInt16:
                writer.Write(value is UInt16 uint16Value ? uint16Value : System.Convert.ToUInt16(value));
                break;
            case TypeCode.Int32:
                writer.Write(value is Int32 int32Value ? int32Value : System.Convert.ToInt32(value));
                break;
            case TypeCode.UInt32:
                writer.Write(value is UInt32 uint32Value ? uint32Value : System.Convert.ToUInt32(value));
                break;
            case TypeCode.Int64:
                writer.Write(value is Int64 int64Value ? int64Value : System.Convert.ToInt64(value));
                break;
            case TypeCode.UInt64:
                writer.Write(value is UInt64 uint64Value ? uint64Value : System.Convert.ToUInt64(value));
                break;
            case TypeCode.Single:
                writer.Write(value is Single singleValue ? singleValue : System.Convert.ToSingle(value));
                break;
            case TypeCode.Double:
                writer.Write(value is Double doubleValue ? doubleValue : System.Convert.ToDouble(value));
                break;
            case TypeCode.Decimal:
                var bits = Decimal.GetBits(value is Decimal decimalValue ? decimalValue : System.Convert.ToDecimal(value));
                for (var i = 0; i < 4; i++)
                {
                    writer.Write(bits[i]);
                }
                break;
            case TypeCode.DateTime:
                var dateTime = value is DateTime dt ? dt : System.Convert.ToDateTime(value);
                writer.Write(dateTime > DateTime.MinValue ? (UInt32)(dateTime - _dt1970).TotalSeconds : (UInt32)0);
                break;
            case TypeCode.String:
                writer.Write(value as String, 0);
                break;
            case TypeCode.Object:
                if (type == typeof(Guid))
                {
                    writer.Write(value is Guid guidValue ? guidValue : Guid.Empty);
                    break;
                }

                if (type == typeof(Byte[]))
                {
                    var buffer = value as Byte[];
                    writer.WriteEncodedInt(buffer?.Length ?? 0);
                    if (buffer is { Length: > 0 }) writer.Write(buffer);
                    break;
                }

                throw new NotSupportedException($"Type {type.FullName} is not supported by Pek.AOT SpanSerializer. Implement ISpanSerializable and register a factory for this type.");
            default:
                throw new NotSupportedException($"Type {type.FullName} is not supported by Pek.AOT SpanSerializer.");
        }
    }

    private static Object? ReadDirectValue(ref SpanReader reader, Type type)
    {
        if (type.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(type);
            var enumValue = ReadDirectValue(ref reader, underlyingType);
            return enumValue == null ? null : Enum.ToObject(type, enumValue);
        }

        var code = Type.GetTypeCode(type);
        return code switch
        {
            TypeCode.Boolean => reader.ReadByte() != 0,
            TypeCode.SByte => unchecked((SByte)reader.ReadByte()),
            TypeCode.Byte => reader.ReadByte(),
            TypeCode.Char => (Char)reader.ReadUInt16(),
            TypeCode.Int16 => reader.ReadInt16(),
            TypeCode.UInt16 => reader.ReadUInt16(),
            TypeCode.Int32 => reader.ReadInt32(),
            TypeCode.UInt32 => reader.ReadUInt32(),
            TypeCode.Int64 => reader.ReadInt64(),
            TypeCode.UInt64 => reader.ReadUInt64(),
            TypeCode.Single => reader.ReadSingle(),
            TypeCode.Double => reader.ReadDouble(),
            TypeCode.Decimal => new Decimal([reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()]),
            TypeCode.DateTime => reader.ReadUInt32() is var seconds && seconds == 0 ? DateTime.MinValue : _dt1970.AddSeconds(seconds),
            TypeCode.String => reader.ReadString(),
            TypeCode.Object => ReadComplexValue(ref reader, type),
            _ => throw new NotSupportedException($"Type {type.FullName} is not supported by Pek.AOT SpanSerializer."),
        };
    }

    private static Object? ReadComplexValue(ref SpanReader reader, Type type)
    {
        if (type == typeof(Guid)) return reader.Read<Guid>();
        if (type == typeof(Byte[]))
        {
            var length = reader.ReadEncodedInt();
            if (length <= 0) return Array.Empty<Byte>();
            return reader.ReadBytes(length).ToArray();
        }

        throw new NotSupportedException($"Type {type.FullName} is not supported by Pek.AOT SpanSerializer. Implement ISpanSerializable and register a factory for this type.");
    }
}