using System.Collections;

using Pek.Buffers;
using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Model;

namespace Pek.Serialization;

/// <summary>二进制编码解码器</summary>
/// <remarks>
/// 上游 BinaryCodec2 依赖的 Object 动态读取链路并未闭环；
/// AOT 版本改为显式类型标记协议，专门承接动态字典/列表与常见标量类型，
/// 避免反射构造、BinaryFormatter 和 Json 回退。
/// </remarks>
public class BinaryCodec2 : Handler
{
    private enum ValueKind : Byte
    {
        Null = 0,
        BooleanFalse = 1,
        BooleanTrue = 2,
        Byte = 3,
        SByte = 4,
        Int16 = 5,
        UInt16 = 6,
        Int32 = 7,
        UInt32 = 8,
        Int64 = 9,
        UInt64 = 10,
        Single = 11,
        Double = 12,
        Decimal = 13,
        String = 14,
        DateTime = 15,
        Guid = 16,
        ByteArray = 17,
        Dictionary = 18,
        List = 19,
        TimeSpan = 20,
        DateTimeOffset = 21,
    }

    /// <summary>使用7位编码整数。默认true使用</summary>
    public Boolean EncodedInt { get; set; } = true;

    /// <summary>对象转二进制</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>编码结果</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        if (message is IDictionary<String, Object> dictionary)
            return Encode(ToNullableDictionary(dictionary));

        if (message is IDictionary<String, Object?> nullableDictionary)
            return Encode(nullableDictionary);

        if (message is IDictionarySource source)
            return Encode(source.ToDictionary());

