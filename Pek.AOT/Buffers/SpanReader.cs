using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Pek.Data;
using Pek.Extension;
using Pek.IO;

namespace Pek.Buffers;

/// <summary>Span读取器</summary>
public ref struct SpanReader
{
    private ReadOnlySpan<Byte> _span;
    private Int32 _index;
    private Stream? _stream;
    private readonly Int32 _bufferSize;
    private IPacket? _data;
    private Int32 _total;

    /// <summary>数据片段</summary>
    public readonly ReadOnlySpan<Byte> Span => _span;

    /// <summary>已读取字节数</summary>
    public Int32 Position { readonly get => _index; set => _index = value; }

    /// <summary>当前缓冲总容量</summary>
    public readonly Int32 Capacity => _span.Length;

    /// <summary>空闲容量</summary>
    [Obsolete("=>Available")]
    public readonly Int32 FreeCapacity => _span.Length - _index;

    /// <summary>空闲容量</summary>
    public readonly Int32 Available => _span.Length - _index;

    /// <summary>是否小端字节序。默认 true</summary>
    public Boolean IsLittleEndian { get; set; } = true;

    /// <summary>最大容量</summary>
    public Int32 MaxCapacity { get; set; }

    /// <summary>实例化读取器</summary>
    public SpanReader(ReadOnlySpan<Byte> span) => _span = span;

    /// <summary>实例化读取器</summary>
    public SpanReader(Span<Byte> span) => _span = span;

    /// <summary>实例化读取器</summary>
    public SpanReader(IPacket data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (data.Next != null)
        {
            _stream = data.GetStream(false);
            _bufferSize = 8192;
        }
        else
        {
            _data = data;
            _span = data.GetSpan();
            _total = data.Total;
        }
    }

    /// <summary>实例化读取器</summary>
    public SpanReader(Byte[] buffer, Int32 offset = 0, Int32 count = -1) : this(new ReadOnlySpan<Byte>(buffer, offset, count < 0 ? buffer.Length - offset : count)) { }

    /// <summary>实例化读取器，支持后续从流追加读取</summary>
    public SpanReader(Stream stream, IPacket? data = null, Int32 bufferSize = 8192)
    {
        _stream = stream;
        _bufferSize = bufferSize;

        if (data != null)
        {
            _data = data;
            _span = data.GetSpan();
            _total = data.Total;
        }
    }

    /// <summary>告知已消耗指定字节</summary>
    public void Advance(Int32 count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
        if (count > 0) EnsureSpace(count);
        if (_index + count > _span.Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Exceeds available data.");
        _index += count;
    }

    /// <summary>返回剩余可读数据片段</summary>
    public readonly ReadOnlySpan<Byte> GetSpan(Int32 sizeHint = 0)
    {
        if (_index + sizeHint > _span.Length)
            throw new ArgumentOutOfRangeException(nameof(sizeHint), "Size hint exceeds free capacity.");
        return _span[_index..];
    }

    /// <summary>确保缓冲区中有足够的可读取字节</summary>
    public void EnsureSpace(Int32 size)
    {
        if (size <= 0) return;

        var remain = Available;
        if (remain >= size) return;

        if (_stream != null)
        {
            var bufferSize = size;
            if (bufferSize < _bufferSize) bufferSize = _bufferSize;
            if (MaxCapacity > 0 && bufferSize > MaxCapacity - _total) bufferSize = MaxCapacity - _total;
            if (remain + bufferSize < size) throw new InvalidOperationException();

            var packet = new OwnerPacket(bufferSize);

            var available = 0;
            var old = _data;
            if (old != null && remain > 0)
            {
                if (!old.TryGetArray(out var segment))
                    throw new NotSupportedException("Data packet does not support array access.");

                segment.AsSpan(_index, remain).CopyTo(packet.Buffer);
                available += remain;
            }

            old.TryDispose();
            _data = packet;
            _index = 0;

            available = _stream.ReadAtLeast(packet.Buffer, packet.Offset + available, packet.Length - available, size - remain, false);
            if (remain + available < size)
                throw new InvalidOperationException($"Not enough data to read. Required: {size}, Available: {available}");
            packet.Resize(remain + available);

            _span = packet.GetSpan();
            _total += packet.Length - remain;
        }

        if (_index + size > _span.Length)
            throw new InvalidOperationException($"Not enough data to read. Required: {size}, Available: {Available}");
    }

    /// <summary>读取单个字节</summary>
    public Byte ReadByte()
    {
        const Int32 size = sizeof(Byte);
        EnsureSpace(size);
        var result = _span[_index];
        _index += size;
        return result;
    }

    /// <summary>读取Int16整数</summary>
    public Int16 ReadInt16()
    {
        const Int32 size = sizeof(Int16);
        EnsureSpace(size);
        var result = IsLittleEndian
            ? BinaryPrimitives.ReadInt16LittleEndian(_span.Slice(_index, size))
            : BinaryPrimitives.ReadInt16BigEndian(_span.Slice(_index, size));
        _index += size;
        return result;
    }

    /// <summary>读取UInt16整数</summary>
    public UInt16 ReadUInt16()
    {
        const Int32 size = sizeof(UInt16);
        EnsureSpace(size);
        var result = IsLittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(_span.Slice(_index, size))
            : BinaryPrimitives.ReadUInt16BigEndian(_span.Slice(_index, size));
        _index += size;
        return result;
    }

    /// <summary>读取Int32整数</summary>
    public Int32 ReadInt32()
    {
        const Int32 size = sizeof(Int32);
        EnsureSpace(size);
        var result = IsLittleEndian
            ? BinaryPrimitives.ReadInt32LittleEndian(_span.Slice(_index, size))
            : BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_index, size));
        _index += size;
        return result;
    }

    /// <summary>读取UInt32整数</summary>
    public UInt32 ReadUInt32()
    {
        const Int32 size = sizeof(UInt32);
        EnsureSpace(size);
        var result = IsLittleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(_span.Slice(_index, size))
            : BinaryPrimitives.ReadUInt32BigEndian(_span.Slice(_index, size));
        _index += size;
        return result;
    }

    /// <summary>读取Int64整数</summary>
    public Int64 ReadInt64()
    {
        const Int32 size = sizeof(Int64);
        EnsureSpace(size);
        var result = IsLittleEndian
            ? BinaryPrimitives.ReadInt64LittleEndian(_span.Slice(_index, size))
            : BinaryPrimitives.ReadInt64BigEndian(_span.Slice(_index, size));
        _index += size;
        return result;
    }

    /// <summary>读取UInt64整数</summary>
    public UInt64 ReadUInt64()
    {
        const Int32 size = sizeof(UInt64);
        EnsureSpace(size);
        var result = IsLittleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(_span.Slice(_index, size))
            : BinaryPrimitives.ReadUInt64BigEndian(_span.Slice(_index, size));
        _index += size;
        return result;
    }

    /// <summary>读取单精度浮点数</summary>
    public Single ReadSingle()
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return BitConverter.Int32BitsToSingle(ReadInt32());
#else
        var result = ReadInt32();
        return Unsafe.ReadUnaligned<Single>(ref Unsafe.As<Int32, Byte>(ref result));
