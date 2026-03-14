using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Pek.Buffers;
using Pek.Collections;
using Pek.Extension;

namespace Pek.Data;

/// <summary>数据包接口。基于内存共享理念，统一提供数据包处理能力</summary>
public interface IPacket
{
    /// <summary>数据长度。仅当前数据包，不包括 Next</summary>
    Int32 Length { get; }

    /// <summary>下一个链式包</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    IPacket? Next { get; set; }

    /// <summary>总长度。包括 Next 链的长度</summary>
    Int32 Total { get; }

    /// <summary>获取或设置指定绝对位置的字节</summary>
    Byte this[Int32 index] { get; set; }

    /// <summary>获取当前片段 Span</summary>
    Span<Byte> GetSpan();

    /// <summary>获取当前片段 Memory</summary>
    Memory<Byte> GetMemory();

    /// <summary>切片得到新数据包</summary>
    IPacket Slice(Int32 offset, Int32 count = -1);

    /// <summary>切片得到新数据包，可选择转移所有权</summary>
    IPacket Slice(Int32 offset, Int32 count, Boolean transferOwner);

    /// <summary>尝试获取当前片段数组段</summary>
    Boolean TryGetArray(out ArraySegment<Byte> segment);
}

/// <summary>拥有管理权的数据包。使用完以后需要释放</summary>
public interface IOwnerPacket : IPacket, IDisposable;

/// <summary>数据包辅助扩展方法</summary>
public static class PacketHelper
{
    /// <summary>将字节数组包装为数据包</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPacket AsPacket(this Byte[] data) => new(data);

    /// <summary>将字节数组的指定区域包装为数据包</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPacket AsPacket(this Byte[] data, Int32 offset, Int32 count = -1) => new(data, offset, count);

    /// <summary>将数组段包装为数据包</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayPacket AsPacket(this ArraySegment<Byte> segment) => new(segment);

    /// <summary>将数据包追加到当前包链末尾</summary>
    public static IPacket Append(this IPacket packet, IPacket next)
    {
        if (next == null) return packet;
        if (ReferenceEquals(packet, next)) return packet;

        var current = packet;
        while (current.Next != null)
        {
            if (ReferenceEquals(current.Next, packet)) break;
            current = current.Next;
        }

        current.Next = next;
        return packet;
    }

    /// <summary>将字节数组作为新包追加到末尾</summary>
    public static IPacket Append(this IPacket packet, Byte[] data) => Append(packet, new ArrayPacket(data));

    /// <summary>转换为字符串</summary>
    public static String ToStr(this IPacket packet, Encoding? encoding = null, Int32 offset = 0, Int32 count = -1)
    {
        if (packet == null) return null!;

        if (offset < 0) offset = 0;
        if (count == 0) return String.Empty;

        var total = packet.Total;
        if (total == 0 || offset >= total) return String.Empty;

        if (packet.Next == null)
        {
            var length = packet.Length;
            if (offset >= length) return String.Empty;

            var actualCount = count < 0 || count > length - offset ? length - offset : count;
            return packet.GetSpan().Slice(offset, actualCount).ToStr(encoding);
        }

        var finalCount = count < 0 || count > total - offset ? total - offset : count;
        if (finalCount <= 0) return String.Empty;

        return ProcessMultiPacketString(packet, offset, finalCount, encoding);
    }

    /// <summary>处理多包链的字符串转换</summary>
    private static String ProcessMultiPacketString(IPacket packet, Int32 offset, Int32 count, Encoding? encoding)
    {
        var skip = offset;
        var remain = count;
        var builder = Pool.StringBuilder.Get();
        builder.EnsureCapacity(count);
        for (var current = packet; current != null && remain > 0; current = current.Next)
        {
            var span = current.GetSpan();
            if (skip >= span.Length)
            {
                skip -= span.Length;
                continue;
            }

            if (skip > 0)
            {
                span = span[skip..];
                skip = 0;
            }

            if (span.Length > remain) span = span[..remain];
            builder.Append(span.ToStr(encoding));
            remain -= span.Length;
        }

        return builder.Return(true);
    }

