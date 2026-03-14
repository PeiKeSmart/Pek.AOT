using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

using Pek.Collections;
using Pek.Extension;
using Pek.IO;

namespace Pek.Data;

/// <summary>数据包。表示数据区Data的指定范围（Offset, Count）。</summary>
[Obsolete("请使用 ArrayPacket 或 OwnerPacket 替代")]
public class Packet : IPacket
{
    /// <summary>数据</summary>
    public Byte[] Data { get; private set; } = [];

    /// <summary>偏移</summary>
    public Int32 Offset { get; private set; }

    /// <summary>长度</summary>
    public Int32 Count { get; private set; }

    Int32 IPacket.Length => Count;

    /// <summary>下一个链式包</summary>
    public Packet? Next { get; set; }

    /// <summary>总长度</summary>
    public Int32 Total => Count + (Next != null ? Next.Total : 0);

    [EditorBrowsable(EditorBrowsableState.Never)]
    IPacket? IPacket.Next { get => Next; set => Next = (value as Packet) ?? throw new InvalidDataException(); }

    /// <summary>根据数据区实例化</summary>
    /// <param name="data">数据区</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">长度</param>
    public Packet(Byte[] data, Int32 offset = 0, Int32 count = -1) => Set(data, offset, count);

    /// <summary>根据数组段实例化</summary>
    /// <param name="segment">数组段</param>
    public Packet(ArraySegment<Byte> segment)
    {
        if (segment.Array == null) throw new ArgumentNullException(nameof(segment));

        Set(segment.Array, segment.Offset, segment.Count);
    }

    /// <summary>从可扩展内存流实例化</summary>
    /// <param name="stream">数据流</param>
    public Packet(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        if (stream is MemoryStream memoryStream)
        {
#if !NET45
            if (memoryStream.TryGetBuffer(out var segment))
            {
                if (segment.Array == null) throw new ArgumentNullException(nameof(segment));

                Set(segment.Array, segment.Offset + (Int32)memoryStream.Position, segment.Count - (Int32)memoryStream.Position);
                return;
            }
#endif
        }

        var buffer = new Byte[stream.Length - stream.Position];
        var count = stream.Read(buffer, 0, buffer.Length);
        Set(buffer, 0, count);
        if (count > 0) stream.Seek(-count, SeekOrigin.Current);
    }

    /// <summary>从Span实例化</summary>
    /// <param name="span">数据片段</param>
    public Packet(Span<Byte> span) => Set(span.ToArray());

    /// <summary>从Memory实例化</summary>
    /// <param name="memory">数据片段</param>
    public Packet(Memory<Byte> memory)
    {
        if (MemoryMarshal.TryGetArray((ReadOnlyMemory<Byte>)memory, out var segment))
            Set(segment.Array!, segment.Offset, segment.Count);
        else
            Set(memory.ToArray());
    }