        return message;
    }

    /// <summary>二进制转对象</summary>
    /// <param name="context">处理器上下文</param>
    /// <param name="message">消息</param>
    /// <returns>解码结果</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is not IPacket packet) return message;

        return Decode(packet);
    }

    private IPacket Encode(IDictionary<String, Object?> dictionary)
    {
        using var packet = new OwnerPacket(4096);
        var stream = Pool.MemoryStream.Get();
        var writer = new SpanWriter(packet.GetSpan(), stream);

        WriteDictionary(ref writer, dictionary);

        if (writer.TotalWritten == writer.WrittenCount)
        {
            var count = writer.WrittenCount;
            Pool.MemoryStream.Return(stream);
            return (packet.Slice(0, count) as IPacket)!;
        }

        writer.Dispose();
        packet.Dispose();

        stream.Position = 0;
        return new OwnerPacket(stream);
    }

    private IDictionary<String, Object?> Decode(IPacket packet)
    {
        var reader = new SpanReader(packet);
        return ReadDictionary(ref reader);
    }

    private void WriteDictionary(ref SpanWriter writer, IDictionary<String, Object?> dictionary)
    {
        WriteLength(ref writer, dictionary.Count);

        foreach (var item in dictionary)
        {
            writer.Write(item.Key);
            WriteValue(ref writer, item.Value);
        }
    }

    private IDictionary<String, Object?> ReadDictionary(ref SpanReader reader)
    {
        var count = ReadLength(ref reader);
        IDictionary<String, Object?> dictionary = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadString();
            dictionary[key] = ReadValue(ref reader);
        }

        return dictionary;
    }

    private void WriteList(ref SpanWriter writer, IList list)
    {
        WriteLength(ref writer, list.Count);

        foreach (var item in list)
        {
            WriteValue(ref writer, item);
        }
    }

    private IList<Object?> ReadList(ref SpanReader reader)
    {
        var count = ReadLength(ref reader);
        IList<Object?> list = new List<Object?>(count);

        for (var i = 0; i < count; i++)
        {
            list.Add(ReadValue(ref reader));
        }

        return list;
    }

    private void WriteValue(ref SpanWriter writer, Object? value)
    {
        switch (value)
        {
            case null:
                writer.Write((Byte)ValueKind.Null);
                return;
            case Boolean booleanValue:
                writer.Write((Byte)(booleanValue ? ValueKind.BooleanTrue : ValueKind.BooleanFalse));
                return;
            case Byte byteValue:
                writer.Write((Byte)ValueKind.Byte);
                writer.Write(byteValue);
                return;
            case SByte sbyteValue:
                writer.Write((Byte)ValueKind.SByte);
                writer.Write(unchecked((Byte)sbyteValue));
                return;
            case Int16 int16Value:
                writer.Write((Byte)ValueKind.Int16);
                writer.Write(int16Value);
                return;
            case UInt16 uint16Value:
                writer.Write((Byte)ValueKind.UInt16);
                writer.Write(uint16Value);
                return;
            case Int32 int32Value:
                writer.Write((Byte)ValueKind.Int32);
                writer.Write(int32Value);
                return;
            case UInt32 uint32Value:
                writer.Write((Byte)ValueKind.UInt32);
                writer.Write(uint32Value);
                return;
            case Int64 int64Value:
                writer.Write((Byte)ValueKind.Int64);
                writer.Write(int64Value);
                return;
            case UInt64 uint64Value:
                writer.Write((Byte)ValueKind.UInt64);
                writer.Write(uint64Value);
                return;
            case Single singleValue:
                writer.Write((Byte)ValueKind.Single);
                writer.Write(singleValue);
                return;
            case Double doubleValue:
                writer.Write((Byte)ValueKind.Double);
                writer.Write(doubleValue);
                return;
            case Decimal decimalValue:
                writer.Write((Byte)ValueKind.Decimal);
                WriteDecimal(ref writer, decimalValue);
                return;
            case String stringValue:
                writer.Write((Byte)ValueKind.String);
                writer.Write(stringValue);
                return;
            case DateTime dateTimeValue:
                writer.Write((Byte)ValueKind.DateTime);
                writer.Write(dateTimeValue.ToBinary());
                return;
            case Guid guidValue:
                writer.Write((Byte)ValueKind.Guid);
                writer.Write(guidValue.ToByteArray());
                return;
            case Byte[] buffer:
                writer.Write((Byte)ValueKind.ByteArray);
                WriteBuffer(ref writer, buffer);
                return;
            case IDictionary<String, Object?> dictionary:
                writer.Write((Byte)ValueKind.Dictionary);
                WriteDictionary(ref writer, dictionary);
                return;
            case IDictionarySource dictionarySource:
                writer.Write((Byte)ValueKind.Dictionary);
                WriteDictionary(ref writer, dictionarySource.ToDictionary());
                return;
            case IList list:
                writer.Write((Byte)ValueKind.List);
                WriteList(ref writer, list);
                return;
            case TimeSpan timeSpanValue:
                writer.Write((Byte)ValueKind.TimeSpan);
                writer.Write(timeSpanValue.Ticks);
                return;
            case DateTimeOffset dateTimeOffsetValue:
                writer.Write((Byte)ValueKind.DateTimeOffset);
                writer.Write(dateTimeOffsetValue.Ticks);
                writer.Write(dateTimeOffsetValue.Offset.Ticks);
                return;
            default:
                throw new NotSupportedException($"Type {value.GetType().FullName} is not supported by Pek.AOT BinaryCodec2. Supported values are scalar primitives, String, DateTime, DateTimeOffset, Guid, TimeSpan, Byte[], IDictionary<String, Object?> and IList.");
        }
    }

    private Object? ReadValue(ref SpanReader reader)
    {
        var kind = (ValueKind)reader.ReadByte();

        return kind switch
        {
            ValueKind.Null => null,
            ValueKind.BooleanFalse => false,
            ValueKind.BooleanTrue => true,
            ValueKind.Byte => reader.ReadByte(),
            ValueKind.SByte => unchecked((SByte)reader.ReadByte()),
            ValueKind.Int16 => reader.ReadInt16(),
            ValueKind.UInt16 => reader.ReadUInt16(),
            ValueKind.Int32 => reader.ReadInt32(),
            ValueKind.UInt32 => reader.ReadUInt32(),
            ValueKind.Int64 => reader.ReadInt64(),
            ValueKind.UInt64 => reader.ReadUInt64(),
            ValueKind.Single => reader.ReadSingle(),
            ValueKind.Double => reader.ReadDouble(),
            ValueKind.Decimal => ReadDecimal(ref reader),
            ValueKind.String => reader.ReadString(),
            ValueKind.DateTime => DateTime.FromBinary(reader.ReadInt64()),
            ValueKind.Guid => new Guid(ReadByteArray(ref reader)),
            ValueKind.ByteArray => ReadByteArray(ref reader),
            ValueKind.Dictionary => ReadDictionary(ref reader),
            ValueKind.List => ReadList(ref reader),
            ValueKind.TimeSpan => TimeSpan.FromTicks(reader.ReadInt64()),
            ValueKind.DateTimeOffset => new DateTimeOffset(reader.ReadInt64(), new TimeSpan(reader.ReadInt64())),
            _ => throw new NotSupportedException($"Value kind {kind} is not supported by Pek.AOT BinaryCodec2."),
        };
    }

    private void WriteLength(ref SpanWriter writer, Int32 value)
    {
        if (EncodedInt)
            writer.WriteEncodedInt(value);
        else
            writer.Write(value);
    }

    private Int32 ReadLength(ref SpanReader reader) => EncodedInt ? reader.ReadEncodedInt() : reader.ReadInt32();

    private void WriteBuffer(ref SpanWriter writer, Byte[] buffer)
    {
        WriteLength(ref writer, buffer.Length);
        if (buffer.Length > 0) writer.Write(buffer);
    }

    private Byte[] ReadByteArray(ref SpanReader reader)
    {
        var length = ReadLength(ref reader);
        if (length <= 0) return [];

        return reader.ReadBytes(length).ToArray();
    }

    private static void WriteDecimal(ref SpanWriter writer, Decimal value)
    {
        var bits = Decimal.GetBits(value);
        writer.Write(bits[0]);
        writer.Write(bits[1]);
        writer.Write(bits[2]);
        writer.Write(bits[3]);
    }

    private static Decimal ReadDecimal(ref SpanReader reader) => new([reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()]);

    private static IDictionary<String, Object?> ToNullableDictionary(IDictionary<String, Object> source)
    {
        IDictionary<String, Object?> dictionary = new NullableDictionary<String, Object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            dictionary[item.Key] = item.Value;
        }

        return dictionary;
    }
}