    /// <summary>转换为十六进制字符串</summary>
    public static String ToHex(this IPacket packet, Int32 maxLength = 32, String? separator = null, Int32 groupSize = 0)
    {
        if (packet == null) return null!;

        var total = packet.Total;
        if (total == 0 || maxLength == 0) return String.Empty;
        if (groupSize < 0) groupSize = 0;

        if (packet.Next == null)
            return packet.GetSpan().ToHex(separator, groupSize, maxLength);

        return ProcessMultiPacketHex(packet, maxLength, separator, groupSize);
    }

    /// <summary>处理多包链的十六进制转换</summary>
    private static String ProcessMultiPacketHex(IPacket packet, Int32 maxLength, String? separator, Int32 groupSize)
    {
        var builder = Pool.StringBuilder.Get();
        const String hexDigits = "0123456789ABCDEF";
        var writtenBytes = 0;

        for (var current = packet; current != null; current = current.Next)
        {
            var span = current.GetSpan();
            for (var i = 0; i < span.Length && (maxLength < 0 || writtenBytes < maxLength); i++)
            {
                if (writtenBytes > 0 && !separator.IsNullOrEmpty())
                {
                    if (groupSize <= 0 || writtenBytes % groupSize == 0) builder.Append(separator);
                }

                var value = span[i];
                builder.Append(hexDigits[value >> 4]);
                builder.Append(hexDigits[value & 0x0F]);
                writtenBytes++;
            }

            if (maxLength >= 0 && writtenBytes >= maxLength) break;
        }

        return builder.Return(true);
    }

    /// <summary>将数据包内容以文本形式写入 TextWriter</summary>
    public static void WriteTo(this IPacket packet, TextWriter writer, Encoding? encoding = null)
    {
        if (packet == null || writer == null) return;

        encoding ??= Encoding.UTF8;

#if NETCOREAPP || NETSTANDARD2_1
        const Int32 MaxStackAllocChars = 1024;
        Span<Char> stackChars = stackalloc Char[MaxStackAllocChars];
#endif

        for (var current = packet; current != null; current = current.Next)
        {
            var span = current.GetSpan();
            if (span.Length == 0) continue;

#if NETCOREAPP || NETSTANDARD2_1
            var charCount = encoding.GetCharCount(span);
            if (charCount <= MaxStackAllocChars)
            {
                var written = encoding.GetChars(span, stackChars);
                writer.Write(stackChars[..written]);
            }
            else
            {
                var chars = ArrayPool<Char>.Shared.Rent(charCount);
                try
                {
                    var written = encoding.GetChars(span, chars);
                    writer.Write(chars, 0, written);
                }
                finally
                {
                    ArrayPool<Char>.Shared.Return(chars);
                }
            }
#else
            if (current.TryGetArray(out var segment))
                writer.Write(encoding.GetChars(segment.Array!, segment.Offset, segment.Count));
            else
                writer.Write(encoding.GetString(span.ToArray()));
#endif
        }
    }

    /// <summary>将数据包内容复制到流</summary>
    public static void CopyTo(this IPacket packet, Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        for (var current = packet; current != null; current = current.Next)
        {
            if (current.TryGetArray(out var segment))
                stream.Write(segment.Array!, segment.Offset, segment.Count);
            else
                stream.Write(current.GetSpan());
        }
    }