    /// <summary>获取或设置指定位置的字节</summary>
    public Byte this[Int32 index]
    {
        get
        {
            var position = index - Count;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                return Next[position];
            }

            return Data[Offset + index];
        }
        set
        {
            var position = index - Count;
            if (position >= 0)
            {
                if (Next == null) throw new IndexOutOfRangeException(nameof(index));
                Next[position] = value;
            }
            else
            {
                Data[Offset + index] = value;
            }
        }
    }

    /// <summary>设置新的数据区</summary>
    /// <param name="data">数据区</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">字节个数</param>
    [MemberNotNull(nameof(Data))]
    public virtual void Set(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        Data = data;

        if (data == null)
        {
            Data = [];
            Offset = 0;
            Count = 0;
        }
        else
        {
            Offset = offset;
            if (count < 0) count = data.Length - offset;
            Count = count;
        }
    }

    /// <summary>截取子数据区</summary>
    /// <param name="offset">相对偏移</param>
    /// <param name="count">字节个数</param>
    /// <returns>子数据包</returns>
    public Packet Slice(Int32 offset, Int32 count = -1)
    {
        var start = Offset + offset;
        var remain = Count - offset;

        if (Next == null)
        {
            if (count < 0 || count > remain) count = remain;
            if (count < 0) count = 0;

            return new Packet(Data, start, count);
        }

        if (remain <= 0) return Next.Slice(offset - Count, count);
        if (count < 0) return new Packet(Data, start, remain) { Next = Next };
        if (count <= remain) return new Packet(Data, start, count);

        return new Packet(Data, start, remain) { Next = Next.Slice(0, count - remain) };
    }

    IPacket IPacket.Slice(Int32 offset, Int32 count) => Slice(offset, count);
    IPacket IPacket.Slice(Int32 offset, Int32 count, Boolean transferOwner) => Slice(offset, count);

    /// <summary>查找目标数组</summary>
    /// <param name="data">目标数组</param>
    /// <param name="offset">本数组起始偏移</param>
    /// <param name="count">本数组搜索个数</param>
    /// <returns>匹配位置</returns>
    public Int32 IndexOf(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        var start = offset;
        var length = data.Length;

        if (count < 0 || count > Total - offset) count = Total - offset;

        if (Next == null)
        {
            if (start >= Count) return -1;

            var position = Data.IndexOf(data, Offset + start, count);
            return position >= 0 ? position - Offset : -1;
        }

        var window = 0;
        for (var i = 0; i + length - window <= count; i++)
        {
            if (this[start + i] == data[window])
            {
                window++;
                if (window >= length) return (start + i) - length + 1;
            }
            else
            {
                i -= window;
                window = 0;

                if (start + i == Count && Next != null)
                {
                    var position = Next.IndexOf(data, 0, count - i);
                    if (position >= 0) return (start + i) + position;
                    break;
                }
            }
        }

        return -1;
    }

    /// <summary>附加一个包到当前包链的末尾</summary>
    /// <param name="packet">待附加包</param>
    /// <returns>当前包</returns>
    public Packet Append(Packet packet)
    {
        if (packet == null) return this;

        var current = this;
        while (current.Next != null) current = current.Next;
        current.Next = packet;

        return this;
    }

    /// <summary>返回字节数组。无差别复制，一定返回新数组</summary>
    /// <returns>字节数组副本</returns>
    public virtual Byte[] ToArray()
    {
        if (Next == null) return Data.ReadBytes(Offset, Count);

        using var memoryStream = Pool.MemoryStream.Get();
        CopyTo(memoryStream);
        return memoryStream.Return(true);
    }

    /// <summary>从封包中读取指定数据区</summary>
    /// <param name="offset">相对于数据包的起始位置</param>
    /// <param name="count">字节个数</param>
    /// <returns>字节数组</returns>
    public Byte[] ReadBytes(Int32 offset = 0, Int32 count = -1)
    {
        if (offset == 0 && count < 0)
        {
            if (Offset == 0 && Offset + Count == Data.Length && Next == null) return Data;
            return ToArray();
        }

        if (Next == null)
        {
            return Data.ReadBytes(Offset + offset, count < 0 || count > Count ? Count : count);
        }

        if (count >= 0 && offset + count <= Count)
            return Data.ReadBytes(Offset + offset, count);

        if (count < 0) count = Total - offset;
        using var memoryStream = Pool.MemoryStream.Get();

        var current = this;
        while (current != null && count > 0)
        {
            var length = current.Count;
            if (length < offset)
            {
                offset -= length;
            }
            else if (current.Data != null)
            {
                length -= offset;
                if (length > count) length = count;
                memoryStream.Write(current.Data, current.Offset + offset, length);

                offset = 0;
                count -= length;
            }

            current = current.Next;
        }

        return memoryStream.Return(true);
    }

    /// <summary>返回数据段</summary>
    /// <returns>数组段</returns>
    public ArraySegment<Byte> ToSegment()
    {
        if (Next == null) return new ArraySegment<Byte>(Data, Offset, Count);
        return new ArraySegment<Byte>(ToArray());
    }

    /// <summary>返回数据段集合</summary>
    /// <returns>数组段集合</returns>
    public IList<ArraySegment<Byte>> ToSegments()
    {
        var list = new List<ArraySegment<Byte>>(4);
        for (var packet = this; packet != null; packet = packet.Next)
        {
            list.Add(new ArraySegment<Byte>(packet.Data, packet.Offset, packet.Count));
        }

        return list;
    }

    /// <summary>转为Span</summary>
    /// <returns>字节Span</returns>
    public Span<Byte> AsSpan()
    {
        if (Next == null) return new Span<Byte>(Data, Offset, Count);
        return new Span<Byte>(ToArray());
    }

    /// <summary>转为Memory</summary>
    /// <returns>字节Memory</returns>
    public Memory<Byte> AsMemory()
    {
        if (Next == null) return new Memory<Byte>(Data, Offset, Count);
        return new Memory<Byte>(ToArray());
    }

    Span<Byte> IPacket.GetSpan() => AsSpan();
    Memory<Byte> IPacket.GetMemory() => AsMemory();

    /// <summary>获取封包的数据流形式</summary>
    /// <returns>内存流</returns>
    public virtual MemoryStream GetStream()
    {
        if (Next == null) return new MemoryStream(Data, Offset, Count, false, true);

        var memoryStream = new MemoryStream();
        CopyTo(memoryStream);
        memoryStream.Position = 0;
        return memoryStream;
    }

    /// <summary>把封包写入到数据流</summary>
    /// <param name="stream">目标流</param>
    public void CopyTo(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        stream.Write(Data, Offset, Count);
        Next?.CopyTo(stream);
    }

    /// <summary>把封包写入到目标数组</summary>
    /// <param name="buffer">目标数组</param>
    /// <param name="offset">目标数组偏移量</param>
    /// <param name="count">目标数组字节数</param>
    public void WriteTo(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));

        if (count < 0) count = Total;
        var length = count;
        if (length > Count) length = Count;
        Buffer.BlockCopy(Data, Offset, buffer, offset, length);

        offset += length;
        count -= length;
        if (count > 0) Next?.WriteTo(buffer, offset, count);
    }

    /// <summary>异步复制到目标数据流</summary>
    /// <param name="stream">目标流</param>
    /// <returns>任务</returns>
    public async Task CopyToAsync(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        await stream.WriteAsync(Data, Offset, Count).ConfigureAwait(false);
        if (Next != null) await Next.CopyToAsync(stream).ConfigureAwait(false);
    }

    /// <summary>异步复制到目标数据流</summary>
    /// <param name="stream">目标流</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>任务</returns>
    public async Task CopyToAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        await stream.WriteAsync(Data, Offset, Count, cancellationToken).ConfigureAwait(false);
        if (Next != null) await Next.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>深度克隆一份数据包</summary>
    /// <returns>新的数据包</returns>
    public Packet Clone()
    {
        if (Next == null) return new Packet(ReadBytes(0, Count));

        using var memoryStream = Pool.MemoryStream.Get();
        CopyTo(memoryStream);
        return new Packet(memoryStream.Return(true));
    }

    /// <summary>尝试获取缓冲区</summary>
    /// <param name="segment">数组段</param>
    /// <returns>是否成功</returns>
    public Boolean TryGetArray(out ArraySegment<Byte> segment)
    {
        if (Next == null)
        {
            segment = new ArraySegment<Byte>(Data, Offset, Count);
            return true;
        }

        segment = default;
        return false;
    }

    /// <summary>以字符串表示</summary>
    /// <param name="encoding">字符串编码，默认UTF-8</param>
    /// <param name="offset">起始偏移</param>
    /// <param name="count">字节个数</param>
    /// <returns>字符串表示</returns>
    public String ToStr(Encoding? encoding = null, Int32 offset = 0, Int32 count = -1)
    {
        if (Data == null) return String.Empty;

        encoding ??= Encoding.UTF8;
        if (count < 0) count = Total - offset;

        if (Next == null) return Data.ToStr(encoding, Offset + offset, count);
        return ReadBytes(offset, count).ToStr(encoding);
    }

    /// <summary>以十六进制编码表示</summary>
    /// <param name="maxLength">最大显示多少个字节。默认32</param>
    /// <param name="separate">分隔符</param>
    /// <param name="groupSize">分组大小</param>
    /// <returns>十六进制字符串</returns>
    public String ToHex(Int32 maxLength = 32, String? separate = null, Int32 groupSize = 0)
    {
        if (Data == null) return String.Empty;

        var hex = ReadBytes(0, maxLength).ToHex(separate, groupSize);
        return (maxLength == -1 || Count <= maxLength) ? hex : String.Concat(hex, "...");
    }

    /// <summary>转为Base64编码</summary>
    /// <returns>Base64字符串</returns>
    public String ToBase64()
    {
        if (Data == null) return String.Empty;

        if (Next == null) return Data.ToBase64(Offset, Count);

        return ToArray().ToBase64();
    }

    /// <summary>读取无符号短整数</summary>
    /// <param name="isLittleEndian">是否小端</param>
    /// <returns>无符号短整数</returns>
    public UInt16 ReadUInt16(Boolean isLittleEndian = true)
        => Data.ToUInt16(Offset, isLittleEndian);

    /// <summary>读取无符号整数</summary>
    /// <param name="isLittleEndian">是否小端</param>
    /// <returns>无符号整数</returns>
    public UInt32 ReadUInt32(Boolean isLittleEndian = true)
        => Data.ToUInt32(Offset, isLittleEndian);

    /// <summary>字节数组隐式转换为数据包</summary>
    public static implicit operator Packet(Byte[] value) => value == null ? null! : new(value);

    /// <summary>数组段隐式转换为数据包</summary>
    public static implicit operator Packet(ArraySegment<Byte> value) => new(value);

    /// <summary>字符串隐式转换为数据包</summary>
    public static implicit operator Packet(String value) => new(value.GetBytes());

    /// <summary>返回文本表示</summary>
    public override String ToString() => $"[{Data.Length}]({Offset}, {Count})" + (Next == null ? String.Empty : $"<{Total}>");
}