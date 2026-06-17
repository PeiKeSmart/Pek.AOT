using System.Text;

namespace Pek.Ids;

/// <summary>Base62 编码解码工具类。AOT 安全版</summary>
public static class Base62Helper
{
    /// <summary>默认 Base62 字符集：a-z, A-Z, 0-9（共 62 个字符）。适用于 ID 转换场景</summary>
    private static readonly String idChars = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyYzZ0123456789";

    /// <summary>标准 Base62 字符集：0-9, A-Z, a-z（共 62 个字符）。适用于字节数组编码场景</summary>
    private static readonly String standardChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>反转 Base62 字符集：0-9, a-z, A-Z（共 62 个字符）</summary>
    private static readonly String invertedChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    #region ID 转换方法（使用 idChars 字符集）

    /// <summary>将 Int64 数值转换为 Base62 字符串（ID 专用字符集）</summary>
    public static String Encode(Int64 value)
    {
        if (value <= 0) return "0";

        var list = new List<Char>();
        var id = (UInt64)value;

        while (id > 0)
        {
            var remainder = (Int32)(id % 62);
            list.Add(idChars[remainder]);
            id /= 62;
        }

        list.Reverse();
        return new String([.. list]);
    }

    /// <summary>将 UInt32 数值转换为 Base62 字符串（ID 专用字符集）</summary>
    public static String Encode(UInt32 value)
    {
        if (value == 0) return "0";

        var list = new List<Char>();
        var id = value;

        while (id > 0)
        {
            var remainder = (Int32)(id % 62);
            list.Add(idChars[remainder]);
            id /= 62;
        }

        list.Reverse();
        return new String([.. list]);
    }

    /// <summary>将 Base62 字符串解码为 Int64 数值（ID 专用字符集）</summary>
    public static Int64 DecodeToInt64(String base62String)
    {
        if (String.IsNullOrEmpty(base62String)) return 0;

        UInt64 result = 0;
        UInt64 power = 1;

        for (var i = base62String.Length - 1; i >= 0; i--)
        {
            var charIndex = idChars.IndexOf(base62String[i]);
            if (charIndex == -1)
                throw new ArgumentException($"Invalid character '{base62String[i]}' in Base62 string");

            result += (UInt64)charIndex * power;
            power *= 62;
        }

        return (Int64)result;
    }

    /// <summary>将 Base62 字符串解码为 UInt32 数值（ID 专用字符集）</summary>
    public static UInt32 DecodeToUInt32(String base62String)
    {
        if (String.IsNullOrEmpty(base62String)) return 0;

        UInt32 result = 0;
        UInt32 power = 1;

        for (var i = base62String.Length - 1; i >= 0; i--)
        {
            var charIndex = idChars.IndexOf(base62String[i]);
            if (charIndex == -1)
                throw new ArgumentException($"Invalid character '{base62String[i]}' in Base62 string");

            result += (UInt32)charIndex * power;
            power *= 62;
        }

        return result;
    }
    #endregion

    #region 字节数组转换方法（使用标准字符集）

    /// <summary>将字节数组转换为 Base62 字符串（使用标准字符集）</summary>
    public static String Encode(Byte[] bytes, Boolean inverted = false)
    {
        if (bytes == null || bytes.Length == 0) return "0";

        if (bytes.Length == 4)
        {
            var value = BitConverter.ToUInt32(bytes, 0);
            return EncodeNumber(value, inverted);
        }

        return EncodeByteArray(bytes, inverted);
    }

    /// <summary>将 Base62 字符串解码为字节数组（使用标准字符集）</summary>
    public static Byte[] Decode(String base62String, Boolean inverted = false)
    {
        if (String.IsNullOrEmpty(base62String)) return [];

        return DecodeByteArray(base62String, inverted);
    }
    #endregion

    #region 私有辅助方法