    /// <summary>异步将数据包内容复制到流</summary>
    public static async Task CopyToAsync(this IPacket packet, Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        for (var current = packet; current != null; current = current.Next)
        {
            if (current.TryGetArray(out var segment))
                await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
            else
                await stream.WriteAsync(current.GetMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>写入到目标流</summary>
    public static void WriteTo(this IPacket packet, Stream stream) => CopyTo(packet, stream);

    /// <summary>异步写入到目标流</summary>
    public static Task WriteToAsync(this IPacket packet, Stream stream, CancellationToken cancellationToken = default) => CopyToAsync(packet, stream, cancellationToken);

    /// <summary>获取包含数据包内容的内存流</summary>
    public static Stream GetStream(this IPacket packet) => GetStream(packet, true);

    /// <summary>获取包含数据包内容的内存流</summary>
    public static Stream GetStream(this IPacket packet, Boolean writable)
    {
        if (packet.Next == null && packet.TryGetArray(out var segment))
            return new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable);

        var stream = new MemoryStream(packet.Total);
        packet.CopyTo(stream);
        stream.Position = 0;
        return stream;
    }

    /// <summary>转换为数组段，多包时进行聚合复制</summary>
    public static ArraySegment<Byte> ToSegment(this IPacket packet)
    {
        if (packet.Next == null && packet.TryGetArray(out var segment)) return segment;

        var buffer = new Byte[packet.Total];
        var position = 0;
        for (var current = packet; current != null; current = current.Next)
        {
            var span = current.GetSpan();
            span.CopyTo(buffer.AsSpan(position));
            position += span.Length;
        }

        return new ArraySegment<Byte>(buffer, 0, position);
    }

    /// <summary>转换为数组段集合</summary>
    public static IList<ArraySegment<Byte>> ToSegments(this IPacket packet)
    {
        var segments = new List<ArraySegment<Byte>>(4);
        for (var current = packet; current != null; current = current.Next)
        {
            if (current.TryGetArray(out var segment))
                segments.Add(segment);
            else
                segments.Add(new ArraySegment<Byte>(current.GetSpan().ToArray(), 0, current.Length));
        }

        return segments;
    }

    /// <summary>转换为字节数组，始终返回新数组副本</summary>
    public static Byte[] ToArray(this IPacket packet)
    {
        if (packet == null) return [];
        if (packet.Next == null) return packet.GetSpan().ToArray();

        var buffer = new Byte[packet.Total];
        var position = 0;
        for (var current = packet; current != null; current = current.Next)
        {
            var span = current.GetSpan();
            span.CopyTo(buffer.AsSpan(position));
            position += span.Length;
        }

        return buffer;
    }

    /// <summary>读取指定数据区</summary>
    public static Byte[] ReadBytes(this IPacket packet, Int32 offset = 0, Int32 count = -1)
    {
        if (packet == null) return [];
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        if (packet.Next == null)
        {
            if (count < 0) count = packet.Length - offset;
            if (count <= 0) return [];

            if (packet.TryGetArray(out var segment))
            {
                if (offset == 0 && count == packet.Length && segment.Offset == 0 && segment.Count == segment.Array!.Length)
                    return segment.Array;

                var buffer = new Byte[count];
                Buffer.BlockCopy(segment.Array!, segment.Offset + offset, buffer, 0, count);
                return buffer;
            }

            return packet.GetSpan().Slice(offset, count).ToArray();
        }

        var total = packet.Total;
        if (count < 0) count = total - offset;
        if (offset + count > total) count = total - offset;
        if (count <= 0) return [];

        var result = new Byte[count];
        var skip = offset;
        var remain = count;
        var position = 0;
        for (var current = packet; current != null && remain > 0; current = current.Next)
        {
            var span = current.GetSpan();
            if (skip >= span.Length)
            {
                skip -= span.Length;
                continue;
            }

            if (skip > 0)
            {
                span = span[skip..];
                skip = 0;
            }

            var toCopy = Math.Min(span.Length, remain);
            span[..toCopy].CopyTo(result.AsSpan(position));
            position += toCopy;
            remain -= toCopy;
        }

        return result;
    }

    /// <summary>深度克隆数据包</summary>
    public static IPacket Clone(this IPacket packet)
    {
        var owner = new OwnerPacket(packet.Total);
        var destination = owner.GetSpan();
        var position = 0;
        for (var current = packet; current != null; current = current.Next)
        {
            var span = current.GetSpan();
            span.CopyTo(destination[position..]);
            position += span.Length;
        }

        return owner;
    }

    /// <summary>尝试获取 Span</summary>
    public static Boolean TryGetSpan(this IPacket packet, out Span<Byte> span)
    {
        if (packet.Next == null)
        {
            span = packet.GetSpan();
            return true;
        }

        span = default;
        return false;
    }

    /// <summary>尝试扩展头部空间</summary>
    [Obsolete("请改用 ExpandHeader，并确保根据返回结果继续使用新实例。")]
    public static Boolean TryExpandHeader(this IPacket packet, Int32 size, [NotNullWhen(true)] out IPacket? newPacket)
    {
        newPacket = null;

        if (packet is ArrayPacket arrayPacket && arrayPacket.Offset >= size)
        {
            newPacket = new ArrayPacket(arrayPacket.Buffer, arrayPacket.Offset - size, arrayPacket.Length + size) { Next = arrayPacket.Next };
            return true;
        }

        if (packet is OwnerPacket ownerPacket && ownerPacket.Offset >= size)
        {
            newPacket = new OwnerPacket(ownerPacket, size);
            return true;
        }

        return false;
    }

    /// <summary>扩展头部空间</summary>
    public static IPacket ExpandHeader(this IPacket? packet, Int32 size)
    {
        return packet switch
        {
            ArrayPacket arrayPacket when arrayPacket.Offset >= size =>
                new ArrayPacket(arrayPacket.Buffer, arrayPacket.Offset - size, arrayPacket.Length + size) { Next = arrayPacket.Next },
            OwnerPacket ownerPacket when ownerPacket.Offset >= size =>
                new OwnerPacket(ownerPacket, size),
            _ => new OwnerPacket(size) { Next = packet }
        };
    }
}

/// <summary>拥有管理权的数据包</summary>
public sealed class OwnerPacket : IPacket, IOwnerPacket
{
    private Byte[]? _buffer;
    private Int32 _offset;
    private Int32 _length;
    private Boolean _hasOwner;

    /// <summary>缓冲区数组</summary>
    public Byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(OwnerPacket));

    /// <summary>偏移</summary>
    public Int32 Offset => _offset;

    /// <summary>长度</summary>
    public Int32 Length => _length;

    /// <summary>下一个链式包</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IPacket? Next { get; set; }

    /// <summary>总长度</summary>
    public Int32 Total => _length + (Next?.Total ?? 0);

    /// <summary>获取或设置指定位置的字节</summary>
    public Byte this[Int32 index]
    {
        get => index switch
        {
            < 0 => throw new IndexOutOfRangeException(nameof(index)),
            var i when i < _length => Buffer[_offset + i],
            var i when Next != null => Next[i - _length],
            _ => throw new IndexOutOfRangeException(nameof(index))
        };
        set
        {
            switch (index)
            {
                case < 0:
                    throw new IndexOutOfRangeException(nameof(index));
                case var i when i < _length:
                    Buffer[_offset + i] = value;
                    break;
                case var i when Next != null:
                    Next[i - _length] = value;
                    break;
                default:
                    throw new IndexOutOfRangeException(nameof(index));
            }
        }
    }

    /// <summary>创建指定长度的内存包</summary>
    public OwnerPacket(Int32 length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        _buffer = ArrayPool<Byte>.Shared.Rent(length);
        _offset = 0;
        _length = length;
        _hasOwner = true;
    }

    /// <summary>创建内存包，使用现有缓冲区</summary>
    public OwnerPacket(Byte[] buffer, Int32 offset, Int32 length, Boolean hasOwner)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (offset + length > buffer.Length) throw new ArgumentOutOfRangeException(nameof(length));

        _buffer = buffer;
        _offset = offset;
        _length = length;
        _hasOwner = hasOwner;
    }

