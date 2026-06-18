#if NET8_0_OR_GREATER
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Pek.Compression.StringZipper;

/// <summary>字符串压缩工具</summary>
public static class DHStringZipper
{
    private static void Register(String identifier, Object component)
    {
        var syncRoot = SyncRoot;
        lock (syncRoot)
        {
            var sortedDictionary = new SortedDictionary<String, Object>(_components, StringComparer.OrdinalIgnoreCase);
            sortedDictionary[identifier] = component;
            _components = sortedDictionary;
        }
    }

    /// <summary>注册一个压缩器</summary>
    /// <param name="compresser">压缩器</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Register(ICompressor compresser)
    {
        if (compresser == null)
            throw new ArgumentNullException(nameof(compresser));

        Register(compresser.Identifier, compresser);
    }

    /// <summary>注册一个编码器</summary>
    /// <param name="encoder">编码器</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Register(IEncoder encoder)
    {
        if (encoder == null)
            throw new ArgumentNullException(nameof(encoder));

        Register(encoder.Identifier, encoder);
    }

    /// <summary>尝试获取已注册的组件</summary>
    /// <typeparam name="T">组件类型</typeparam>
    /// <param name="id">标识符</param>
    /// <param name="component">组件实例</param>
    /// <returns>是否成功获取</returns>
    public static Boolean TryGetComponent<T>(String id, out T component)
    {
        if (_components.TryGetValue(id, out var value) && value is T)
        {
            component = (T)value;
            return true;
        }
        component = default;
        return false;
    }

    /// <summary>压缩字符串</summary>
    /// <param name="str">待压缩字符串</param>
    /// <param name="compressor">压缩器</param>
    /// <param name="encoder">编码器</param>
    /// <returns>压缩后的字符串</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static String Zip(String str, ICompressor compressor, IEncoder encoder)
    {
        if (String.IsNullOrWhiteSpace(str))
            return String.Empty;

        if (compressor == null)
            throw new ArgumentNullException(nameof(compressor));

        if (encoder == null)
            throw new ArgumentNullException(nameof(encoder));

        if (PrefixRegex.IsMatch(str))
            return str;

        var data = compressor.Compress(str);
        var output = encoder.Encode(data);

        var handler = new DefaultInterpolatedStringHandler(14, 3);
        handler.AppendLiteral("data:text/x-");
        handler.AppendFormatted(compressor.Identifier);
        handler.AppendLiteral(";");
        handler.AppendFormatted(encoder.Identifier);
        handler.AppendLiteral(",");
        handler.AppendFormatted(output);
        return handler.ToStringAndClear();
    }

    /// <summary>使用默认配置压缩字符串</summary>
    /// <param name="str">待压缩字符串</param>
    /// <returns>压缩后的字符串</returns>
    public static String Zip(String str)
    {
        return Zip(str, Deflate, Ascii85);
    }

    /// <summary>解压缩字符串</summary>
    /// <param name="str">压缩后的字符串</param>
    /// <returns>解压后的字符串</returns>
    /// <exception cref="Exception"></exception>
    public static String Unzip(String str)
    {
        var i = PrefixRegex.Match(str);
        if (!i.Success)
            return str;

        var strCompresser = i.Groups["Compresser"].Value;
        var strEncoder = i.Groups["Encoder"].Value;
        var strData = i.Groups["Data"].Value;

        if (!TryGetComponent<ICompressor>(strCompresser, out var compresser))
            throw new Exception("压缩方式不支持:" + strCompresser);

        if (!TryGetComponent<IEncoder>(strEncoder, out var encoder))
            throw new Exception("编码方式不支持:" + strEncoder);

        var bytes = encoder.Decode(strData);
        return compresser.Decompress(bytes);
    }

    /// <summary>Base16 编码器</summary>
    public static IEncoder Base16 => Base16Encoder.Instance;

    /// <summary>Base62 编码器</summary>
    public static IEncoder Base62 => Base62Encoder.Instance;

    /// <summary>Base64 编码器</summary>
    public static IEncoder Base64 => Base64Encoder.Instance;

    /// <summary>Ascii85 编码器</summary>
    public static IEncoder Ascii85 => Ascii85Encoder.Instance;

    /// <summary>LzString 压缩器</summary>
    public static ICompressor LzString => LzStringCompressor.Instance;

    /// <summary>Deflate 压缩器</summary>
    public static ICompressor Deflate => DeflateStreamCompressor.Instance;

    /// <summary>GZip 压缩器</summary>
    public static ICompressor GZip => GZipStreamCompressor.Instance;

    /// <summary>Brotli 压缩器</summary>
    public static ICompressor Br => BrotliStreamCompressor.Instance;

    private static readonly Object SyncRoot = new Object();

    private static IDictionary<String, Object> _components = new SortedDictionary<String, Object>(StringComparer.OrdinalIgnoreCase)
        {
            { LzString.Identifier, LzString },
            { Deflate.Identifier, Deflate },
            { GZip.Identifier, GZip },
            { Br.Identifier, Br },
            { Base16.Identifier, Base16 },
            { Base62.Identifier, Base62 },
            { Base64.Identifier, Base64 },
            { Ascii85.Identifier, Ascii85 }
        };

    private static readonly Regex PrefixRegex = new Regex("^data:text/x-(?<Compresser>\\w+);(?<Encoder>\\w+),(?<Data>.*)", RegexOptions.Compiled);

