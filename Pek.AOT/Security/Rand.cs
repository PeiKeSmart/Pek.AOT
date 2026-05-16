using System.Security.Cryptography;

using Pek.Collections;

namespace Pek.Security;

/// <summary>随机数</summary>
public static class Rand
{
    /// <summary>返回一个小于所指定最大值的非负随机数</summary>
    /// <param name="max">返回的随机数的上界，随机数不能取该上界值</param>
    /// <returns></returns>
    public static Int32 Next(Int32 max = Int32.MaxValue)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));

        return RandomNumberGenerator.GetInt32(max);
    }

    /// <summary>返回一个指定范围内的随机数</summary>
    /// <param name="min">返回的随机数的下界，随机数可取该下界值</param>
    /// <param name="max">返回的随机数的上界，随机数不能取该上界值</param>
    /// <returns></returns>
    public static Int32 Next(Int32 min, Int32 max)
    {
        if (max <= min) throw new ArgumentOutOfRangeException(nameof(max));

        return RandomNumberGenerator.GetInt32(min, max);
    }

    /// <summary>返回指定长度随机字节数组</summary>
    /// <param name="count">字节长度</param>
    /// <returns></returns>
    public static Byte[] NextBytes(Int32 count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

        return RandomNumberGenerator.GetBytes(count);
    }

    /// <summary>返回指定长度随机字符串</summary>
    /// <param name="length">长度</param>
    /// <param name="symbol">是否包含符号</param>
    /// <returns></returns>
    public static String NextString(Int32 length, Boolean symbol = false)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        var sb = Pool.StringBuilder.Get();
        for (var i = 0; i < length; i++)
        {
            var ch = ' ';
            if (symbol)
                ch = (Char)Next(' ', 0x7F);
            else
            {
                var n = Next(0, 10 + 26 + 26);
                if (n < 10)
                    ch = (Char)('0' + n);
                else if (n < 10 + 26)
                    ch = (Char)('A' + n - 10);
                else
                    ch = (Char)('a' + n - 10 - 26);
            }
            sb.Append(ch);
        }

        return sb.Return(true);
    }
}