    /// <summary>基于现有实例创建扩展头部的新内存包</summary>
    public OwnerPacket(OwnerPacket owner, Int32 expandSize)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (expandSize < 0) throw new ArgumentOutOfRangeException(nameof(expandSize));
        if (owner._offset < expandSize) throw new ArgumentOutOfRangeException(nameof(expandSize));

        _buffer = owner._buffer;
        _offset = owner._offset - expandSize;
        _length = owner._length + expandSize;
        Next = owner.Next;
        _hasOwner = owner._hasOwner;
        owner._hasOwner = false;
    }

    /// <summary>从数据流创建内存包</summary>
    public OwnerPacket(Stream stream, Int32 reserve = 0)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (reserve < 0) throw new ArgumentOutOfRangeException(nameof(reserve));

        if (stream is MemoryStream memoryStream)
        {
#if !NET45
            if (memoryStream.TryGetBuffer(out var segment))
            {
                if (segment.Array == null) throw new InvalidDataException();

                _buffer = segment.Array;
                _offset = segment.Offset + (Int32)memoryStream.Position;
                _length = segment.Count - (Int32)memoryStream.Position;
                _hasOwner = false;
                return;
            }
#endif
        }

        var size = (Int32)(stream.Length - stream.Position);
        _buffer = ArrayPool<Byte>.Shared.Rent(reserve + size);
        var count = stream.Read(_buffer, reserve, size);
        _offset = 0;
        _length = count;
        _hasOwner = true;
        if (count > 0) stream.Seek(-count, SeekOrigin.Current);
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        if (!_hasOwner) return;

        _hasOwner = false;
        var buffer = _buffer;
        _buffer = null;
        if (buffer != null) ArrayPool<Byte>.Shared.Return(buffer);
        Next.TryDispose();
        Next = null;
    }

    /// <summary>立即放弃所有权</summary>
    public void Free()
    {
        _buffer = null;
        Next = null;
        _hasOwner = false;
    }

    /// <summary>获取当前片段 Span</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Byte> GetSpan() => new(Buffer, _offset, _length);

    /// <summary>获取当前片段 Memory</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<Byte> GetMemory() => new(Buffer, _offset, _length);

    /// <summary>尝试获取数组段</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Boolean TryGetArray(out ArraySegment<Byte> segment)
    {
        segment = new ArraySegment<Byte>(Buffer, _offset, _length);
        return true;
    }

    /// <summary>调整数据包有效长度</summary>
    public OwnerPacket Resize(Int32 size)
    {
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));

        if (Next == null)
        {
            if (size > Buffer.Length) throw new ArgumentOutOfRangeException(nameof(size));
            _length = size;
        }
        else
        {
            if (size >= _length) throw new NotSupportedException("Cannot increase size when Next segment exists");
            _length = size;
        }

        return this;
    }

    /// <summary>切片生成新数据包，默认转移所有权</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPacket Slice(Int32 offset, Int32 count = -1) => Slice(offset, count, true);

    /// <summary>切片生成新数据包</summary>
    public IPacket Slice(Int32 offset, Int32 count, Boolean transferOwner)
    {
        if (_buffer == null) throw new ObjectDisposedException(nameof(OwnerPacket));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count > Total - offset) throw new ArgumentOutOfRangeException(nameof(count));

        var startPosition = _offset + offset;
        var remainInCurrent = _length - offset;
        var hasOwnership = _hasOwner && transferOwner;

        if (Next == null)
        {
            if (transferOwner) _hasOwner = false;
            var actualCount = count < 0 || count > remainInCurrent ? remainInCurrent : count;
            return new OwnerPacket(_buffer, startPosition, actualCount, hasOwnership);
        }

        if (remainInCurrent <= 0) return Next!.Slice(offset - _length, count, transferOwner);

        if (transferOwner) _hasOwner = false;
        if (count < 0) return new OwnerPacket(_buffer, startPosition, remainInCurrent, hasOwnership) { Next = Next };
        if (count <= remainInCurrent) return new OwnerPacket(_buffer, startPosition, count, hasOwnership);

        return new OwnerPacket(_buffer, startPosition, remainInCurrent, hasOwnership)
        {
            Next = Next!.Slice(0, count - remainInCurrent, transferOwner)
        };
    }

    /// <summary>返回文本表示</summary>
    public override String ToString() => $"OwnerPacket[{_buffer?.Length ?? 0}]({_offset}, {_length})<{Total}>";
}

