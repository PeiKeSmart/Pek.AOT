namespace Pek.Ids;

/// <summary>短唯一码生成器（精简版）。AOT 安全版</summary>
public class ShortUniqueCode
{
    /// <summary>将雪花 ID 转换为固定 11 位的 Base62 编码</summary>
    /// <param name="snowflakeId">雪花算法生成的 ID</param>
    /// <returns>固定 11 位的 Base62 字符串</returns>
    public static String GetFixed11DigitCode(Int64 snowflakeId) => GetFixedLengthCode(snowflakeId, 11);

    /// <summary>将雪花 ID 转换为固定长度的 Base62 编码</summary>
    /// <param name="snowflakeId">雪花算法生成的 ID</param>
    /// <param name="fixedLength">固定长度，默认 11 位</param>
    /// <returns>固定长度的 Base62 字符串</returns>
    public static String GetFixedLengthCode(Int64 snowflakeId, Int32 fixedLength = 11)
    {
        var base62 = Base62Helper.Encode(snowflakeId);

        if (base62.Length >= fixedLength)
            return base62.Substring(0, fixedLength);

        // 不足指定长度则基于雪花 ID 确定性补位
        var needLength = fixedLength - base62.Length;
        var random = new Random((Int32)(snowflakeId & 0xFFFFFFFF));

        var base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var suffix = "";
        for (var i = 0; i < needLength; i++)
        {
            suffix += base62Chars[random.Next(62)];
        }

        return base62 + suffix;
    }
}
