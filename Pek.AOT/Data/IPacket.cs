using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Pek.Collections;

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

    /// <summary>转为字符串</summary>
    public static String ToStr(this IPacket packet, Encoding? encoding = null, Int32 offset = 0, Int32 count = -1)
    {
        if (packet == null) return null!;

        encoding ??= Encoding.UTF8;
        if (offset < 0) offset = 0;
        if (count == 0) return String.Empty;
        if (packet.Total == 0 || offset >= packet.Total) return String.Empty;

        var data = packet.ReadBytes(offset, count);
        return encoding.GetString(data, 0, data.Length);
    }

    /// <summary>转换为十六进制字符串</summary>
    public static String ToHex(this IPacket packet, Int32 maxLength = 32, String? separator = null, Int32 groupSize = 0)
    {
        if (packet == null) return null!;
        if (packet.Total == 0 || maxLength == 0) return String.Empty;
        if (groupSize < 0) groupSize = 0;

        var data = packet.ReadBytes(0, maxLength);
        var builder = Pool.StringBuilder.Get();
        try
        {
            const String hex = "0123456789ABCDEF";
            for (var i = 0; i < data.Length; i++)
            {
                if (i > 0 && !String.IsNullOrEmpty(separator))
                {
                    if (groupSize <= 0 || i % groupSize == 0) builder.Append(separator);
                }

                var value = data[i];
                builder.Append(hex[value >> 4]);
                builder.Append(hex[value & 0x0F]);
            }

            if (maxLength >= 0 && packet.Total > maxLength) builder.Append("...");
            return builder.ToString();
        }
        finally
        {
            Pool.StringBuilder.Return(builder);
        }
    }

    /// <summary>写入到目标流</summary>
    public static void WriteTo(this IPacket packet, Stream stream)
    {
        if (packet == null || stream == null) return;

        for (var current = packet; current != null; current = current.Next)
        {
            if (current.TryGetArray(out var segment))
                stream.Write(segment.Array!, segment.Offset, segment.Count);
            else
                stream.Write(current.GetSpan());
        }
    }

    /// <summary>异步写入到目标流</summary>
    public static async Task WriteToAsync(this IPacket packet, Stream stream, CancellationToken cancellationToken = default)
    {
        if (packet == null || stream == null) return;

        for (var current = packet; current != null; current = current.Next)
        {
            if (current.TryGetArray(out var segment))
                await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
            else
                await stream.WriteAsync(current.GetMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>返回字节数组</summary>
    public static Byte[] ToArray(this IPacket packet)
    {
        if (packet == null) return [];
        if (packet.Next == null && packet.TryGetArray(out var segment))
        {
            var result = new Byte[segment.Count];
            Buffer.BlockCopy(segment.Array!, segment.Offset, result, 0, segment.Count);
            return result;
        }

        using var stream = Pool.MemoryStream.Get();
        packet.WriteTo(stream);
        return stream.Return(true);
    }

    /// <summary>读取指定数据区</summary>
    public static Byte[] ReadBytes(this IPacket packet, Int32 offset = 0, Int32 count = -1)
    {
        if (packet == null) return [];
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        var total = packet.Total;
        if (offset >= total) return [];
        if (count < 0 || count > total - offset) count = total - offset;
        if (count <= 0) return [];

        var result = new Byte[count];
        var written = 0;
        var skip = offset;
        for (var current = packet; current != null && written < count; current = current.Next)
        {
            var length = current.Length;
            if (skip >= length)
            {
                skip -= length;
                continue;
            }

            var span = current.GetSpan();
            if (skip > 0)
            {
                span = span[skip..];
                skip = 0;
            }

            var take = Math.Min(span.Length, count - written);
            span[..take].CopyTo(result.AsSpan(written));
            written += take;
        }

        return result;
    }

    /// <summary>获取数组段集合</summary>
    public static IList<ArraySegment<Byte>> ToSegments(this IPacket packet)
    {
        var list = new List<ArraySegment<Byte>>(4);
        for (var current = packet; current != null; current = current.Next)
        {
            if (current.TryGetArray(out var segment))
                list.Add(segment);
            else
                list.Add(new ArraySegment<Byte>(current.GetSpan().ToArray()));
        }

        return list;
    }

    /// <summary>尝试获取 Span</summary>
    public static Boolean TryGetSpan(this IPacket packet, out Span<Byte> span)
    {
        if (packet == null || packet.Next != null)
        {
            span = default;
            return false;
        }

        span = packet.GetSpan();
        return true;
    }
}

/// <summary>拥有管理权的数据包</summary>
public sealed class OwnerPacket : IOwnerPacket
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
        get
        {
            var position = index - _length;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                return Next[position];
            }

            return Buffer[_offset + index];
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
                Buffer[_offset + index] = value;
            }
        }
    }

    /// <summary>创建指定长度的内存包</summary>
    public OwnerPacket(Int32 length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        _buffer = ArrayPool<Byte>.Shared.Rent(length);
        _length = length;
        _hasOwner = true;
    }

    /// <summary>创建内存包，使用现有缓冲区</summary>
    public OwnerPacket(Byte[] buffer, Int32 offset, Int32 length, Boolean hasOwner = false)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException(nameof(length));

        _buffer = buffer;
        _offset = offset;
        _length = length;
        _hasOwner = hasOwner;
    }

    /// <summary>从数据流创建内存包</summary>
    public OwnerPacket(Stream stream, Int32 reserve = 0)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (reserve < 0) throw new ArgumentOutOfRangeException(nameof(reserve));

        var size = (Int32)(stream.Length - stream.Position);
        _buffer = ArrayPool<Byte>.Shared.Rent(reserve + size);
        var count = stream.Read(_buffer, reserve, size);
        _offset = 0;
        _length = reserve + count;
        _hasOwner = true;

        if (count > 0) stream.Seek(-count, SeekOrigin.Current);
    }

    /// <summary>获取当前片段 Span</summary>
    public Span<Byte> GetSpan() => new(Buffer, _offset, _length);

    /// <summary>获取当前片段 Memory</summary>
    public Memory<Byte> GetMemory() => new(Buffer, _offset, _length);

    /// <summary>尝试获取数组段</summary>
    public Boolean TryGetArray(out ArraySegment<Byte> segment)
    {
        segment = new ArraySegment<Byte>(Buffer, _offset, _length);
        return true;
    }

    /// <summary>调整有效长度</summary>
    public OwnerPacket Resize(Int32 size)
    {
        if (size < 0 || size > Buffer.Length - _offset) throw new ArgumentOutOfRangeException(nameof(size));
        _length = size;
        return this;
    }

    /// <summary>切片得到新数据包</summary>
    public IPacket Slice(Int32 offset, Int32 count = -1) => Slice(offset, count, true);

    /// <summary>切片得到新数据包，可选择转移所有权</summary>
    public IPacket Slice(Int32 offset, Int32 count, Boolean transferOwner)
    {
        if (_buffer == null) throw new ObjectDisposedException(nameof(OwnerPacket));
        if (offset < 0 || offset > Total) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count > Total - offset) throw new ArgumentOutOfRangeException(nameof(count));

        var start = _offset + offset;
        var remain = _length - offset;
        var hasOwner = _hasOwner && transferOwner;

        if (Next == null)
        {
            if (transferOwner) _hasOwner = false;
            if (count < 0 || count > remain) count = remain;
            return new OwnerPacket(Buffer, start, count, hasOwner);
        }

        if (remain <= 0) return Next!.Slice(offset - _length, count, transferOwner);

        if (transferOwner) _hasOwner = false;
        if (count < 0) return new OwnerPacket(Buffer, start, remain, hasOwner) { Next = Next };
        if (count <= remain) return new OwnerPacket(Buffer, start, count, hasOwner);

        return new OwnerPacket(Buffer, start, remain, hasOwner)
        {
            Next = Next!.Slice(0, count - remain, transferOwner)
        };
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
}