    /// <summary>压缩器接口</summary>
    public interface ICompressor
    {
        /// <summary>标识符</summary>
        String Identifier { get; }

        /// <summary>压缩</summary>
        /// <param name="value">待压缩字符串</param>
        /// <returns>压缩后的字节数组</returns>
        Byte[] Compress(String value);

        /// <summary>解压</summary>
        /// <param name="data">压缩数据</param>
        /// <returns>解压后的字符串</returns>
        String Decompress(Byte[] data);
    }

    /// <summary>编码器接口</summary>
    public interface IEncoder
    {
        /// <summary>标识符</summary>
        String Identifier { get; }

        /// <summary>编码</summary>
        /// <param name="data">原始字节数组</param>
        /// <returns>编码后的字符串</returns>
        String Encode(Byte[] data);

        /// <summary>解码</summary>
        /// <param name="value">编码字符串</param>
        /// <returns>解码后的字节数组</returns>
        Byte[] Decode(String value);
    }

    private class Base16Encoder : IEncoder
    {
        public static Base16Encoder Instance { get; } = new Base16Encoder();

        public String Identifier => "base16";

        public Byte[] Decode(String value) => Util.Base16.FromBase16(value);

        public String Encode(Byte[] original) => Util.Base16.ToBase16(original);
    }

    private class Base62Encoder : IEncoder
    {
        public static Base62Encoder Instance { get; } = new Base62Encoder();

        public String Identifier => "base62";

        public Byte[] Decode(String value) => Ids.Base62Helper.FromBase62(value, false);

        public String Encode(Byte[] data) => Ids.Base62Helper.ToBase62(data, false);
    }

    private class Base64Encoder : IEncoder
    {
        public static Base64Encoder Instance { get; } = new Base64Encoder();

        public String Identifier => "base64";

        public Byte[] Decode(String value) => Convert.FromBase64String(value);

        public String Encode(Byte[] data) => Convert.ToBase64String(data);
    }

    private class Ascii85Encoder : IEncoder
    {
        public static Ascii85Encoder Instance { get; } = new Ascii85Encoder();

        public String Identifier => "ascii85";

        public Byte[] Decode(String value) => Util.Ascii85.FromAscii85String(value);

        public String Encode(Byte[] data) => Util.Ascii85.ToAscii85String(data);
    }

    private class LzStringCompressor : ICompressor
    {
        public static LzStringCompressor Instance { get; } = new LzStringCompressor();

        public String Identifier => "lzstring";

        public Byte[] Compress(String value) => Util.LzString.CompressToUint8Array(value);

        public String Decompress(Byte[] data) => Util.LzString.DecompressFromUint8Array(data);
    }

    /// <summary>基于 Stream 的压缩器基类</summary>
    /// <typeparam name="TStream">Stream 类型</typeparam>
    public abstract class StreamCompressor<TStream> : ICompressor where TStream : Stream
    {
        /// <summary>标识符</summary>
        public abstract String Identifier { get; }

        /// <summary>创建压缩流</summary>
        /// <param name="stream">基础流</param>
        /// <param name="mode">压缩模式</param>
        /// <param name="leaveOpen">是否保持流开启</param>
        /// <returns>压缩流</returns>
        protected abstract TStream Create(Stream stream, CompressionMode mode, Boolean leaveOpen);

        /// <summary>编码</summary>
        protected virtual Encoding Encoding => Encoding.UTF8;

        /// <summary>压缩</summary>
        /// <param name="value">待压缩字符串</param>
        /// <returns>压缩后的字节数组</returns>
        public virtual Byte[] Compress(String value)
        {
            Byte[] result;
            using (var o = new MemoryStream(Encoding.GetBytes(value)))
            using (var t = new MemoryStream())
            {
                using (Stream c = Create(t, CompressionMode.Compress, true))
                {
                    o.CopyTo(c);
                }
                result = t.ToArray();
            }
            return result;
        }

        /// <summary>解压</summary>
        /// <param name="data">压缩数据</param>
        /// <returns>解压后的字符串</returns>
        public virtual String Decompress(Byte[] data)
        {
            String result;
            using (var o = new MemoryStream(data))
            using (var t = new MemoryStream())
            {
                using (Stream u = Create(o, CompressionMode.Decompress, true))
                {
                    u.CopyTo(t);
                }
                var bytes = t.ToArray();
                result = Encoding.GetString(bytes);
            }
            return result;
        }
    }

    private class DeflateStreamCompressor : StreamCompressor<DeflateStream>
    {
        public static DeflateStreamCompressor Instance { get; } = new DeflateStreamCompressor();

        public override String Identifier => "deflate";

        protected override DeflateStream Create(Stream stream, CompressionMode mode, Boolean leaveOpen) => new DeflateStream(stream, mode, leaveOpen);
    }

    private class GZipStreamCompressor : StreamCompressor<GZipStream>
    {
        public static GZipStreamCompressor Instance { get; } = new GZipStreamCompressor();

        public override String Identifier => "gzip";

        protected override GZipStream Create(Stream stream, CompressionMode mode, Boolean leaveOpen) => new GZipStream(stream, mode, leaveOpen);
    }

    private class BrotliStreamCompressor : StreamCompressor<BrotliStream>
    {
        public static BrotliStreamCompressor Instance { get; } = new BrotliStreamCompressor();

        public override String Identifier => "br";

        protected override BrotliStream Create(Stream stream, CompressionMode mode, Boolean leaveOpen) => new BrotliStream(stream, mode, leaveOpen);
    }
}
#endif
