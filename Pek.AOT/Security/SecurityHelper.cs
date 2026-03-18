using System.Buffers;
using System.Security.Cryptography;
using System.Text;

using Pek.Buffers;

namespace Pek.Security;

/// <summary>安全算法</summary>
public static class SecurityHelper
{
    [ThreadStatic]
    private static MD5? _md5;

    /// <summary>MD5散列</summary>
    /// <param name="data">数据</param>
    /// <returns>散列值</returns>
    public static Byte[] MD5(this Byte[] data)
    {
        _md5 ??= System.Security.Cryptography.MD5.Create();
        return _md5.ComputeHash(data);
    }

    /// <summary>MD5散列</summary>
    /// <param name="data">字符串</param>
    /// <param name="encoding">编码</param>
    /// <returns>散列值</returns>
    public static String MD5(this String data, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var maxByteCount = Math.Max(encoding.GetMaxByteCount(data.Length), 16);
        Byte[]? rented = null;
        Span<Byte> source = maxByteCount <= 256 ? stackalloc Byte[maxByteCount] : (rented = ArrayPool<Byte>.Shared.Rent(maxByteCount));
        try
        {
            var sourceLength = encoding.GetBytes(data.AsSpan(), source);

            Span<Byte> hash = stackalloc Byte[16];
            _md5 ??= System.Security.Cryptography.MD5.Create();
            _md5.TryComputeHash(source[..sourceLength], hash, out _);

            return hash.ToHex();
        }
        finally
        {
            if (rented != null) ArrayPool<Byte>.Shared.Return(rented);
        }
    }

    /// <summary>MD5散列16位</summary>
    /// <param name="data">字符串</param>
    /// <param name="encoding">编码</param>
    /// <returns>16位散列值</returns>
    public static String MD5_16(this String data, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var buffer = MD5(encoding.GetBytes(data + String.Empty));
        return buffer.AsSpan(0, 8).ToHex();
    }

    /// <summary>计算文件的MD5散列</summary>
    /// <param name="file">文件</param>
    /// <returns>散列值</returns>
    public static Byte[] MD5(this FileInfo file)
    {
        _md5 ??= System.Security.Cryptography.MD5.Create();
        using var stream = file.OpenRead();
        return _md5.ComputeHash(stream);
    }

    /// <summary>Crc散列</summary>
    /// <param name="data">数据</param>
    /// <returns>校验值</returns>
    public static UInt32 Crc(this Byte[] data) => new Crc32().Update(data).Value;

    /// <summary>SHA1散列</summary>
    /// <param name="data">数据</param>
    /// <param name="key">可选密钥。指定时使用 HMACSHA1</param>
    /// <returns>散列值</returns>
    public static Byte[] SHA1(this Byte[] data, Byte[]? key = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

#if NET6_0_OR_GREATER
        return key == null ? System.Security.Cryptography.SHA1.HashData(data) : HMACSHA1.HashData(key, data);
#else
        return key == null ? System.Security.Cryptography.SHA1.Create().ComputeHash(data) : new HMACSHA1(key).ComputeHash(data);
#endif
    }

    /// <summary>SHA256散列</summary>
    /// <param name="data">数据</param>
    /// <param name="key">可选密钥。指定时使用 HMACSHA256</param>
    /// <returns>散列值</returns>
    public static Byte[] SHA256(this Byte[] data, Byte[]? key = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

#if NET6_0_OR_GREATER
        return key == null ? System.Security.Cryptography.SHA256.HashData(data) : HMACSHA256.HashData(key, data);
#else
        return key == null ? System.Security.Cryptography.SHA256.Create().ComputeHash(data) : new HMACSHA256(key).ComputeHash(data);
#endif
    }

    /// <summary>SHA384散列</summary>
    /// <param name="data">数据</param>
    /// <param name="key">可选密钥。指定时使用 HMACSHA384</param>
    /// <returns>散列值</returns>
    public static Byte[] SHA384(this Byte[] data, Byte[]? key = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

#if NET6_0_OR_GREATER
        return key == null ? System.Security.Cryptography.SHA384.HashData(data) : HMACSHA384.HashData(key, data);
#else
        return key == null ? System.Security.Cryptography.SHA384.Create().ComputeHash(data) : new HMACSHA384(key).ComputeHash(data);
#endif
    }

    /// <summary>SHA512散列</summary>
    /// <param name="data">数据</param>
    /// <param name="key">可选密钥。指定时使用 HMACSHA512</param>
    /// <returns>散列值</returns>
    public static Byte[] SHA512(this Byte[] data, Byte[]? key = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

#if NET6_0_OR_GREATER
        return key == null ? System.Security.Cryptography.SHA512.HashData(data) : HMACSHA512.HashData(key, data);
#else
        return key == null ? System.Security.Cryptography.SHA512.Create().ComputeHash(data) : new HMACSHA512(key).ComputeHash(data);
#endif
    }