/// <summary>字节数组包</summary>
public record struct ArrayPacket : IPacket
{
    private readonly Byte[] _buffer;
    private readonly Int32 _offset;
    private readonly Int32 _length;

    /// <summary>缓冲区</summary>
    public readonly Byte[] Buffer => _buffer;

    /// <summary>偏移</summary>
    public readonly Int32 Offset => _offset;

    /// <summary>长度</summary>
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
    }

    /// <summary>从数组段实例化数据包</summary>
    public ArrayPacket(ArraySegment<Byte> segment) : this(segment.Array!, segment.Offset, segment.Count) { }

    /// <summary>获取 Span</summary>
    public readonly Span<Byte> GetSpan() => new(_buffer, _offset, _length);

    /// <summary>获取 Memory</summary>
    public readonly Memory<Byte> GetMemory() => new(_buffer, _offset, _length);

    /// <summary>切片得到新数据包</summary>
    public readonly IPacket Slice(Int32 offset, Int32 count = -1) => Slice(offset, count, false);

    /// <summary>切片得到新数据包</summary>
    public readonly IPacket Slice(Int32 offset, Int32 count, Boolean transferOwner)
    {
        if (offset < 0 || offset > Total) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count > Total - offset) throw new ArgumentOutOfRangeException(nameof(count));

        var start = _offset + offset;
        var remain = _length - offset;
        if (Next == null)
        {
            if (count < 0 || count > remain) count = remain;
            return new ArrayPacket(_buffer, start, count);
        }

        if (remain <= 0) return Next!.Slice(offset - _length, count, transferOwner);
        if (count < 0) return new ArrayPacket(_buffer, start, remain) { Next = Next };
        if (count <= remain) return new ArrayPacket(_buffer, start, count);

        return new ArrayPacket(_buffer, start, remain) { Next = Next!.Slice(0, count - remain, transferOwner) };
    }

    /// <summary>尝试获取数组段</summary>
    public readonly Boolean TryGetArray(out ArraySegment<Byte> segment)
    {
        segment = new ArraySegment<Byte>(_buffer, _offset, _length);
        return true;
    }
}