    /// <summary>使用简单方法编码数值</summary>
    private static String EncodeNumber(UInt32 value, Boolean inverted)
    {
        if (value == 0) return "0";

        var chars = inverted ? invertedChars : standardChars;
        var list = new List<Char>();

        while (value > 0)
        {
            var remainder = (Int32)(value % 62);
            list.Add(chars[remainder]);
            value /= 62;
        }

        list.Reverse();
        return new String([.. list]);
    }

    /// <summary>使用复杂方法编码字节数组</summary>
    private static String EncodeByteArray(Byte[] original, Boolean inverted)
    {
        var characterSet = inverted ? invertedChars : standardChars;
        var array = BaseConvert(Array.ConvertAll(original, b => (Int32)b), 256, 62);
        var builder = new StringBuilder();
        foreach (var t in array)
        {
            builder.Append(characterSet[t]);
        }
        return builder.ToString();
    }

    /// <summary>解码字节数组</summary>
    private static Byte[] DecodeByteArray(String base62, Boolean inverted)
    {
        if (String.IsNullOrWhiteSpace(base62)) throw new ArgumentNullException(nameof(base62));

        var characterSet = inverted ? invertedChars : standardChars;
        return Array.ConvertAll(
            BaseConvert(
                Array.ConvertAll(base62.ToCharArray(), c => characterSet.IndexOf(c)),
                62,
                256
            ),
            Convert.ToByte
        );
    }

    /// <summary>进制转换核心算法</summary>
    private static Int32[] BaseConvert(Int32[] source, Int32 sourceBase, Int32 targetBase)
    {
        var result = new List<Int32>();
        var leadingZeroCount = Math.Min(source.TakeWhile(x => x == 0).Count(), source.Length - 1);
        Int32 count;
        while ((count = source.Length) > 0)
        {
            var quotient = new List<Int32>();
            var remainder = 0;
            for (var i = 0; i != count; i++)
            {
                var num = source[i] + remainder * sourceBase;
                var digit = num / targetBase;
                remainder = num % targetBase;
                if (quotient.Count > 0 || digit > 0)
                    quotient.Add(digit);
            }
            result.Insert(0, remainder);
            source = [.. quotient];
        }
        result.InsertRange(0, Enumerable.Repeat(0, leadingZeroCount));
        return [.. result];
    }
    #endregion

    #region 工具方法

    /// <summary>验证字符串是否为有效的 Base62 格式（ID 字符集）</summary>
    public static Boolean IsValidBase62(String input)
    {
        if (String.IsNullOrEmpty(input)) return false;
        return input.All(c => idChars.Contains(c));
    }

    /// <summary>验证字符串是否为有效的 Base62 格式（标准字符集）</summary>
    public static Boolean IsValidBase62Standard(String input, Boolean inverted = false)
    {
        if (String.IsNullOrEmpty(input)) return false;
        var chars = inverted ? invertedChars : standardChars;
        return input.All(c => chars.Contains(c));
    }
    #endregion

    #region 兼容原始 Util.Base62 的方法

    /// <summary>将字节数组编码为 Base62 字符串（兼容原始 Util.Base62.ToBase62 方法）</summary>
    public static String ToBase62(Byte[] original, Boolean inverted = false)
    {
        var characterSet = inverted ? invertedChars : standardChars;
        var array = BaseConvert(Array.ConvertAll(original, t => (Int32)t), 256, 62);
        var builder = new StringBuilder();
        foreach (var t2 in array)
        {
            builder.Append(characterSet[t2]);
        }
        return builder.ToString();
    }

    /// <summary>将 Base62 字符串解码为字节数组（兼容原始 Util.Base62.FromBase62 方法）</summary>
    public static Byte[] FromBase62(String base62, Boolean inverted = false)
    {
        if (String.IsNullOrWhiteSpace(base62))
            throw new ArgumentNullException(nameof(base62));

        var characterSet = inverted ? invertedChars : standardChars;
        return Array.ConvertAll(
            BaseConvert(
                Array.ConvertAll(base62.ToCharArray(), new Converter<Char, Int32>(characterSet.IndexOf)),
                62,
                256
            ),
            new Converter<Int32, Byte>(Convert.ToByte)
        );
    }
    #endregion
}