/// <summary>内存包</summary>
public struct MemoryPacket : IPacket
{
    private readonly Memory<Byte> _memory;
    private readonly Int32 _length;
    private readonly Byte[]? _cachedArray;
    private readonly Int32 _cachedOffset;

    /// <summary>内存</summary>
    public readonly Memory<Byte> Memory => _memory;

    /// <summary>数据长度</summary>
    public readonly Int32 Length => _length;

    /// <summary>下一个链式包</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IPacket? Next { get; set; }

    /// <summary>总长度</summary>
    public readonly Int32 Total => Length + (Next?.Total ?? 0);

    /// <summary>获取或设置指定位置的字节</summary>
    public Byte this[Int32 index]
    {
        get
        {
            var position = index - _length;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                return Next[position];
            }

            var array = _cachedArray;
            return array != null ? array[_cachedOffset + index] : _memory.Span[index];
        }
        set
        {
            var position = index - _length;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                Next[position] = value;
            }
            else
            {
                var array = _cachedArray;
                if (array != null)
                    array[_cachedOffset + index] = value;
                else
                    _memory.Span[index] = value;
            }
        }
    }

    /// <summary>实例化内存包</summary>
    public MemoryPacket(Memory<Byte> memory, Int32 length)
    {
        if (length < 0 || length > memory.Length) throw new ArgumentOutOfRangeException(nameof(length));

        _memory = memory;
        _length = length;
        Next = null;
        if (MemoryMarshal.TryGetArray((ReadOnlyMemory<Byte>)memory, out var segment))
        {
            _cachedArray = segment.Array;
            _cachedOffset = segment.Offset;
        }
        else
        {
            _cachedArray = null;
            _cachedOffset = 0;
        }
    }

    /// <summary>获取 Span</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<Byte> GetSpan() => _memory.Span[.._length];

    /// <summary>获取 Memory</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Memory<Byte> GetMemory() => _memory[.._length];

    IPacket IPacket.Slice(Int32 offset, Int32 count) => Slice(offset, count);
    IPacket IPacket.Slice(Int32 offset, Int32 count, Boolean transferOwner) => Slice(offset, count);

    /// <summary>切片得到新数据包</summary>
    public MemoryPacket Slice(Int32 offset, Int32 count = -1, Boolean transferOwner = false)
    {
        if (Next != null) throw new NotSupportedException("Slice with Next");

        var remain = _length - offset;
        if (count < 0 || count > remain) count = remain;
        if (offset == 0 && count == _length) return this;

        return offset == 0 ? new MemoryPacket(_memory, count) : new MemoryPacket(_memory[offset..], count);
    }

    /// <summary>尝试获取数组段</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Boolean TryGetArray(out ArraySegment<Byte> segment) => MemoryMarshal.TryGetArray((ReadOnlyMemory<Byte>)GetMemory(), out segment);

    /// <summary>返回文本表示</summary>
    public override readonly String ToString() => $"MemoryPacket[{_memory.Length}](0, {_length})<{Total}>";
}