    /// <summary>对称加密算法扩展</summary>
    /// <remarks>注意：CryptoStream 会关闭输出流</remarks>
    /// <param name="algorithm">算法实例</param>
    /// <param name="inputStream">输入流</param>
    /// <param name="outputStream">输出流</param>
    /// <returns>算法实例</returns>
    public static SymmetricAlgorithm Encrypt(this SymmetricAlgorithm algorithm, Stream inputStream, Stream outputStream)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));
        if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));

        using (var stream = new CryptoStream(outputStream, algorithm.CreateEncryptor(), CryptoStreamMode.Write))
        {
            inputStream.CopyTo(stream);
            stream.FlushFinalBlock();
        }

        return algorithm;
    }

    /// <summary>对称加密算法扩展</summary>
    /// <remarks>CBC 填充依赖 IV，要求加解密的 IV 一致，而 ECB 不需要</remarks>
    /// <param name="algorithm">算法</param>
    /// <param name="data">数据</param>
    /// <param name="pass">密码</param>
    /// <param name="mode">模式，.NET 默认 CBC</param>
    /// <param name="padding">填充算法，默认 PKCS7</param>
    /// <returns>加密后的数据</returns>
    public static Byte[] Encrypt(this SymmetricAlgorithm algorithm, Byte[] data, Byte[]? pass = null, CipherMode mode = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        if (data == null || data.Length <= 0) throw new ArgumentNullException(nameof(data));

        PrepareAlgorithm(algorithm, pass, mode, padding);

        using var outputStream = new MemoryStream();
        using var stream = new CryptoStream(outputStream, algorithm.CreateEncryptor(), CryptoStreamMode.Write);
        stream.Write(data, 0, data.Length);

        if (algorithm.Padding == PaddingMode.None)
        {
            var length = data.Length % 8;
            if (length > 0)
            {
                var buffer = new Byte[8 - length];
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        stream.FlushFinalBlock();
        return outputStream.ToArray();
    }

    /// <summary>对称解密算法扩展</summary>
    /// <remarks>注意：CryptoStream 会关闭输入流</remarks>
    /// <param name="algorithm">算法实例</param>
    /// <param name="inputStream">输入流</param>
    /// <param name="outputStream">输出流</param>
    /// <returns>算法实例</returns>
    public static SymmetricAlgorithm Decrypt(this SymmetricAlgorithm algorithm, Stream inputStream, Stream outputStream)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));
        if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));

        using (var stream = new CryptoStream(inputStream, algorithm.CreateDecryptor(), CryptoStreamMode.Read))
        {
            stream.CopyTo(outputStream);
        }

        return algorithm;
    }

    /// <summary>对称解密算法扩展</summary>
    /// <remarks>CBC 填充依赖 IV，要求加解密的 IV 一致，而 ECB 不需要</remarks>
    /// <param name="algorithm">算法</param>
    /// <param name="data">数据</param>
    /// <param name="pass">密码</param>
    /// <param name="mode">模式，.NET 默认 CBC</param>
    /// <param name="padding">填充算法，默认 PKCS7</param>
    /// <returns>解密后的数据</returns>
    public static Byte[] Decrypt(this SymmetricAlgorithm algorithm, Byte[] data, Byte[]? pass = null, CipherMode mode = CipherMode.CBC, PaddingMode padding = PaddingMode.PKCS7)
    {
        if (algorithm == null) throw new ArgumentNullException(nameof(algorithm));
        if (data == null || data.Length <= 0) throw new ArgumentNullException(nameof(data));

        PrepareAlgorithm(algorithm, pass, mode, padding);

        using var inputStream = new MemoryStream(data);
        using var stream = new CryptoStream(inputStream, algorithm.CreateDecryptor(), CryptoStreamMode.Read);
        using var outputStream = new MemoryStream();
        stream.CopyTo(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>转换数据</summary>
    /// <param name="transform">转换器</param>
    /// <param name="data">原始数据</param>
    /// <returns>转换后的数据</returns>
    public static Byte[] Transform(this ICryptoTransform transform, Byte[] data)
    {
        if (transform == null) throw new ArgumentNullException(nameof(transform));
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (data.Length <= transform.InputBlockSize)
            return transform.TransformFinalBlock(data, 0, data.Length);

        var blocks = data.Length / transform.InputBlockSize;
        var inputCount = blocks * transform.InputBlockSize;
        if (inputCount < data.Length) blocks++;

        var output = new Byte[blocks * transform.OutputBlockSize];
        var count = 0;
        if (inputCount > 0 && transform.CanTransformMultipleBlocks)
        {
            count = transform.TransformBlock(data, 0, inputCount, output, 0);
        }
        else
        {
            var inputOffset = 0;
            var outputOffset = 0;
            while (inputOffset < inputCount)
            {
                count += transform.TransformBlock(data, inputOffset, transform.InputBlockSize, output, outputOffset);
                inputOffset += transform.InputBlockSize;
                outputOffset += transform.OutputBlockSize;
            }
        }

        if (count == data.Length) return output;

        var remain = transform.TransformFinalBlock(data, count, data.Length - count);
        Buffer.BlockCopy(remain, 0, output, count, remain.Length);
        return output;
    }

    private static void PrepareAlgorithm(SymmetricAlgorithm algorithm, Byte[]? pass, CipherMode mode, PaddingMode padding)
    {
        if (pass == null || pass.Length == 0) return;

        if (algorithm.LegalKeySizes != null && algorithm.LegalKeySizes.Length > 0)
            algorithm.Key = Pad(pass, algorithm.LegalKeySizes[0]);
        else
            algorithm.Key = pass;

        var iv = new Byte[algorithm.IV.Length];
        Buffer.BlockCopy(pass, 0, iv, 0, Math.Min(pass.Length, iv.Length));
        algorithm.IV = iv;
        algorithm.Mode = mode;
        algorithm.Padding = padding;
    }

    private static Byte[] Pad(Byte[] buffer, KeySizes keySize)
    {
        var bitSize = buffer.Length * 8;
        var size = 0;
        for (var i = keySize.MinSize; i <= keySize.MaxSize; i += keySize.SkipSize)
        {
            if (i >= bitSize)
            {
                size = i / 8;
                break;
            }

            if (keySize.SkipSize == 0) break;
        }

        if (size == 0) size = keySize.MaxSize / 8;
        if (buffer.Length == size) return buffer;

        var result = new Byte[size];
        Buffer.BlockCopy(buffer, 0, result, 0, Math.Min(buffer.Length, result.Length));
        return result;
    }
}