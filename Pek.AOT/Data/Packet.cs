using System.ComponentModel;
using System.Text;

using Pek.Collections;

namespace Pek.Data;

/// <summary>数据包。表示数据区 Data 的指定范围（Offset, Count）。</summary>
public class Packet : IPacket
{
    /// <summary>数据</summary>
    public Byte[] Data { get; private set; } = [];

    /// <summary>偏移</summary>
    public Int32 Offset { get; private set; }

    /// <summary>长度</summary>
    public Int32 Count { get; private set; }

    /// <summary>下一个链式包</summary>
    public Packet? Next { get; set; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    IPacket? IPacket.Next
    {
        get => Next;
        set => Next = value as Packet ?? (value != null ? new Packet(value.ToArray()) : null);
    }

    /// <summary>总长度</summary>
    public Int32 Total => Count + (Next != null ? Next.Total : 0);

    Int32 IPacket.Length => Count;

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

    /// <summary>设置新的数据区</summary>
    /// <param name="data">数据区</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">长度</param>
    public void Set(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || offset > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

        if (count < 0) count = data.Length - offset;
        if (count < 0 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

        Data = data;
        Offset = offset;
        Count = count;
    }

    /// <summary>截取子数据区</summary>
    /// <param name="offset">相对偏移</param>
    /// <param name="count">字节个数</param>
    /// <returns>子数据包</returns>
    public Packet Slice(Int32 offset, Int32 count = -1)
    {
        if (offset < 0 || offset > Count) throw new ArgumentOutOfRangeException(nameof(offset));

        var available = Count - offset;
        if (count < 0 || count > available) count = available;

        return new Packet(Data, Offset + offset, count);
    }

    IPacket IPacket.Slice(Int32 offset, Int32 count) => Slice(offset, count);

    IPacket IPacket.Slice(Int32 offset, Int32 count, Boolean transferOwner) => Slice(offset, count);

    /// <summary>获取数组段</summary>
    public ArraySegment<Byte> ToSegment() => Next == null ? new ArraySegment<Byte>(Data, Offset, Count) : new ArraySegment<Byte>(ToArray());

    /// <summary>获取数组段集合</summary>
    public IList<ArraySegment<Byte>> ToSegments()
    {
        var list = new List<ArraySegment<Byte>>(4);
        for (var current = this; current != null; current = current.Next)
        {
            list.Add(new ArraySegment<Byte>(current.Data, current.Offset, current.Count));
        }

        return list;
    }

    /// <summary>转为 Span</summary>
    public Span<Byte> AsSpan() => Next == null ? new Span<Byte>(Data, Offset, Count) : new Span<Byte>(ToArray());

    /// <summary>转为 Memory</summary>
    public Memory<Byte> AsMemory() => Next == null ? new Memory<Byte>(Data, Offset, Count) : new Memory<Byte>(ToArray());

    Span<Byte> IPacket.GetSpan() => AsSpan();

    Memory<Byte> IPacket.GetMemory() => AsMemory();

    /// <summary>尝试获取数组段</summary>
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

    /// <summary>返回字节数组。总是返回新数组。</summary>
    /// <returns>字节数组副本</returns>
    public virtual Byte[] ToArray()
    {
        if (Next == null)
        {
            var result = new Byte[Count];
            Buffer.BlockCopy(Data, Offset, result, 0, Count);
            return result;
        }

        using var stream = Pool.MemoryStream.Get();
        CopyTo(stream);
        return stream.Return(true);
    }

    /// <summary>复制到目标流</summary>
    /// <param name="stream">目标流</param>
    public void CopyTo(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));

        var current = this;
        while (current != null)
        {
            stream.Write(current.Data, current.Offset, current.Count);
            current = current.Next;
        }
    }

    /// <summary>转十六进制字符串</summary>
    /// <param name="maxBytes">最大输出字节数，小于等于0表示全部</param>
    /// <returns>十六进制文本</returns>
    public String ToHex(Int32 maxBytes = 0)
    {
        var bytes = ToArray();
        var length = maxBytes > 0 && maxBytes < bytes.Length ? maxBytes : bytes.Length;
        var builder = Pool.StringBuilder.Get();
        try
        {
            for (var i = 0; i < length; i++)
            {
                builder.Append(bytes[i].ToString("X2"));
            }

            if (length < bytes.Length) builder.Append("...");
            return builder.ToString();
        }
        finally
        {
            Pool.StringBuilder.Return(builder);
        }
    }

    /// <summary>返回文本表示</summary>
    /// <returns>十六进制文本</returns>
    public override String ToString() => $"[{Data.Length}]({Offset}, {Count})" + (Next == null ? String.Empty : $"<{Total}>");
}