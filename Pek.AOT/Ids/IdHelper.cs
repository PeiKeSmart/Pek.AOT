using Pek.Data;

namespace Pek.Ids;

/// <summary>ID 生成器。AOT 安全版</summary>
public static class IdHelper
{
    /// <summary>雪花算法实例</summary>
    public static readonly Snowflake snowflake = new();

#if NETSTANDARD2_1_OR_GREATER || NET8_0_OR_GREATER
    /// <summary>获取新的 13 位 Base32 ID 字符串</summary>
    /// <example>0HLV413GIHKK5</example>
    public static String GetNextId() => CorrelationIdGenerator.GetNextId();

    /// <summary>获取 FastGuid 的 ID 字符串</summary>
    public static String GetIdString() => FastGuid.NewGuid().IdString;
#endif

    /// <summary>生成 SessionId（16 进制格式）</summary>
    /// <example>62acfda11f5a4b3c</example>
    public static String GenerateSid()
    {
        var i = 1;
        var byteArray = Guid.NewGuid().ToByteArray();
        foreach (var b in byteArray)
        {
            i *= b + 1;
        }
        return String.Format("{0:x}", i - DateTime.Now.Ticks);
    }

    /// <summary>获取雪花算法 ID</summary>
    /// <returns>Int64 雪花 ID</returns>
    public static Int64 GetSId() => snowflake.NewId();

    /// <summary>获取雪花算法生成的 Base62 格式短 ID</summary>
    /// <returns>Base62 编码的短 ID 字符串，通常为 10-11 位</returns>
    public static String GetShortId() => Base62Helper.Encode(snowflake.NewId());

    /// <summary>将 Base62 字符串转换回雪花算法的 Int64 ID</summary>
    /// <param name="base62String">Base62 编码的字符串</param>
    /// <returns>原始的雪花算法 ID</returns>
    public static Int64 ConvertBase62ToSnowflake(String base62String) => Base62Helper.DecodeToInt64(base62String);

    /// <summary>获取雪花算法生成的固定长度 Base62 格式短 ID。不能逆向转换为原始雪花 ID</summary>
    /// <param name="fixedLength">固定长度，默认 11 位</param>
    /// <returns>固定长度的 Base62 编码字符串</returns>
    public static String GetFixedLengthId(Int32 fixedLength = 11) => ShortUniqueCode.GetFixedLengthCode(snowflake.NewId(), fixedLength);
}
