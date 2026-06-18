using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Pek.Helpers;

/// <summary>随机工具类</summary>
public static class RandomUtilities
{
    private static readonly Random _random = new();

    /// <summary>获取指定范围内的随机整数</summary>
    public static Int32 GetRandomInt(Int32 min, Int32 max, Int32[]? excludeValues = null)
    {
        if (min == max) return min;
        if (max < min) ConvertUtilities.Switch(ref min, ref max);
        var value = _random.Next(min, max + 1);
        if (excludeValues != null && excludeValues.Contains(value))
            return GetRandomInt(max, excludeValues);
        return value;
    }

    /// <summary>获取随机索引</summary>
    public static Int32 GetRandomIndex(Int32 maxCount) => _random.Next(0, maxCount);

    /// <summary>获取随机正整数</summary>
    public static Int32 GetRandomPositiveInt(Int32 maxCount) => _random.Next(1, maxCount);

    /// <summary>获取指定范围内的随机整数（对称范围）</summary>
    public static Int32 GetRandomInt(Int32 max, Int32[]? excludeValues = null)
    {
        var value = _random.Next(-1 * max, max + 1);
        if (excludeValues != null && excludeValues.Contains(value))
            return GetRandomInt(max, excludeValues);
        return value;
    }

    /// <summary>从字符串中获取随机字符</summary>
    public static Char GetRandomChar(this String text) => text[GetRandomIndex(text.Length)];

    /// <summary>从列表中获取随机值</summary>
    public static T GetRandomValue<T>(this IReadOnlyList<T> items) => items[GetRandomIndex(items.Count)];

    /// <summary>获取随机枚举值</summary>
    public static T GetRandomEnum<T>() where T : Enum
    {
        var enumValues = Enum.GetValues(typeof(T)).Cast<Int32>().ToArray();
        return (T)(Object)enumValues.GetRandomValue();
    }

    /// <summary>生成随机布尔值</summary>
    public static Boolean GenerateBool() => GetRandomInt(1, 2) == 1;

    /// <summary>随机排序</summary>
    public static IEnumerable<T> OrderByRandom<T>(this IEnumerable<T> items) => items.OrderBy(_ => GetRandomIndex(100000));
}

/// <summary>数组工具类</summary>
public static class ArrayUtilities
{
    /// <summary>判断数组是否为null或空</summary>
    public static Boolean IsNullOrEmpty(this Array? array)
    {
        return array == null || array.Length == 0;
    }
}

// AOT: skipped - XmlUtilities 使用 XmlSerializer，依赖动态代码生成，与 NativeAOT 不兼容
// 原 Pek.Common Utilities.cs 中的 FromXml<T> 和 ToXml<T> 方法已略去。
// 如需 XML 序列化，请在 AOT 中使用 XmlSerializer 源生成或改用 DataContractSerializer。

/// <summary>转换工具类</summary>
public static class ConvertUtilities
{
    /// <summary>字符串解析为目标类型（使用 Convert.ChangeType）</summary>
    public static Object ParseTo(this String str, Type toType) => Convert.ChangeType(str, toType, CultureInfo.InvariantCulture);

    /// <summary>交换两个值</summary>
    public static void Switch<T>(ref T obj1, ref T obj2) => (obj2, obj1) = (obj1, obj2);

    /// <summary>根据文本生成确定性的Guid</summary>
    public static Guid GenerateGuid(this String text)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
        return new Guid(hash);
    }

    /// <summary>格式化为保留一位小数的字符串</summary>
    public static String To1CString(this Double d) => d.ToString("N1");

    /// <summary>格式化为保留一位小数的字符串</summary>
    public static String To1CString(this Single f) => f.ToString("N1");

    /// <summary>按数量限制拆分集合</summary>
    public static IEnumerable<IEnumerable<T>> SplitByCount<T>(this IEnumerable<T> items, Int32 countLimit) => items.SplitByLimit(countLimit, _ => 1);

    /// <summary>按长度限制拆分集合</summary>
    public static IEnumerable<IEnumerable<T>> SplitByLimit<T>(this IEnumerable<T> items, Int32 lengthLimit, Func<T, Int32> getLength)
    {
        var result = new List<T>();
        var totalLength = 0;
        foreach (var item in items)
        {
            var length = getLength(item);
            if (totalLength + length > lengthLimit && result.Count != 0)
            {
                yield return result.ToArray();
                result.Clear();
                totalLength = 0;
            }
            totalLength += length;
            result.Add(item);
        }
        if (result.Count != 0) yield return result;
    }

    /// <summary>按等长限制拆分集合</summary>
    public static IEnumerable<IEnumerable<T>> SplitByEqualLimit<T>(this IEnumerable<T> items, Int32 lengthLimit, Func<T, Int32> getLength)
    {
        var result = new List<T>();
        var maxLength = 0;
        foreach (var item in items)
        {
            var length = getLength(item);
            if (maxLength < length) maxLength = length;
            if (maxLength * (result.Count + 1) > lengthLimit && result.Count != 0)
            {
                yield return result.ToArray();
                result.Clear();
                maxLength = 0;
            }
            result.Add(item);
        }
        if (result.Count != 0) yield return result;
    }

    /// <summary>根据初始值和递推函数生成序列</summary>
    public static IEnumerable<T> SequenceElements<T>(Int32 count, IEnumerable<T> initialPreviousValues, Func<T[], T> getNextValue)
    {
        var previousItems = initialPreviousValues.ToList();
        for (var i = 0; i < count; ++i)
        {
            var value = getNextValue([.. previousItems]);
            previousItems.Add(value);
            yield return value;
        }
    }
}

/// <summary>日志工具类</summary>
public static class LogUtilities
{
    private static readonly Object _lock = new();

    /// <summary>记录异常到文件</summary>
    public static void Log(String filePath, Int32 maxSizeInKb, Exception exception) => Log(filePath, maxSizeInKb, exception.ToString());

    /// <summary>记录消息到文件</summary>
    public static void Log(String filePath, Int32 maxSizeInKb, String message)
    {
        var dateTime = DateTime.Now.ToString("dd'.'MM'.'yy' 'HH':'mm':'ss");
        var logContent = $"{dateTime}{Environment.NewLine}{message}";
        lock (_lock)
        {
            if (File.Exists(filePath) && maxSizeInKb > 0 && new FileInfo(filePath).Length > maxSizeInKb * 1024)
            {
                var lines = File.ReadAllLines(filePath);
                File.WriteAllLines(filePath, lines.Skip(lines.Length / 2));
            }
            File.AppendAllText(filePath, Environment.NewLine + Environment.NewLine + logContent);
        }
    }
}

/// <summary>文件监视工具类</summary>
public static class FileUtilities
{
    /// <summary>简单文件监视</summary>
    public static void SimpleWatchFiles(this FileSystemWatcher watcher, Action action)
    {
        watcher.EnableRaisingEvents = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.Changed += (_, _) => action();
        watcher.Created += (_, _) => action();
        watcher.Deleted += (_, _) => action();
        watcher.Renamed += (_, _) => action();
    }
}
