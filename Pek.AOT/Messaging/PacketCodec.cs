using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using Pek.Buffers;
using Pek.Data;
using Pek.IO;
using Pek.Log;

namespace Pek.Messaging;

/// <summary>获取长度的委托</summary>
/// <param name="span">数据片段</param>
/// <returns>完整消息长度，返回0或负数表示数据不足</returns>
public delegate Int32 GetLengthDelegate(ReadOnlySpan<Byte> span);

/// <summary>数据包编码器。用于网络粘包处理</summary>
public class PacketCodec : IDisposable
{
    /// <summary>缓存流</summary>
    public MemoryStream? Stream { get; set; }

    /// <summary>获取长度的委托</summary>
    [Obsolete("请使用 GetLength2")]
    public Func<IPacket, Int32>? GetLength { get; set; }

    /// <summary>获取长度的委托</summary>
    public GetLengthDelegate? GetLength2 { get; set; }

    /// <summary>最后一次解包成功时间</summary>
    public DateTime Last { get; set; } = DateTime.Now;

    /// <summary>缓存有效期，默认5000毫秒</summary>
    public Int32 Expire { get; set; } = 5_000;

    /// <summary>最大缓存待处理数据，默认1M</summary>
    public Int32 MaxCache { get; set; } = 1024 * 1024;

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(Boolean disposing)
    {
        if (!disposing) return;

        Stream?.Dispose();
        Stream = null;
    }

    /// <summary>分析数据流并得到一帧或多帧数据</summary>
    /// <param name="packet">待分析数据包</param>
    /// <returns>解析出的完整数据包列表</returns>
    public virtual IList<IPacket> Parse(IPacket packet)
    {
        var stream = Stream;
        var noData = stream == null || stream.Position < 0 || stream.Position >= stream.Length;

#pragma warning disable CS0618
        var func = GetLength;
#pragma warning restore CS0618
        var func2 = GetLength2;
        if (func == null && func2 == null) throw new ArgumentNullException(nameof(GetLength2));

        var list = new List<IPacket>();
        if (noData)
        {
            if (packet == null || packet.Total == 0) return list;

            var index = 0;
            while (index < packet.Total)
            {
                var length = 0;
                if (func2 != null && packet.Next == null)
                {
                    var span = packet.GetSpan().Slice(index);
                    length = func2(span);
                    if (length <= 0 || length > span.Length) break;
                }
                else
                {
                    var slice = packet.Slice(index, -1, false);
                    length = func!(slice);
                    if (length <= 0 || length > slice.Total) break;
                }

                list.Add(packet.Slice(index, length, false));
                index += length;
            }

            if (index == packet.Total) return list;
            packet = packet.Slice(index, -1, false);
        }

        lock (this)
        {
            CheckCache();
            stream = Stream;

            if (packet != null && packet.Total > 0)
            {
                var position = stream.Position;
                stream.Position = stream.Length;
                packet.CopyTo(stream);
                stream.Position = position;
            }

            while (stream.Position < stream.Length)
            {
                var current = new ArrayPacket(stream);
                var length = func2 != null ? func2(current.GetSpan()) : func!(current);
                if (length <= 0 || length > current.Total) break;

                list.Add(current.Slice(0, length));
                stream.Seek(length, SeekOrigin.Current);
            }

            if (stream.Position >= stream.Length)
            {
                stream.SetLength(0);
                stream.Position = 0;
            }

            return list;
        }
    }

    /// <summary>检查缓存，超时或超大时清空</summary>
    [MemberNotNull(nameof(Stream))]
    protected virtual void CheckCache()
    {
        var stream = Stream ??= new MemoryStream();

        var now = DateTime.Now;
        var retain = stream.Length - stream.Position;
        if (retain > 0 && (Last.AddMilliseconds(Expire) < now || MaxCache > 0 && MaxCache <= retain))
        {
            var length = (Int32)(retain > 64 ? 64 : retain);
            var buffer = ArrayPool<Byte>.Shared.Rent(length);
            try
            {
                var count = stream.Read(buffer, 0, length);
                stream.Seek(-count, SeekOrigin.Current);
                var hex = buffer.ToHex(0, count);

                if (XXTrace.Debug)
                    XXTrace.WriteLine("数据包编码器放弃数据 {0:n0}，Last={1}，MaxCache={2:n0}，Preview={3}", retain, Last, MaxCache, hex);
            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(buffer);
            }

            if (stream.Capacity > 1024)
                stream = Stream = new MemoryStream();
            else
            {
                stream.SetLength(0);
                stream.Position = 0;
            }
        }

        Last = now;
    }

    /// <summary>清空缓存</summary>
    public virtual void Clear()
    {
        var stream = Stream;
        if (stream == null) return;

        stream.SetLength(0);
        stream.Position = 0;
    }
}