/// <summary>字节数组包</summary>
public record struct ArrayPacket : IPacket
{
    private readonly Byte[] _buffer;
    private readonly Int32 _offset;
    private readonly Int32 _length;

    /// <summary>缓冲区</summary>
    public readonly Byte[] Buffer => _buffer;

    /// <summary>数据偏移</summary>
    public readonly Int32 Offset => _offset;

    /// <summary>数据长度</summary>
    public readonly Int32 Length => _length;

    /// <summary>下一个链式包</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IPacket? Next { get; set; }

    /// <summary>总长度</summary>
    public readonly Int32 Total => Length + (Next?.Total ?? 0);

    /// <summary>空数组</summary>
    public static ArrayPacket Empty = new([]);

    /// <summary>获取或设置指定位置的字节</summary>
    public Byte this[Int32 index]
    {
        readonly get
        {
            var position = index - _length;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                return Next[position];
            }

            return _buffer[_offset + index];
        }
        set
        {
            var position = index - _length;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                Next[position] = value;
            }
            else
            {
                _buffer[_offset + index] = value;
            }
        }
    }

    /// <summary>通过指定字节数组来实例化数据包</summary>
    public ArrayPacket(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (count < 0) count = buffer.Length - offset;

        _buffer = buffer;
        _offset = offset;
        _length = count;
        Next = null;
    }

    /// <summary>从数据流实例化</summary>
    public ArrayPacket(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        if (stream is MemoryStream memoryStream)
        {
#if !NET45
            if (memoryStream.TryGetBuffer(out var segment))
            {
                if (segment.Array == null) throw new InvalidDataException();

                _buffer = segment.Array;
                _offset = segment.Offset + (Int32)memoryStream.Position;
                _length = segment.Count - (Int32)memoryStream.Position;
                Next = null;
                return;
            }
#endif
        }

        var data = new Byte[stream.Length - stream.Position];
        var count = stream.Read(data, 0, data.Length);
        _buffer = data;
        _offset = 0;
        _length = count;
        Next = null;
        if (count > 0) stream.Seek(-count, SeekOrigin.Current);
    }

    /// <summary>从数组段实例化数据包</summary>
    public ArrayPacket(ArraySegment<Byte> segment) : this(segment.Array!, segment.Offset, segment.Count) { }

    /// <summary>获取 Span</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<Byte> GetSpan() => new(_buffer, _offset, _length);

    /// <summary>获取 Memory</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Memory<Byte> GetMemory() => new(_buffer, _offset, _length);

    IPacket IPacket.Slice(Int32 offset, Int32 count)
    {
        if (count == 0) return Empty;

        var remain = _length - offset;
        var next = Next;
        if (next != null && remain <= 0) return next.Slice(offset - _length, count, true);

        return Slice(offset, count, true);
    }

    IPacket IPacket.Slice(Int32 offset, Int32 count, Boolean transferOwner)
    {
        if (count == 0) return Empty;

        var remain = _length - offset;
        var next = Next;
        if (next != null && remain <= 0) return next.Slice(offset - _length, count, transferOwner);

        return Slice(offset, count, transferOwner);
    }

    /// <summary>切片得到新数据包</summary>
    public ArrayPacket Slice(Int32 offset, Int32 count = -1, Boolean transferOwner = false)
    {
        if (count == 0) return Empty;

        var start = Offset + offset;
        var remain = _length - offset;
        var next = Next;
        if (next == null)
        {
            if (count < 0 || count > remain) count = remain;
            return count <= 0 ? Empty : new ArrayPacket(_buffer, start, count);
        }

        if (remain <= 0) return (ArrayPacket)next.Slice(offset - _length, count, transferOwner);
        if (count < 0) return new ArrayPacket(_buffer, start, remain) { Next = next };
        if (count <= remain) return new ArrayPacket(_buffer, start, count);

        return new ArrayPacket(_buffer, start, remain) { Next = next.Slice(0, count - remain, transferOwner) };
    }

    /// <summary>尝试获取数组段</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Boolean TryGetArray(out ArraySegment<Byte> segment)
    {
        segment = new ArraySegment<Byte>(_buffer, _offset, _length);
        return true;
    }

    /// <summary>字节数组隐式转换为数据包</summary>
    public static implicit operator ArrayPacket(Byte[] value) => new(value);

    /// <summary>数组段隐式转换为数据包</summary>
    public static implicit operator ArrayPacket(ArraySegment<Byte> value) => new(value.Array!, value.Offset, value.Count);

    /// <summary>字符串隐式转换为数据包</summary>
    public static implicit operator ArrayPacket(String value) => new(value.GetBytes());

    /// <summary>返回文本表示</summary>
    public override readonly String ToString() => $"ArrayPacket[{_buffer.Length}]({_offset}, {_length})<{Total}>";
}

