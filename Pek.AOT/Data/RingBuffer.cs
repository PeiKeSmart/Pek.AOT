namespace Pek.Data;

/// <summary>环形缓冲区。用于协议组包设计</summary>
/// <remarks>
/// 环形缓冲区是一种固定大小的缓冲区，当缓冲区满时新数据会覆盖旧数据。
/// 本实现支持动态扩容，确保数据不丢失。
/// 使用 Head 指针标记写入位置，Tail 指针标记读取位置。
/// </remarks>
public class RingBuffer
{
    private Byte[] _data;

    /// <summary>容量</summary>
    public Int32 Capacity => _data.Length;

    /// <summary>头指针。写入位置</summary>
    public Int32 Head { get; set; }

    /// <summary>尾指针。读取位置</summary>
    public Int32 Tail { get; set; }

    /// <summary>数据长度</summary>
    /// <remarks>环形缓冲区中实际存储的数据长度</remarks>
    public Int32 Length { get; private set; }

    /// <summary>使用默认容量 1024 初始化</summary>
    public RingBuffer() : this(1024) { }

    /// <summary>实例化环形缓冲区</summary>
    /// <param name="capacity">容量</param>
    public RingBuffer(Int32 capacity) => _data = new Byte[capacity];

    /// <summary>扩容，确保容量</summary>
    /// <param name="capacity">目标容量</param>
    /// <remarks>
    /// 当需要更大容量时，会分配新的缓冲区并正确复制环形数据。
    /// 复制后会重置 Head 和 Tail 指针为线性布局。
    /// </remarks>
    public void EnsureCapacity(Int32 capacity)
    {
        if (capacity <= Capacity) return;

        var newData = new Byte[capacity];
        var length = Length;
        if (length > 0)
        {
            if (Head > Tail)
            {
                Buffer.BlockCopy(_data, Tail, newData, 0, length);
            }
            else
            {
                var tailToEnd = _data.Length - Tail;
                Buffer.BlockCopy(_data, Tail, newData, 0, tailToEnd);
                Buffer.BlockCopy(_data, 0, newData, tailToEnd, Head);
            }

            Tail = 0;
            Head = length;
        }

        _data = newData;
    }

    /// <summary>写入数据</summary>
    /// <param name="data">源数据</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">个数</param>
    /// <remarks>
    /// 当缓冲区空间不足时会自动扩容。
    /// 写入时会正确处理环形边界情况。
    /// </remarks>
    public void Write(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) return;
        if (offset < 0 || offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

        if (count < 0) count = data.Length - offset;
        if (count == 0) return;
        if (offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

        CheckCapacity(Length + count);

        var remaining = count;
        var srcOffset = offset;

        if (Head >= Tail)
        {
            var firstChunkSize = Math.Min(remaining, _data.Length - Head);
            if (firstChunkSize > 0)
            {
                Buffer.BlockCopy(data, srcOffset, _data, Head, firstChunkSize);
                Head += firstChunkSize;
                srcOffset += firstChunkSize;
                remaining -= firstChunkSize;
                if (Head == _data.Length) Head = 0;
            }

            if (remaining > 0)
            {
                Buffer.BlockCopy(data, srcOffset, _data, Head, remaining);
                Head += remaining;
            }
        }
        else
        {
            var availableSpace = Tail - Head;
            if (count > availableSpace) throw new InvalidOperationException("缓冲区空间不足，容量检查失败");

            Buffer.BlockCopy(data, offset, _data, Head, count);
            Head += count;
        }

        Length += count;
    }

    /// <summary>读取数据</summary>
    /// <param name="data">目标缓冲区</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">期望读取字节数</param>
    /// <returns>实际读取字节数</returns>
    /// <remarks>
    /// 读取时会正确处理环形边界情况。
    /// 实际读取的字节数可能小于期望值，取决于缓冲区中的可用数据量。
    /// </remarks>
    public Int32 Read(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

        if (count < 0) count = data.Length - offset;
        if (count == 0) return 0;
        if (offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));

        var availableLength = Length;
        if (availableLength == 0) return 0;

        var toRead = Math.Min(count, availableLength);
        var totalRead = 0;

        if (Head > Tail)
        {
            var readSize = Math.Min(toRead, Head - Tail);
            Buffer.BlockCopy(_data, Tail, data, offset, readSize);
            Tail += readSize;
            totalRead = readSize;
        }
        else
        {
            var firstChunkSize = Math.Min(toRead, _data.Length - Tail);
            Buffer.BlockCopy(_data, Tail, data, offset, firstChunkSize);
            Tail = (Tail + firstChunkSize) % _data.Length;
            totalRead += firstChunkSize;
            toRead -= firstChunkSize;

            if (toRead > 0)
            {
                var secondChunkSize = Math.Min(toRead, Head);
                Buffer.BlockCopy(_data, 0, data, offset + totalRead, secondChunkSize);
                Tail = secondChunkSize;
                totalRead += secondChunkSize;
            }
        }

        Length -= totalRead;
        return totalRead;
    }

    private void CheckCapacity(Int32 capacity)
    {
        var length = _data.Length;
        while (length < capacity)
        {
            length *= 2;
        }

        EnsureCapacity(length);
    }
}