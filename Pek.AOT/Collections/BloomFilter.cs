using System.Collections;
using System.Security.Cryptography;
using System.Text;

using Pek.IO;

namespace Pek.Collections;

/// <summary>布隆过滤器</summary>
public class BloomFilter
{
    private readonly BitArray _container;
    private readonly Int32 _M;
    private readonly Int32 _K;

    /// <summary>位数组大小</summary>
    public Int32 Length => _M;

    /// <summary>K 值，循环哈希次数</summary>
    public Int32 K => _K;

    /// <summary>实例化布隆过滤器</summary>
    /// <param name="length">位数组大小</param>
    public BloomFilter(Int32 length)
    {
        _container = new BitArray(length);
        _M = length;
        _K = 4;
    }

    /// <summary>实例化布隆过滤器</summary>
    /// <param name="n">预估数据量</param>
    /// <param name="fpp">期望误判率</param>
    public BloomFilter(Int64 n, Double fpp)
    {
        if (fpp is <= 0 or >= 1) fpp = 0.0001;

        _M = (Int32)(-n * Math.Log(fpp) / (Math.Log(2) * Math.Log(2)));
        _K = Math.Max(1, (Int32)Math.Round(_M / (Double)n * Math.Log(2)));
        _container = new BitArray(_M);
    }

    /// <summary>从字节数组初始化布隆过滤器</summary>
    /// <param name="values">位数组数据</param>
    public BloomFilter(Byte[] values)
    {
        _container = new BitArray(values);
        _M = _container.Length;
        _K = 4;
    }

    /// <summary>设置指定键进入集合</summary>
    /// <param name="key">键</param>
    public void Set(String key)
    {
        var hash = Hash(key);
        var hash1 = BitConverter.ToUInt64(hash, 0);
        var hash2 = BitConverter.ToUInt64(hash, 8);

        var current = hash1;
        for (var i = 0; i < _K; i++)
        {
            _container[(Int32)((Int64)(current & Int64.MaxValue) % _M)] = true;
            current += hash2;
        }
    }

    /// <summary>判断指定键是否存在于集合中</summary>
    /// <param name="key">键</param>
    /// <returns>是否可能存在</returns>
    public Boolean Get(String key)
    {
        var hash = Hash(key);
        var hash1 = BitConverter.ToUInt64(hash, 0);
        var hash2 = BitConverter.ToUInt64(hash, 8);

        var current = hash1;
        for (var i = 0; i < _K; i++)
        {
            if (!_container[(Int32)((Int64)(current & Int64.MaxValue) % _M)]) return false;
            current += hash2;
        }

        return true;
    }

    /// <summary>导出内部字节数组</summary>
    /// <returns>字节数组</returns>
    public Byte[] GetBytes()
    {
        var length = (_container.Length + 7) / 8;
        var buffer = new Byte[length];
        _container.CopyTo(buffer, 0);
        return buffer;
    }

    /// <summary>导出内部 Base64 字符串</summary>
    /// <returns>Base64 字符串</returns>
    public String GetString() => GetBytes().ToBase64();

    private static Byte[] Hash(String key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

#if NET8_0_OR_GREATER
        return MD5.HashData(Encoding.UTF8.GetBytes(key));
#else
        using var md5 = MD5.Create();
        return md5.ComputeHash(Encoding.UTF8.GetBytes(key));
#endif
    }
}