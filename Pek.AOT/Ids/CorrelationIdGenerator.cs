#if NETSTANDARD2_1_OR_GREATER || NET8_0_OR_GREATER

namespace Pek.Ids;

/// <summary>关联 ID 生成器。基于 Base32 编码的 13 位短 ID。AOT 安全版</summary>
public static class CorrelationIdGenerator
{
    /// <summary>Base32 编码字符集，按 ASCII 排序方便文本排序</summary>
    private static readonly Char[] s_encode32Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

    /// <summary>以此应用程序实例的 _lastId 种子，自 0001 年 1 月 1 日午夜以来的 100 纳秒间隔数</summary>
    private static Int64 _lastId = DateTime.UtcNow.Ticks;

    /// <summary>获取下一个 13 位 Base32 编码 ID</summary>
    public static String GetNextId() => GenerateId(Interlocked.Increment(ref _lastId));

    private static String GenerateId(Int64 id)
    {
        return String.Create(13, id, (buffer, value) =>
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
