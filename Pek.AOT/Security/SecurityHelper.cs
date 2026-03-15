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
        var buffer = MD5(encoding.GetBytes(data + String.Empty));
        return buffer.AsSpan().ToHex();
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
}