#endif
    }

    /// <summary>读取双精度浮点数</summary>
    public Double ReadDouble()
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        return BitConverter.Int64BitsToDouble(ReadInt64());
#else
        var result = ReadInt64();
        return Unsafe.ReadUnaligned<Double>(ref Unsafe.As<Int64, Byte>(ref result));
#endif
    }

    /// <summary>读取字符串</summary>
    public String ReadString(Int32 length = 0, Encoding? encoding = null)
    {
        var actualLength = length switch
        {
            < 0 => _span.Length - _index,
            0 => ReadEncodedInt(),
            _ => length
        };

        if (actualLength == 0) return String.Empty;

        EnsureSpace(actualLength);

        encoding ??= Encoding.UTF8;
        var result = encoding.GetString(_span.Slice(_index, actualLength));
        _index += actualLength;
        return result;
    }

    /// <summary>读取字节数组片段</summary>
    public ReadOnlySpan<Byte> ReadBytes(Int32 length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");
        EnsureSpace(length);

        var result = _span.Slice(_index, length);
        _index += length;
        return result;
    }

    /// <summary>读取到目标 Span</summary>
    public Int32 Read(Span<Byte> data)
    {
        var length = data.Length;
        EnsureSpace(length);

        var result = _span.Slice(_index, length);
        result.CopyTo(data);
        _index += length;
        return length;
    }

    /// <summary>读取数据包</summary>
    public IPacket ReadPacket(Int32 length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");

        EnsureSpace(length);

        if (_data != null)
        {
            var result = _data.Slice(_index, length);
            _index += length;
            return result;
        }

        var packet = new OwnerPacket(length);
        _span.Slice(_index, length).CopyTo(packet.Buffer);
        packet.Resize(length);
        _index += length;
        return packet;
    }

    /// <summary>读取结构体</summary>
    public T Read<T>() where T : struct
    {
        var size = Unsafe.SizeOf<T>();
        EnsureSpace(size);

        var result = MemoryMarshal.Read<T>(_span.Slice(_index));
        _index += size;
        return result;
    }

    /// <summary>以 7 位压缩格式读取 32 位整数</summary>
    public Int32 ReadEncodedInt()
    {
        UInt32 result = 0;
        Byte shift = 0;

        while (true)
        {
            var value = ReadByte();
            result |= (UInt32)((value & 0x7f) << shift);
            if ((value & 0x80) == 0) break;

            shift += 7;
            if (shift >= 32)
                throw new FormatException("The number value is too large to read in compressed format!");
        }

        return (Int32)result;
    }

    /// <summary>读取长度前缀</summary>
    private Int32 ReadLength(Int32 sizeOf) => sizeOf switch
    {
        0 => ReadEncodedInt(),
        1 => ReadByte(),
        2 => ReadUInt16(),
        4 => ReadInt32(),
        _ => throw new ArgumentOutOfRangeException(nameof(sizeOf), $"Unsupported length size: {sizeOf}. Use 0/1/2/4.")
    };

    /// <summary>读取长度前缀的二进制数据</summary>
    public ReadOnlySpan<Byte> ReadArray(Int32 sizeOf = 2)
    {
        var length = ReadLength(sizeOf);
        if (length <= 0) return [];
        return ReadBytes(length);
    }

    /// <summary>读取长度前缀的字符串</summary>
    public String ReadLengthString(Int32 sizeOf = 2, Encoding? encoding = null)
    {
        var length = ReadLength(sizeOf);
        if (length <= 0) return String.Empty;

        EnsureSpace(length);

        encoding ??= Encoding.UTF8;
        var result = encoding.GetString(_span.Slice(_index, length));
        _index += length;
        return result;
    }
}