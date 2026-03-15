using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text;

using Pek.Collections;

namespace Pek.IO;

/// <summary>IO 辅助扩展</summary>
public static class IOHelper
{
    /// <summary>最大安全数组大小。超过该大小时，读取数据操作将强制失败，默认 1024*1024</summary>
    /// <remarks>
    /// 这是一个保护性设置，避免解码错误数据时读取了超大数组导致应用崩溃。
    /// 需要解码较大二进制数据时，可以适当放宽该阈值。
    /// </remarks>
    public static Int32 MaxSafeArraySize { get; set; } = 1024 * 1024;

    /// <summary>复制数组</summary>
    public static Byte[] ReadBytes(this Byte[] src, Int32 offset, Int32 count)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (count == 0) return [];
        if (count < 0) count = src.Length - offset;

        var data = new Byte[count];
        Buffer.BlockCopy(src, offset, data, 0, data.Length);
        return data;
    }

    /// <summary>压缩数据流</summary>
    /// <param name="inStream">输入流</param>
    /// <param name="outStream">输出流。如果不指定，则内部实例化一个内存流</param>
    /// <returns>输出流</returns>
    public static Stream Compress(this Stream inStream, Stream? outStream = null)
    {
        if (inStream == null) throw new ArgumentNullException(nameof(inStream));

        var ms = outStream ?? new MemoryStream();
        using (var stream = new DeflateStream(ms, CompressionLevel.Optimal, true))
        {
            inStream.CopyTo(stream);
            stream.Flush();
        }

        if (outStream == null) ms.Position = 0;
        return ms;
    }

    /// <summary>解压缩数据流</summary>
    /// <param name="inStream">输入流</param>
    /// <param name="outStream">输出流。如果不指定，则内部实例化一个内存流</param>
    /// <returns>输出流</returns>
    public static Stream Decompress(this Stream inStream, Stream? outStream = null)
    {
        if (inStream == null) throw new ArgumentNullException(nameof(inStream));

        var ms = outStream ?? new MemoryStream();
        using (var stream = new DeflateStream(inStream, CompressionMode.Decompress, true))
        {
            stream.CopyTo(ms);
        }

        if (outStream == null) ms.Position = 0;
        return ms;
    }

    /// <summary>压缩字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <returns>压缩后的字节数组</returns>
    public static Byte[] Compress(this Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var ms = new MemoryStream();
        Compress(new MemoryStream(data), ms);
        return ms.ToArray();
    }

    /// <summary>解压缩字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <returns>解压后的字节数组</returns>
    public static Byte[] Decompress(this Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var ms = new MemoryStream();
        Decompress(new MemoryStream(data), ms);
        return ms.ToArray();
    }

    /// <summary>使用 GZip 压缩数据流</summary>
    /// <param name="inStream">输入流</param>
    /// <param name="outStream">输出流。如果不指定，则内部实例化一个内存流</param>
    /// <returns>输出流</returns>
    public static Stream CompressGZip(this Stream inStream, Stream? outStream = null)
    {
        if (inStream == null) throw new ArgumentNullException(nameof(inStream));

        var ms = outStream ?? new MemoryStream();
        using (var stream = new GZipStream(ms, CompressionLevel.Optimal, true))
        {
            inStream.CopyTo(stream);
            stream.Flush();
        }

        if (outStream == null) ms.Position = 0;
        return ms;
    }

    /// <summary>使用 GZip 解压缩数据流</summary>
    /// <param name="inStream">输入流</param>
    /// <param name="outStream">输出流。如果不指定，则内部实例化一个内存流</param>
    /// <returns>输出流</returns>
    public static Stream DecompressGZip(this Stream inStream, Stream? outStream = null)
    {
        if (inStream == null) throw new ArgumentNullException(nameof(inStream));

        var ms = outStream ?? new MemoryStream();
        using (var stream = new GZipStream(inStream, CompressionMode.Decompress, true))
        {
            stream.CopyTo(ms);
        }

        if (outStream == null) ms.Position = 0;
        return ms;
    }

    /// <summary>使用 GZip 压缩字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <returns>压缩后的字节数组</returns>
    public static Byte[] CompressGZip(this Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var ms = new MemoryStream();
        CompressGZip(new MemoryStream(data), ms);
        return ms.ToArray();
    }

    /// <summary>使用 GZip 解压缩字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <returns>解压后的字节数组</returns>
    public static Byte[] DecompressGZip(this Byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var ms = new MemoryStream();
        DecompressGZip(new MemoryStream(data), ms);
        return ms.ToArray();
    }

    /// <summary>从流中至少读取指定字节数</summary>
    public static Int32 ReadAtLeast(this Stream stream, Byte[] buffer, Int32 offset, Int32 count, Int32 minimumBytes, Boolean throwOnEndOfStream = true)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (minimumBytes < 0 || minimumBytes > count) throw new ArgumentOutOfRangeException(nameof(minimumBytes));
        if (minimumBytes == 0 || count == 0) return 0;

        var totalRead = 0;
        while (totalRead < minimumBytes)
        {
            var bytesRead = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (bytesRead == 0)
            {
                if (throwOnEndOfStream)
                    throw new EndOfStreamException($"Unable to read the required minimum number of bytes. Expected {minimumBytes}, actual {totalRead}.");
                break;
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }

    /// <summary>从流中精确读取指定字节数到缓冲区</summary>
    /// <param name="stream">源流</param>
    /// <param name="buffer">目标缓冲区</param>
    /// <param name="offset">目标偏移</param>
    /// <param name="count">期望读取字节数</param>
    /// <returns>实际读取字节数</returns>
    public static Int32 ReadExactly(this Stream stream, Byte[] buffer, Int32 offset, Int32 count) => ReadAtLeast(stream, buffer, offset, count, count, true);

    /// <summary>从流中精确读取指定数量字节</summary>
    /// <param name="stream">源流</param>
    /// <param name="count">期望读取字节数</param>
    /// <returns>字节数组</returns>
    public static Byte[] ReadExactly(this Stream stream, Int64 count)
    {
        var buffer = new Byte[count];
        ReadAtLeast(stream, buffer, 0, buffer.Length, (Int32)count, true);
        return buffer;
    }

    /// <summary>字节数组转换为字符串</summary>
    public static String ToStr(this Byte[] buf, Encoding? encoding = null, Int32 offset = 0, Int32 count = -1)
    {
        if (buf == null || buf.Length <= 0 || offset >= buf.Length) return String.Empty;

        encoding ??= Encoding.UTF8;
        var size = buf.Length - offset;
        if (count < 0 || count > size) count = size;

        var skip = 0;
        var preamble = encoding.GetPreamble();
        if (preamble != null && preamble.Length > 0 && buf.Length >= offset + preamble.Length)
        {
            if (buf.AsSpan(offset, preamble.Length).SequenceEqual(preamble)) skip = preamble.Length;
        }

        return encoding.GetString(buf, offset + skip, count - skip);
    }

    /// <summary>把字节数组编码为十六进制字符串</summary>
    public static String ToHex(this Byte[]? data, Int32 offset = 0, Int32 count = -1)
    {
        if (data == null || data.Length <= 0) return String.Empty;

        if (count < 0)
            count = data.Length - offset;
        else if (offset + count > data.Length)
            count = data.Length - offset;
        if (count == 0) return String.Empty;

        var chars = new Char[count * 2];
        for (Int32 i = 0, j = 0; i < count; i++, j += 2)
        {
            var value = data[offset + i];
            chars[j] = GetHexValue(value >> 4);
            chars[j + 1] = GetHexValue(value & 0x0F);
        }

        return new String(chars);
    }

    /// <summary>把字节数组编码为十六进制字符串，带有分隔符和分组功能</summary>
    public static String ToHex(this Byte[]? data, String? separate, Int32 groupSize = 0, Int32 maxLength = -1)
    {
        if (data == null || data.Length <= 0) return String.Empty;
        if (groupSize < 0) groupSize = 0;

        var count = data.Length;
        if (maxLength > 0 && maxLength < count) count = maxLength;

        if (groupSize == 0 && count == data.Length)
        {
            if (String.IsNullOrEmpty(separate)) return data.ToHex();
            if (separate == "-") return BitConverter.ToString(data, 0, count);
        }

        var builder = Pool.StringBuilder.Get();
        for (var i = 0; i < count; i++)
        {
            if (builder.Length > 0)
            {
                if (groupSize <= 0 || i % groupSize == 0) builder.Append(separate);
            }

            var value = data[i];
            builder.Append(GetHexValue(value >> 4));
            builder.Append(GetHexValue(value & 0x0F));
        }

        return builder.Return(true);
    }

    /// <summary>字节数组转为 Base64 编码</summary>
    public static String ToBase64(this Byte[] data, Int32 offset = 0, Int32 count = -1, Boolean lineBreak = false)
    {
        if (data == null || data.Length <= 0) return String.Empty;

        if (count <= 0)
            count = data.Length - offset;
        else if (offset + count > data.Length)
            count = data.Length - offset;

        return Convert.ToBase64String(data, offset, count, lineBreak ? Base64FormattingOptions.InsertLineBreaks : Base64FormattingOptions.None);
    }

    /// <summary>搜索字节模式</summary>
    public static Int32 IndexOf(this Byte[] source, Byte[] pattern, Int32 offset = 0, Int32 count = -1)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (pattern == null) throw new ArgumentNullException(nameof(pattern));

        var total = source.Length;
        var length = pattern.Length;
        if (count > 0 && total > offset + count) total = offset + count;
        if (total == 0 || length == 0 || length > total) return -1;

        var bads = new Int32[256];
        for (var i = 0; i < 256; i++) bads[i] = length;

        var last = length - 1;
        for (var i = 0; i < last; i++) bads[pattern[i]] = last - i;

        var index = offset;
        while (index <= total - length)
        {
            for (var i = last; source[index + i] == pattern[i]; i--)
            {
                if (i == 0) return index;
            }

            index += bads[source[index + last]];
        }

        return -1;
    }

    /// <summary>从字节数据指定位置读取一个无符号16位整数</summary>
    public static UInt16 ToUInt16(this Byte[] data, Int32 offset = 0, Boolean isLittleEndian = true)
    {
        if (isLittleEndian)
            return (UInt16)((data[offset + 1] << 8) | data[offset]);
        else
            return (UInt16)((data[offset] << 8) | data[offset + 1]);
    }

    /// <summary>从字节数据指定位置读取一个无符号32位整数</summary>
    public static UInt32 ToUInt32(this Byte[] data, Int32 offset = 0, Boolean isLittleEndian = true)
    {
        if (isLittleEndian) return BitConverter.ToUInt32(data, offset);

        if (offset > 0) data = data.ReadBytes(offset, 4);
        return (UInt32)(data[0] << 0x18 | data[1] << 0x10 | data[2] << 8 | data[3]);
    }

    private static Char GetHexValue(Int32 value) => value < 10 ? (Char)(value + '0') : (Char)(value - 10 + 'A');
}