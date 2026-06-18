using System.Security.Cryptography;
using System.Text;

namespace Pek.Helpers;

/// <summary>哈希摘要算法帮助类。使用工厂函数替代 new() 约束以兼容 NativeAOT</summary>
/// <typeparam name="Algorithm">哈希算法类型，如 MD5、SHA256 等</typeparam>
public class HashAlgorithmHelper<Algorithm>
    where Algorithm : HashAlgorithm
{
    private readonly Func<Algorithm> _factory;

    /// <summary>使用指定工厂函数创建实例</summary>
    /// <param name="factory">算法工厂函数，如 () => MD5.Create()</param>
    public HashAlgorithmHelper(Func<Algorithm> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Hash获取</summary>
    /// <param name="inputBuff"></param>
    /// <returns></returns>
    public Byte[] ComputeHash(Byte[] inputBuff)
    {
        using var alg = _factory();
        return alg.ComputeHash(inputBuff);
    }

    /// <summary>将输入的字符串以指定编码方式获取其字节数组，并将Hash后的数据以0X的方式返回</summary>
    /// <param name="inputString">要Hash的原始数据</param>
    /// <param name="encoding">inputString转化为byte所采用的编码方式，不传递则为utf-8</param>
    /// <param name="upper">是否返回大写，默认大写</param>
    /// <returns></returns>
    public String HashOf(String inputString, Encoding? encoding = null, Boolean upper = true)
    {
        encoding ??= Encoding.UTF8;
        var buff = ComputeHash(encoding.GetBytes(inputString));
        return ConvertToString(buff, upper);
    }

    /// <summary>将byte数组转化为0X格式的字符串</summary>
    /// <param name="data"></param>
    /// <param name="upper"></param>
    /// <returns></returns>
    public static String ConvertToString(Byte[] data, Boolean upper = true)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < data.Length; i++)
        {
            sb.Append(data[i].ToString(upper ? "X2" : "x2"));
        }
        return sb.ToString();
    }

    /// <summary>将0x格式的字符串转化为byte数组</summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static Byte[] ConvertStringToByte(String str)
    {
        if (str == null || str.Length % 2 != 0) throw new ArgumentException();
        var data = new Byte[str.Length / 2];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
        }
        return data;
    }
}
