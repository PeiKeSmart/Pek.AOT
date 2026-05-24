using System.Buffers;
using System.Runtime.CompilerServices;

namespace Pek.Buffers;

/// <summary>池化缓冲区写入器</summary>
/// <remarks>
/// 面向需要动态扩容、避免频繁分配的大块连续写入场景。
/// 基于 ArrayPool 进行数组租借与归还；使用完毕后必须调用 Dispose 或 ClearAndReturnBuffers。
/// 非线程安全：单实例请勿跨线程并发写入。
/// </remarks>
public sealed class PooledByteBufferWriter : IBufferWriter<Byte>, IDisposable
{
    private const Int32 MaxArrayLength = 0x7FFFFFC7;
    private const Int32 HalfOfMaxArrayLength = MaxArrayLength / 2;

    private Byte[] _rentedBuffer;
    private Int32 _index;

    /// <summary>已写入内存</summary>
    public ReadOnlyMemory<Byte> WrittenMemory => _rentedBuffer.AsMemory(0, _index);

    /// <summary>已写入数据段</summary>
    public ReadOnlySpan<Byte> WrittenSpan => _rentedBuffer.AsSpan(0, _index);

    /// <summary>已写入字节数</summary>
    public Int32 WrittenCount => _index;

    /// <summary>当前缓冲区总容量</summary>
    public Int32 Capacity => _rentedBuffer.Length;

    /// <summary>剩余可写容量</summary>
    public Int32 FreeCapacity => _rentedBuffer.Length - _index;

    /// <summary>指定初始容量并初始化写入器</summary>
    /// <param name="initialCapacity">初始容量</param>
    public PooledByteBufferWriter(Int32 initialCapacity)
    {
        if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        _rentedBuffer = ArrayPool<Byte>.Shared.Rent(initialCapacity);
    }

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        if (_rentedBuffer != null) ClearAndReturnBuffers();
    }

    /// <summary>重新初始化为空实例</summary>
    /// <param name="initialCapacity">初始容量</param>
    public void InitializeEmptyInstance(Int32 initialCapacity)
    {
        _rentedBuffer = ArrayPool<Byte>.Shared.Rent(initialCapacity);
        _index = 0;
    }

    /// <summary>清空写入器</summary>
    public void Clear()
    {
        _rentedBuffer.AsSpan(0, _index).Clear();
        _index = 0;
    }

    /// <summary>清空并归还缓冲区</summary>
    public void ClearAndReturnBuffers()
    {
        Clear();

        var rentedBuffer = _rentedBuffer;
        _rentedBuffer = null!;
        ArrayPool<Byte>.Shared.Return(rentedBuffer);
    }

    /// <summary>推进写入位置</summary>
    /// <param name="count">新增写入的字节数</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(Int32 count)
    {
        var newIndex = _index + count;
        if (newIndex < 0 || (UInt32)newIndex > (UInt32)_rentedBuffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        _index = newIndex;
    }

    /// <summary>返回可写入的内存</summary>
    /// <param name="sizeHint">最小可用空间</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<Byte> GetMemory(Int32 sizeHint = 256)
    {
        EnsureCapacity(sizeHint);
        return _rentedBuffer.AsMemory(_index);
    }

    /// <summary>返回可写入的Span</summary>
    /// <param name="sizeHint">最小可用空间</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Byte> GetSpan(Int32 sizeHint = 256)
    {
        EnsureCapacity(sizeHint);
        return _rentedBuffer.AsSpan(_index);
    }

    /// <summary>同步写入到目标流</summary>
    /// <param name="destination">目标流</param>
    public void WriteToStream(Stream destination) => destination.Write(_rentedBuffer, 0, _index);

    /// <summary>异步写入到目标流</summary>
    /// <param name="destination">目标流</param>
    /// <param name="cancellationToken">取消令牌</param>
    public ValueTask WriteToStreamAsync(Stream destination, CancellationToken cancellationToken) =>
        destination.WriteAsync(WrittenMemory, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(Int32 sizeHint)
    {
        if (sizeHint < 0) sizeHint = 0;

        var remaining = _rentedBuffer.Length - _index;
        if (sizeHint <= remaining) return;

        CheckAndResizeBuffer(sizeHint);
    }

    private void CheckAndResizeBuffer(Int32 sizeHint)
    {
        var currentLength = _rentedBuffer.Length;
        var remaining = currentLength - _index;

        if (_index >= HalfOfMaxArrayLength)
            sizeHint = Math.Max(sizeHint, MaxArrayLength - currentLength);

        if (sizeHint <= remaining) return;

        var grow = Math.Max(sizeHint, currentLength);
        var newSize = currentLength + grow;
        if ((UInt32)newSize > MaxArrayLength)
        {
            newSize = currentLength + sizeHint;
            if ((UInt32)newSize > MaxArrayLength)
                throw new OutOfMemoryException($"BufferMaximumSizeExceeded({newSize})");
        }

        var old = _rentedBuffer;
        _rentedBuffer = ArrayPool<Byte>.Shared.Rent(newSize);

        var written = old.AsSpan(0, _index);
        written.CopyTo(_rentedBuffer);
        written.Clear();
        ArrayPool<Byte>.Shared.Return(old);
    }
}