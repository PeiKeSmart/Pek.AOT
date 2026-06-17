using System.Security.Cryptography;
using System.Text;

using Pek.Caching;
using Pek.Model;
using Pek.Security;

namespace Pek.Ids;

/// <summary>短唯一码生成器。AOT 安全版</summary>
public class ShortUniqueCode
{
    private const Int64 DefaultShortCodeCounter = 238328;
    private const Int32 DefaultShortCodeBackupInterval = 100;

    /// <summary>将整数 ID 转为指定长度的自定义 35 进制短码</summary>
    /// <param name="Id">整数 ID</param>
    /// <param name="Length">长度，默认 6</param>
    public static String CreateCode(Int32 Id, Int32 Length = 6)
    {
        var code = "";
        var source_string = "2YU9IP1ASDFG8QWERTHJ7KLZX4CV5B3ONM6";
        while (Id > 0)
        {
            var mod = Id % 35;
            Id = (Id - mod) / 35;
            code = source_string.ToCharArray()[mod] + code;
        }
        return code.PadRight(Length, '0');
    }

    /// <summary>将短码解码为整数 ID</summary>
    /// <param name="code">短码</param>
    public static Int32 Decode(String code)
    {
        code = new String([.. (from s in code where s != '0' select s)]);
        var num = 0;
        var source_string = "2YU9IP1ASDFG8QWERTHJ7KLZX4CV5B3ONM6";
        for (var i = 0; i < code.ToCharArray().Length; i++)
        {
            for (var j = 0; j < source_string.ToCharArray().Length; j++)
            {
                if (code.ToCharArray()[i] == source_string.ToCharArray()[j])
                    num += j * Convert.ToInt32(Math.Pow(35, code.ToCharArray().Length - i - 1));
            }
        }
        return num;
    }

    /// <summary>短网址生成（基于 MD5）</summary>
    /// <param name="url">原始网址</param>
    /// <returns>4 个候选短码</returns>
    public static String[] ShortUrl(String url)
    {
        var key = "DengHaoNet";
        var chars = new String[]
        {
            "a", "b", "c", "d", "e", "f", "g", "h",
            "i", "j", "k", "l", "m", "n", "o", "p",
            "q", "r", "s", "t", "u", "v", "w", "x",
            "y", "z", "0", "1", "2", "3", "4", "5",
            "6", "7", "8", "9", "A", "B", "C", "D",
            "E", "F", "G", "H", "I", "J", "K", "L",
            "M", "N", "O", "P", "Q", "R", "S", "T",
            "U", "V", "W", "X", "Y", "Z"
        };
        var hex = (key + url).MD5();
        var resUrl = new String[4];
        for (var i = 0; i < 4; i++)
        {
            var hexint = 0x3FFFFFFF & Convert.ToInt32($"0x{hex.Substring(i * 8, 8)}", 16);
            var outChars = String.Empty;
            for (var j = 0; j < 6; j++)
            {
                var index = 0x0000003D & hexint;
                outChars += chars[index];
                hexint >>= 5;
            }
            resUrl[i] = outChars;
        }
        return resUrl;
    }

    /// <summary>将字节数组转为 Base62 编码</summary>
    /// <param name="bytes">字节数组</param>
    public static String ConvertTo62(Byte[] bytes) => Base62Helper.Encode(bytes, false);

    /// <summary>计算字符串的哈希字节数组（AOT: 使用 SHA256 替代 Murmur）</summary>
    /// <param name="str">要哈希的字符串</param>
    public static Byte[] GetMurmurHashBytes(String str)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(str));
    }

    /// <summary>获取下一个短网址 ID（基于哈希 + Base62）</summary>
    /// <param name="url">网址</param>
    /// <param name="salt">盐值</param>
    public static String GetNextCode(String url, Int64 salt)
    {
        var hashurl = url + salt;
        var bytes = GetMurmurHashBytes(hashurl);
        return ConvertTo62(bytes);
    }

    /// <summary>批量获取短码（依赖 Redis 分布式计数器）</summary>
    /// <param name="count">数量</param>
    /// <returns>短码列表</returns>
    public static IList<String> GetShortUrl(Int32 count = 1)
    {
        var provider = ObjectContainer.Provider?.GetService(typeof(ICacheProvider)) as ICacheProvider;
        if (provider != null && provider.Cache != provider.InnerCache && provider.Cache is not MemoryCache)
        {
            var redis = provider.Cache;
            var localBackupCounter = DefaultShortCodeCounter;

            Int64 endCounter;
            var currentCounter = redis.Get<Int64>("shortcode:counter");

            if (currentCounter < localBackupCounter)
            {
                var recoveryLockKey = "shortcode:recovery_lock";
                var lockTimeoutMs = 3000;

                using var distributedLock = provider.Cache.AcquireLock(recoveryLockKey, lockTimeoutMs);

                currentCounter = redis.Get<Int64>("shortcode:counter");
                if (currentCounter < localBackupCounter)
                {
                    var safeCounter = localBackupCounter + DefaultShortCodeBackupInterval;
                    redis.Set("shortcode:counter", safeCounter);
                }

                endCounter = redis.Increment("shortcode:counter", count);
            }
            else
            {
                endCounter = redis.Increment("shortcode:counter", count);
            }

            if (ShouldBackup(endCounter, localBackupCounter))
                Task.Run(() => BackupCounterAsync(redis, endCounter));

            var result = new List<String>(count);
            for (var i = 0; i < count; i++)
            {
                var currentId = endCounter - count + 1 + i;
                result.Add(Base62Helper.Encode(currentId));
            }

            return result;
        }
        else
        {
            throw new Exception("需要 Redis 支持");
        }
    }

    /// <summary>判断是否需要备份计数器</summary>
    private static Boolean ShouldBackup(Int64 endCounter, Int64 localBackupCounter)
    {
        return endCounter - localBackupCounter >= DefaultShortCodeBackupInterval;
    }

    /// <summary>异步备份计数器</summary>
    private static void BackupCounterAsync(ICache redis, Int64 endCounter)
    {
        try
        {
            var backupLockKey = "shortcode:backup_lock";
            var lockTimeoutMs = 5000;

            using var distributedLock = redis.AcquireLock(backupLockKey, lockTimeoutMs);

            // 简化版：Print a log instead of saving to PekSysSetting
            // 实际使用中可替换为自定义持久化逻辑
        }
        catch
        {
            // 备份失败不影响主流程
        }
    }

    /// <summary>将雪花 ID 转换为固定 11 位的 Base62 编码</summary>
    /// <param name="snowflakeId">雪花算法生成的 ID</param>
    public static String GetFixed11DigitCode(Int64 snowflakeId) => GetFixedLengthCode(snowflakeId, 11);

    /// <summary>将雪花 ID 转换为固定长度的 Base62 编码</summary>
    /// <param name="snowflakeId">雪花算法生成的 ID</param>
    /// <param name="fixedLength">固定长度，默认 11 位</param>
    public static String GetFixedLengthCode(Int64 snowflakeId, Int32 fixedLength = 11)
    {
        var base62 = Base62Helper.Encode(snowflakeId);

        if (base62.Length >= fixedLength)
            return base62.Substring(0, fixedLength);

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