/// <summary>只读数据包</summary>
public readonly record struct ReadOnlyPacket : IPacket
{
    private readonly Byte[] _buffer;
    private readonly Int32 _offset;
    private readonly Int32 _length;

    /// <summary>缓冲区</summary>
    public Byte[] Buffer => _buffer;

    /// <summary>数据偏移</summary>
    public Int32 Offset => _offset;

    /// <summary>数据长度</summary>
    public Int32 Length => _length;

    [EditorBrowsable(EditorBrowsableState.Never)]
    IPacket? IPacket.Next { get => null; set { } }

    /// <summary>总长度</summary>
    public Int32 Total => _length;

    /// <summary>空数据包</summary>
    public static ReadOnlyPacket Empty { get; } = new([]);

    /// <summary>获取指定位置的字节</summary>
    public Byte this[Int32 index]
    {
        get
        {
            if (index < 0 || index >= _length) throw new IndexOutOfRangeException(nameof(index));
            return _buffer[_offset + index];
        }
        set => throw new NotSupportedException("ReadOnlyPacket does not support modification");
    }

    /// <summary>通过字节数组实例化只读数据包</summary>
    public ReadOnlyPacket(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0) count = buffer.Length - offset;
        if (offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

        _buffer = buffer;
        _offset = offset;
        _length = count;
    }

    /// <summary>从数组段实例化只读数据包</summary>
    public ReadOnlyPacket(ArraySegment<Byte> segment) : this(segment.Array!, segment.Offset, segment.Count) { }

    /// <summary>从 IPacket 创建只读副本</summary>
    public ReadOnlyPacket(IPacket packet) : this(packet.ToArray()) { }

    /// <summary>获取 Span</summary>
    public Span<Byte> GetSpan() => new(_buffer, _offset, _length);

    /// <summary>获取 Memory</summary>
    public Memory<Byte> GetMemory() => new(_buffer, _offset, _length);

    IPacket IPacket.Slice(Int32 offset, Int32 count) => Slice(offset, count);
    IPacket IPacket.Slice(Int32 offset, Int32 count, Boolean transferOwner) => Slice(offset, count);

    /// <summary>切片得到新的只读数据包</summary>
    public ReadOnlyPacket Slice(Int32 offset, Int32 count = -1)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        var newOffset = _offset + offset;
        var remain = _length - offset;
        if (count < 0 || count > remain) count = remain;
        if (count < 0) count = 0;

        return new ReadOnlyPacket(_buffer, newOffset, count);
    }

    /// <summary>尝试获取数组段</summary>
    public Boolean TryGetArray(out ArraySegment<Byte> segment)
    {
        segment = new ArraySegment<Byte>(_buffer, _offset, _length);
        return true;
    }

    /// <summary>转换为字节数组</summary>
    public Byte[] ToArray()
    {
        if (_offset == 0 && _length == _buffer.Length) return _buffer;
        return GetSpan().ToArray();
    }

    /// <summary>从字节数组隐式转换</summary>
    public static implicit operator ReadOnlyPacket(Byte[] buffer) => new(buffer);

    /// <summary>从数组段隐式转换</summary>
    public static implicit operator ReadOnlyPacket(ArraySegment<Byte> segment) => new(segment);

    /// <summary>返回文本表示</summary>
    public override String ToString() => $"ReadOnlyPacket[{_buffer.Length}]({_offset}, {_length})<{Total}>";
}
