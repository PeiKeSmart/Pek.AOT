#if NETSTANDARD2_1_OR_GREATER || NET8_0_OR_GREATER

namespace Pek.Ids;

/// <summary>快速 Guid 生成器。基于 Base32 编码的 13 位短 ID。AOT 安全版</summary>
public sealed class FastGuid
{
    /// <summary>Base32 编码字符集，按 ASCII 排序方便文本排序</summary>
    private static readonly Char[] s_encode32Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

    /// <summary>全局递增 ID</summary>
    private static Int64 NextId = InitializeNextId();

    /// <summary>缓存的 ID 字符串</summary>
    private String? _idString;

    /// <summary>内部 ID 值</summary>
    internal Int64 IdValue { get; }

    /// <summary>ID 字符串表示</summary>
    public String IdString
    {
        get
        {
            if (_idString == null)
                _idString = GenerateGuidString(this);
            return _idString;
        }
    }

    /// <summary>使用 Guid 初始化全局 ID 种子</summary>
    private static Int64 InitializeNextId()
    {
        var guidBytes = Guid.NewGuid().ToByteArray();

        return
            guidBytes[0] << 32 |
            guidBytes[1] << 40 |
            guidBytes[2] << 48 |
            guidBytes[3] << 56;
    }

    internal FastGuid(Int64 id)
    {
        IdValue = id;
    }

    /// <summary>生成新的 FastGuid</summary>
    public static FastGuid NewGuid()
    {
        return new FastGuid(Interlocked.Increment(ref NextId));
    }

    private static String GenerateGuidString(FastGuid guid)
    {
        return String.Create(13, guid.IdValue, (buffer, value) =>
        {
            var encode32Chars = s_encode32Chars;
            buffer[12] = encode32Chars[value & 31];
            buffer[11] = encode32Chars[(value >> 5) & 31];
            buffer[10] = encode32Chars[(value >> 10) & 31];
            buffer[9] = encode32Chars[(value >> 15) & 31];
            buffer[8] = encode32Chars[(value >> 20) & 31];
            buffer[7] = encode32Chars[(value >> 25) & 31];
            buffer[6] = encode32Chars[(value >> 30) & 31];
            buffer[5] = encode32Chars[(value >> 35) & 31];
            buffer[4] = encode32Chars[(value >> 40) & 31];
            buffer[3] = encode32Chars[(value >> 45) & 31];
            buffer[2] = encode32Chars[(value >> 50) & 31];
            buffer[1] = encode32Chars[(value >> 55) & 31];
            buffer[0] = encode32Chars[(value >> 60) & 31];
        });
    }
}
#endif
