using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;

namespace Pek.Buffers;

/// <summary>Span写入器</summary>
public ref struct SpanWriter
{
    private Span<Byte> _span;
    private Int32 _index;
    private Stream? _stream;
    private Int32 _total;

    /// <summary>数据片段</summary>
    public readonly Span<Byte> Span => _span;

    /// <summary>已写入字节数</summary>
    public Int32 Position { readonly get => _index; set => _index = value; }

    /// <summary>总容量</summary>
    public readonly Int32 Capacity => _span.Length;

    /// <summary>空闲容量</summary>
    public readonly Int32 FreeCapacity => _span.Length - _index;

    /// <summary>已写入数据</summary>
    public readonly ReadOnlySpan<Byte> WrittenSpan => _span[.._index];

    /// <summary>已写入长度</summary>
    public readonly Int32 WrittenCount => _index;

    /// <summary>是否小端字节序。默认true</summary>
    public Boolean IsLittleEndian { get; set; } = true;

    /// <summary>已写入的总字节数</summary>
    public readonly Int32 TotalWritten => _total + _index;

    /// <summary>实例化Span写入器</summary>
    public SpanWriter(Span<Byte> buffer) => _span = buffer;

    /// <summary>实例化Span写入器</summary>
    public SpanWriter(IPacket data) : this(data.GetSpan()) { }

    /// <summary>实例化Span写入器</summary>
    public SpanWriter(Byte[] buffer, Int32 offset = 0, Int32 count = -1) : this(new Span<Byte>(buffer, offset, count < 0 ? buffer.Length - offset : count)) { }

    /// <summary>实例化Span写入器，支持自动 Flush 到流</summary>
    public SpanWriter(Span<Byte> span, Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        _span = span;
        _stream = stream;
    }

    /// <summary>将缓冲区中已写入的数据刷入底层流，并重置写入位置</summary>
    public void Flush()
    {
        if (_stream == null || _index <= 0) return;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        _stream.Write(_span[.._index]);
#else
        _stream.Write(_span[.._index].ToArray(), 0, _index);
#endif
        _total += _index;
        _index = 0;
    }

    /// <summary>释放资源。Flush 剩余数据到流</summary>
    public void Dispose()
    {
        Flush();
        _stream = null;
    }

    /// <summary>告知有多少数据已写入缓冲区</summary>
    public void Advance(Int32 count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
        if (_index + count > _span.Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Exceeds buffer capacity.");

        _index += count;
    }

    /// <summary>返回要写入到的 Span</summary>
    public Span<Byte> GetSpan(Int32 sizeHint = 0)
    {
        if (sizeHint > 0) EnsureSpace(sizeHint);

        return _span[_index..];
    }

    /// <summary>确保缓冲区中有足够的空间</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureSpace(Int32 size)
    {
        if (_index + size > _span.Length)
            FlushAndGrow(size);
    }

    /// <summary>Flush 当前数据到流</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FlushAndGrow(Int32 size)
    {
        if (_stream == null)
            throw new InvalidOperationException($"Not enough space to write {size} bytes. Available: {FreeCapacity}");

        Flush();

        if (size > _span.Length)
            throw new InvalidOperationException($"Single write of {size} bytes exceeds buffer capacity {_span.Length}. Use Write(ReadOnlySpan<Byte>) for large data.");
    }

    /// <summary>写入字节</summary>
    public Int32 WriteByte(Int32 value) => Write((Byte)value);

    /// <summary>写入字节</summary>
    public Int32 Write(Byte value)
    {
        const Int32 size = sizeof(Byte);
        EnsureSpace(size);
        _span[_index] = value;
        _index += size;
        return size;
    }

    /// <summary>写入 16 位整数</summary>
    public Int32 Write(Int16 value)
    {
        const Int32 size = sizeof(Int16);
        EnsureSpace(size);
        if (IsLittleEndian)
            BinaryPrimitives.WriteInt16LittleEndian(_span[_index..], value);
        else
            BinaryPrimitives.WriteInt16BigEndian(_span[_index..], value);
        _index += size;
        return size;
    }

    /// <summary>写入无符号 16 位整数</summary>
    public Int32 Write(UInt16 value)
    {
        const Int32 size = sizeof(UInt16);
        EnsureSpace(size);
        if (IsLittleEndian)
            BinaryPrimitives.WriteUInt16LittleEndian(_span[_index..], value);
        else
            BinaryPrimitives.WriteUInt16BigEndian(_span[_index..], value);
        _index += size;
        return size;
    }

    /// <summary>写入 32 位整数</summary>
    public Int32 Write(Int32 value)
    {
        const Int32 size = sizeof(Int32);
        EnsureSpace(size);
        if (IsLittleEndian)
            BinaryPrimitives.WriteInt32LittleEndian(_span[_index..], value);
        else
            BinaryPrimitives.WriteInt32BigEndian(_span[_index..], value);
        _index += size;
        return size;
    }

    /// <summary>写入无符号 32 位整数</summary>
    public Int32 Write(UInt32 value)
    {
        const Int32 size = sizeof(UInt32);
        EnsureSpace(size);
        if (IsLittleEndian)
            BinaryPrimitives.WriteUInt32LittleEndian(_span[_index..], value);
        else
            BinaryPrimitives.WriteUInt32BigEndian(_span[_index..], value);
        _index += size;
        return size;
    }

    /// <summary>写入 64 位整数</summary>
    public Int32 Write(Int64 value)
    {
        const Int32 size = sizeof(Int64);
        EnsureSpace(size);
        if (IsLittleEndian)
            BinaryPrimitives.WriteInt64LittleEndian(_span[_index..], value);
        else
            BinaryPrimitives.WriteInt64BigEndian(_span[_index..], value);
        _index += size;
        return size;
    }

    /// <summary>写入无符号 64 位整数</summary>
    public Int32 Write(UInt64 value)
    {
        const Int32 size = sizeof(UInt64);
        EnsureSpace(size);
        if (IsLittleEndian)
            BinaryPrimitives.WriteUInt64LittleEndian(_span[_index..], value);
        else
            BinaryPrimitives.WriteUInt64BigEndian(_span[_index..], value);
        _index += size;
        return size;
    }

    /// <summary>写入单精度浮点数</summary>
    public unsafe Int32 Write(Single value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return Write(BitConverter.SingleToInt32Bits(value));
#else
        return Write(*(Int32*)&value);
#endif
    }

    /// <summary>写入双精度浮点数</summary>
    public unsafe Int32 Write(Double value)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return Write(BitConverter.DoubleToInt64Bits(value));
#else
        return Write(*(Int64*)&value);
#endif
    }

    /// <summary>写入字符串</summary>
    public Int32 Write(String? value, Int32 length = 0, Encoding? encoding = null)
    {
        var position = _index;
        encoding ??= Encoding.UTF8;

        if (value.IsNullOrEmpty())
        {
            if (length == 0)
                WriteEncodedInt(0);
            else if (length > 0)
            {
                EnsureSpace(length);
                _span.Slice(_index, length).Clear();
                _index += length;
            }

            return _index - position;
        }

        return length switch
        {
            < 0 => WriteStringAll(value, encoding, position),
            0 => WriteStringWithLength(value, encoding, position),
            _ => WriteStringFixed(value, length, encoding, position)
        };
    }

    /// <summary>写入字符串全部内容</summary>
    private Int32 WriteStringAll(String value, Encoding encoding, Int32 startPos)
    {
        var byteCount = encoding.GetByteCount(value);
        EnsureSpace(byteCount);

        var count = encoding.GetBytes(value.AsSpan(), _span[_index..]);
        _index += count;

        return _index - startPos;
    }

    /// <summary>写入带长度前缀的字符串</summary>
    private Int32 WriteStringWithLength(String value, Encoding encoding, Int32 startPos)
    {
        var byteCount = encoding.GetByteCount(value);
        WriteEncodedInt(byteCount);
        EnsureSpace(byteCount);

        var count = encoding.GetBytes(value.AsSpan(), _span[_index..]);
        _index += count;

        return _index - startPos;
    }

    /// <summary>写入固定长度字符串</summary>
    private Int32 WriteStringFixed(String value, Int32 length, Encoding encoding, Int32 startPos)
    {
        var span = GetSpan(length);
        if (span.Length > length) span = span[..length];

        var source = value.AsSpan();
        var need = encoding.GetByteCount(value);
        if (need <= length)
        {
            var count = encoding.GetBytes(source, span);
            if (count < length) span[count..length].Clear();
        }
        else
        {
            var buffer = Pool.Shared.Rent(need);
            try
            {
                var count = encoding.GetBytes(source, buffer);
                new ReadOnlySpan<Byte>(buffer, 0, length).CopyTo(span);
            }
            finally
            {
                Pool.Shared.Return(buffer);
            }
        }

        _index += length;
        return length;
    }

    /// <summary>写入字节数组</summary>
    public Int32 Write(Byte[]? value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        return Write((ReadOnlySpan<Byte>)value);
    }

    /// <summary>写入Span</summary>
    public Int32 Write(ReadOnlySpan<Byte> span)
    {
        if (_stream != null && span.Length > FreeCapacity)
        {
            var remaining = span;
            while (remaining.Length > 0)
            {
                if (FreeCapacity <= 0) Flush();
                var count = Math.Min(remaining.Length, FreeCapacity);
                remaining[..count].CopyTo(_span[_index..]);
                _index += count;
                remaining = remaining[count..];
            }

            return span.Length;
        }

        EnsureSpace(span.Length);
        span.CopyTo(_span[_index..]);
        _index += span.Length;

        return span.Length;
    }

    /// <summary>写入Span</summary>
    public Int32 Write(Span<Byte> span) => Write((ReadOnlySpan<Byte>)span);

    /// <summary>写入数据包</summary>
    public Int32 Write(IPacket value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));

        var total = 0;
        for (var packet = value; packet != null; packet = packet.Next)
        {
            total += Write(packet.GetSpan());
        }

        return total;
    }

    /// <summary>写入结构体</summary>
    public Int32 Write<T>(T value) where T : struct
    {
        var size = Unsafe.SizeOf<T>();
        EnsureSpace(size);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(_span.Slice(_index, size), in value);
#else
        MemoryMarshal.Write(_span.Slice(_index, size), ref value);
#endif
        _index += size;
        return size;
    }

    /// <summary>写入 7 位压缩编码的 32 位整数</summary>
    public Int32 WriteEncodedInt(Int32 value)
    {
        var number = (UInt32)value;
        var size = number < 0x80 ? 1 : number < 0x4000 ? 2 : number < 0x20_0000 ? 3 : number < 0x1000_0000 ? 4 : 5;
        EnsureSpace(size);

        var span = _span[_index..];
        var count = 0;
        while (number >= 0x80)
        {
            span[count++] = (Byte)(number | 0x80);
            number >>= 7;
        }

        span[count++] = (Byte)number;
        _index += count;
        return count;
    }

    /// <summary>填充指定字节值</summary>
    public Int32 Fill(Byte value, Int32 count)
    {
        if (count <= 0) return 0;

        EnsureSpace(count);
        _span.Slice(_index, count).Fill(value);
        _index += count;

        return count;
    }

    /// <summary>填充零字节</summary>
    public Int32 FillZero(Int32 count)
    {
        if (count <= 0) return 0;

        EnsureSpace(count);
        _span.Slice(_index, count).Clear();
        _index += count;

        return count;
    }

    /// <summary>重复写入数据片段</summary>
    public Int32 WriteRepeat(ReadOnlySpan<Byte> data, Int32 repeat)
    {
        if (repeat <= 0 || data.IsEmpty) return 0;

        var total = data.Length * repeat;
        EnsureSpace(total);

        for (var i = 0; i < repeat; i++)
        {
            data.CopyTo(_span.Slice(_index));
            _index += data.Length;
        }

        return total;
    }

    /// <summary>写入长度前缀</summary>
    private Int32 WriteLength(Int32 length, Int32 sizeOf) => sizeOf switch
    {
        0 => WriteEncodedInt(length),
        1 => Write((Byte)length),
        2 => Write((UInt16)length),
        4 => Write(length),
        _ => throw new ArgumentOutOfRangeException(nameof(sizeOf), $"Unsupported length size: {sizeOf}. Use 0/1/2/4.")
    };

    /// <summary>写入长度前缀的二进制数据</summary>
    public Int32 WriteArray(ReadOnlySpan<Byte> value, Int32 sizeOf = 2)
    {
        var count = WriteLength(value.Length, sizeOf);
        if (value.Length > 0) count += Write(value);
        return count;
    }

    /// <summary>写入长度前缀的字符串</summary>
    public Int32 WriteLengthString(String? value, Int32 sizeOf = 2, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (value.IsNullOrEmpty())
            return WriteLength(0, sizeOf);

        var byteCount = encoding.GetByteCount(value);
        var count = WriteLength(byteCount, sizeOf);
        EnsureSpace(byteCount);

        var written = encoding.GetBytes(value.AsSpan(), _span[_index..]);
        _index += written;

        return count + written;
    }
}