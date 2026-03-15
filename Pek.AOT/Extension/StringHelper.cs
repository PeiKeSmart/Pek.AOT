using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

using Pek.Collections;
using Pek;

namespace Pek.Extension;

/// <summary>字符串辅助扩展</summary>
public static class StringHelper
{
    /// <summary>忽略大小写判断是否等于任意一个候选字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean EqualIgnoreCase(this String? value, params String?[] values)
    {
        if (values == null || values.Length == 0) return false;

        foreach (var item in values)
        {
            if (String.Equals(value, item, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>忽略大小写判断是否以任意候选前缀开始</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean StartsWithIgnoreCase(this String? value, params String?[] values)
    {
        if (value.IsNullOrEmpty()) return false;
        if (values == null || values.Length == 0) return false;

        foreach (var item in values)
        {
            if (!item.IsNullOrEmpty() && value.StartsWith(item, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>忽略大小写判断是否以任意候选后缀结束</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean EndsWithIgnoreCase(this String? value, params String?[] values)
    {
        if (value.IsNullOrEmpty()) return false;
        if (values == null || values.Length == 0) return false;

        foreach (var item in values)
        {
            if (!item.IsNullOrEmpty() && value.EndsWith(item, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>指示指定的字符串是 null 还是 String.Empty 字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean IsNullOrEmpty([NotNullWhen(false)] this String? value) => value == null || value.Length == 0;

    /// <summary>是否空或者空白字符串</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean IsNullOrWhiteSpace([NotNullWhen(false)] this String? value)
    {
        if (value != null)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (!Char.IsWhiteSpace(value[i])) return false;
            }
        }

        return true;
    }

    /// <summary>拆分字符串，过滤空项</summary>
    public static String[] Split(this String? value, params String[] separators)
    {
        if (value.IsNullOrEmpty()) return [];
        if (separators == null || separators.Length == 0 || separators.Length == 1 && separators[0].IsNullOrEmpty()) return [value];

        return value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>拆分字符串成为整型数组，过滤空项和非法值</summary>
    /// <param name="value">字符串</param>
    /// <param name="separators">分隔符，默认为逗号和分号</param>
    /// <returns>整型数组</returns>
    public static Int32[] SplitAsInt(this String? value, params String[] separators)
    {
        if (value.IsNullOrEmpty()) return [];
        if (separators == null || separators.Length == 0) separators = [",", ";"];

        var segments = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        var list = new List<Int32>(segments.Length);
        foreach (var item in segments)
        {
            if (!Int32.TryParse(item.Trim(), out var id)) continue;
            list.Add(id);
        }

        return [.. list];
    }

    /// <summary>拆分字符串成为不区分大小写的可空名值字典</summary>
    /// <param name="value">字符串</param>
    /// <param name="nameValueSeparator">名值分隔符，默认等号</param>
    /// <param name="separator">分组分隔符，默认分号</param>
    /// <param name="trimQuotation">是否去掉值两端引号</param>
    /// <returns>大小写不敏感字典</returns>
    public static IDictionary<String, String> SplitAsDictionary(this String? value, String nameValueSeparator = "=", String separator = ";", Boolean trimQuotation = false)
    {
        var dictionary = new NullableDictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        if (value.IsNullOrWhiteSpace()) return dictionary;

        if (nameValueSeparator.IsNullOrEmpty()) nameValueSeparator = "=";

        var segments = value.Split([separator], StringSplitOptions.RemoveEmptyEntries);
        if (segments == null || segments.Length == 0) return dictionary;

        var index = 0;
        foreach (var item in segments)
        {
            var position = item.IndexOf(nameValueSeparator, StringComparison.Ordinal);
            if (position <= 0)
            {
                dictionary[$"[{index}]"] = item;
                index++;
                continue;
            }

            var key = item[..position].Trim();
            var text = item[(position + nameValueSeparator.Length)..].Trim();
            if (trimQuotation && !text.IsNullOrEmpty())
            {
                if (text[0] == '\'' && text[^1] == '\'') text = text.Trim('\'');
                if (text[0] == '"' && text[^1] == '"') text = text.Trim('"');
            }

#if NETFRAMEWORK || NETSTANDARD2_0
            if (!dictionary.ContainsKey(key)) dictionary.Add(key, text);
#else
            dictionary.TryAdd(key, text);
#endif
        }

        return dictionary;
    }

    /// <summary>把一个列表组合成为一个字符串，默认逗号分隔</summary>
    /// <param name="value">序列</param>
    /// <param name="separator">组合分隔符</param>
    /// <returns>拼接后的字符串</returns>
    public static String Join(this IEnumerable? value, String separator = ",")
    {
        var builder = Pool.StringBuilder.Get();
        if (value != null)
        {
            foreach (var item in value)
            {
                builder.Separate(separator).Append(item + String.Empty);
            }
        }

        return builder.Return(true);
    }

    /// <summary>把一个泛型列表组合成为一个字符串，默认逗号分隔</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="value">序列</param>
    /// <param name="separator">组合分隔符</param>
    /// <param name="func">对象转字符串委托</param>
    /// <returns>拼接后的字符串</returns>
    public static String Join<T>(this IEnumerable<T>? value, String separator = ",", Func<T, Object?>? func = null)
    {
        var builder = Pool.StringBuilder.Get();
        if (value != null)
        {
            func ??= item => item;
            foreach (var item in value)
            {
                builder.Separate(separator).Append(func(item));
            }
        }

        return builder.Return(true);
    }

    /// <summary>追加分隔符字符串，忽略开头，常用于拼接</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringBuilder Separate(this StringBuilder builder, String separator)
    {
        if (String.IsNullOrEmpty(separator)) return builder;
        if (builder.Length > 0) builder.Append(separator);
        return builder;
    }

    /// <summary>按通配符匹配字符串，支持 * 和 ?</summary>
    public static Boolean IsMatch(this String pattern, String input, StringComparison comparisonType = StringComparison.CurrentCulture)
    {
        if (pattern.IsNullOrEmpty()) return false;
        if (pattern == "*") return true;
        if (input.IsNullOrEmpty()) return false;

        var hasStar = pattern.IndexOf('*') >= 0;
        var hasQuestion = pattern.IndexOf('?') >= 0;
        if (!hasStar && !hasQuestion) return String.Equals(input, pattern, comparisonType);

        var patternIndex = 0;
        var inputIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (inputIndex < input.Length)
        {
            if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || CharEquals(pattern[patternIndex], input[inputIndex], comparisonType)))
            {
                patternIndex++;
                inputIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                matchIndex = inputIndex;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                matchIndex++;
                inputIndex = matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*') patternIndex++;
        return patternIndex == pattern.Length;

        static Boolean CharEquals(Char left, Char right, StringComparison comparisonType)
        {
            if (left == right) return true;

            return comparisonType switch
            {
                StringComparison.OrdinalIgnoreCase => Char.ToUpperInvariant(left) == Char.ToUpperInvariant(right),
                StringComparison.CurrentCultureIgnoreCase => Char.ToUpper(left, System.Globalization.CultureInfo.CurrentCulture) == Char.ToUpper(right, System.Globalization.CultureInfo.CurrentCulture),
                StringComparison.InvariantCultureIgnoreCase => Char.ToUpper(left, System.Globalization.CultureInfo.InvariantCulture) == Char.ToUpper(right, System.Globalization.CultureInfo.InvariantCulture),
                _ => false,
            };
        }
    }

    /// <summary>字符串转字节数组</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Byte[] GetBytes(this String? value, Encoding? encoding = null)
    {
        if (value.IsNullOrEmpty()) return [];
        encoding ??= Encoding.UTF8;
        return encoding.GetBytes(value);
    }

    /// <summary>格式化字符串。特别支持无格式化字符串的时间参数</summary>
    /// <param name="value">格式字符串</param>
    /// <param name="args">参数</param>
    /// <returns>格式化结果</returns>
    [Obsolete("建议使用插值字符串")]
    public static String F(this String value, params Object?[] args)
    {
        if (String.IsNullOrEmpty(value)) return value;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is DateTime dateTime)
            {
                if (value.Contains("{" + i + "}")) args[i] = dateTime.ToFullString();
            }
        }

        return String.Format(value, args);
    }

    /// <summary>判断字符串是否包含字符</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Boolean Contains(this String value, Char inputChar) => value.IndexOf(inputChar) >= 0;

    /// <summary>按单字符拆分字符串</summary>
    /// <param name="value">字符串</param>
    /// <param name="separator">分隔符</param>
    /// <param name="options">拆分选项</param>
    /// <returns>拆分后的数组</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static String[] Split(this String value, Char separator, StringSplitOptions options = StringSplitOptions.None) => value.Split([separator], options);

    /// <summary>确保字符串以前缀开头</summary>
    /// <param name="value">字符串</param>
    /// <param name="start">前缀</param>
    /// <returns>处理后的字符串</returns>
    public static String EnsureStart(this String? value, String start)
    {
        if (start.IsNullOrEmpty()) return value + String.Empty;
        if (value.IsNullOrEmpty()) return start + String.Empty;
        if (value.StartsWith(start, StringComparison.OrdinalIgnoreCase)) return value;

        return start + value;
    }

    /// <summary>确保字符串以后缀结束</summary>
    /// <param name="value">字符串</param>
    /// <param name="end">后缀</param>
    /// <returns>处理后的字符串</returns>
    public static String EnsureEnd(this String? value, String end)
    {
        if (end.IsNullOrEmpty()) return value + String.Empty;
        if (value.IsNullOrEmpty()) return end + String.Empty;
        if (value.EndsWith(end, StringComparison.OrdinalIgnoreCase)) return value;

        return value + end;
    }

    /// <summary>移除任意指定前缀</summary>
    /// <param name="value">字符串</param>
    /// <param name="starts">候选前缀</param>
    /// <returns>移除后的字符串</returns>
    public static String TrimStart(this String value, params String[] starts)
    {
        if (value.IsNullOrEmpty()) return value;
        if (starts == null || starts.Length == 0 || starts[0].IsNullOrEmpty()) return value;

        for (var i = 0; i < starts.Length; i++)
        {
            var item = starts[i];
            if (!item.IsNullOrEmpty() && value.StartsWith(item, StringComparison.OrdinalIgnoreCase))
            {
                value = value[item.Length..];
                if (String.IsNullOrEmpty(value)) break;
                i = -1;
            }
        }

        return value;
    }

    /// <summary>移除任意指定后缀</summary>
    /// <param name="value">字符串</param>
    /// <param name="ends">候选后缀</param>
    /// <returns>移除后的字符串</returns>
    public static String TrimEnd(this String value, params String[] ends)
    {
        if (value.IsNullOrEmpty()) return value;
        if (ends == null || ends.Length == 0 || ends[0].IsNullOrEmpty()) return value;

        for (var i = 0; i < ends.Length; i++)
        {
            var item = ends[i];
            if (!item.IsNullOrEmpty() && value.EndsWith(item, StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^item.Length];
                if (String.IsNullOrEmpty(value)) break;
                i = -1;
            }
        }

        return value;
    }

    /// <summary>去掉首尾不可见字符</summary>
    /// <param name="value">字符串</param>
    /// <returns>处理后的字符串</returns>
    public static String? TrimInvisible(this String? value)
    {
        if (value.IsNullOrEmpty()) return value;
        if (!value.Any(static character => character <= 31 || character == 127)) return value;

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character > 31 && character != 127) builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>截取两段标记之间的子串</summary>
    /// <param name="value">字符串</param>
    /// <param name="after">起始标记</param>
    /// <param name="before">结束标记</param>
    /// <param name="startIndex">查找起点</param>
    /// <param name="positions">返回命中的起止位置</param>
    /// <returns>子串</returns>
    public static String Substring(this String value, String? after, String? before = null, Int32 startIndex = 0, Int32[]? positions = null)
    {
        if (String.IsNullOrEmpty(value)) return value;

        var begin = startIndex;
        if (!after.IsNullOrEmpty())
        {
            begin = value.IndexOf(after, startIndex, StringComparison.Ordinal);
            if (begin < 0) return String.Empty;
            begin += after.Length;
        }

        var end = value.Length;
        if (!before.IsNullOrEmpty())
        {
            end = value.IndexOf(before, begin, StringComparison.Ordinal);
            if (end < 0) return String.Empty;
        }

        if (positions != null && positions.Length >= 2)
        {
            positions[0] = begin;
            positions[1] = end;
        }

        return end > begin ? value[begin..end] : String.Empty;
    }

    /// <summary>按最大长度截断字符串</summary>
    /// <param name="value">字符串</param>
    /// <param name="maxLength">最大长度</param>
    /// <param name="pad">追加占位符</param>
    /// <returns>截断后的字符串</returns>
    public static String Cut(this String value, Int32 maxLength, String? pad = null)
    {
        if (value.IsNullOrEmpty() || maxLength <= 0 || value.Length < maxLength) return value;
        var length = maxLength;
        if (!pad.IsNullOrEmpty()) length -= pad.Length;
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(maxLength));

        return value[..length] + pad;
    }

    /// <summary>移除任意指定前缀，未命中则返回原值</summary>
    /// <param name="value">字符串</param>
    /// <param name="starts">候选前缀</param>
    /// <returns>处理后的字符串</returns>
    public static String CutStart(this String value, params String[] starts)
    {
        if (value.IsNullOrEmpty()) return value;
        if (starts == null || starts.Length == 0 || starts[0].IsNullOrEmpty()) return value;
        for (var i = 0; i < starts.Length; i++)
        {
            var position = value.IndexOf(starts[i], StringComparison.Ordinal);
            if (position >= 0)
            {
                value = value[(position + starts[i].Length)..];
                if (value.IsNullOrEmpty()) break;
            }
        }

        return value;
    }

    /// <summary>移除任意指定后缀，未命中则返回原值</summary>
    /// <param name="value">字符串</param>
    /// <param name="ends">候选后缀</param>
    /// <returns>处理后的字符串</returns>
    public static String CutEnd(this String value, params String[] ends)
    {
        if (value.IsNullOrEmpty()) return value;
        if (ends == null || ends.Length == 0 || ends[0].IsNullOrEmpty()) return value;
        for (var i = 0; i < ends.Length; i++)
        {
            var position = value.LastIndexOf(ends[i], StringComparison.Ordinal);
            if (position >= 0)
            {
                value = value[..position];
                if (value.IsNullOrEmpty()) break;
            }
        }

        return value;
    }

    private static readonly Char[] _separator = [' ', '　'];
    private static readonly Char[] _separator2 = [' ', '\u3000'];

    /// <summary>编辑距离搜索，从词组中找到最接近关键字的若干匹配项</summary>
    /// <param name="key">关键字</param>
    /// <param name="words">词组</param>
    /// <returns>候选集</returns>
    public static String[] LevenshteinSearch(String key, String[] words)
    {
        if (IsNullOrWhiteSpace(key)) return [];
        if (words == null || words.Length == 0) return [];

        var keys = key.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in keys)
        {
            var maxDistance = (item.Length - 1) / 2;
            var query = from text in words
                        where item.Length <= text.Length
                            && Enumerable.Range(0, maxDistance + 1).Any(distance =>
                                Enumerable.Range(0, Math.Max(text.Length - item.Length - distance + 1, 0))
                                    .Any(position => LevenshteinDistance(item, text.Substring(position, item.Length + distance)) <= maxDistance))
                        orderby text
                        select text;
            words = [.. query];
        }

        return words;
    }

    /// <summary>Levenshtein 编辑距离</summary>
    /// <param name="str1">字符串1</param>
    /// <param name="str2">字符串2</param>
    /// <returns>编辑距离</returns>
    public static Int32 LevenshteinDistance(String str1, String str2)
    {
        var n = str1.Length;
        var m = str2.Length;
        var cache = new Int32[n + 1, m + 1];
        for (var i = 0; i <= n; i++) cache[i, 0] = i;
        for (var i = 1; i <= m; i++) cache[0, i] = i;

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < m; j++)
            {
                var x = cache[i, j + 1] + 1;
                var y = cache[i + 1, j] + 1;
                var z = cache[i, j] + (str1[i] == str2[j] ? 0 : 1);
                cache[i + 1, j + 1] = Math.Min(Math.Min(x, y), z);
            }
        }

        return cache[n, m];
    }

    /// <summary>最长公共子序列搜索</summary>
    /// <param name="key">关键字</param>
    /// <param name="words">候选词</param>
    /// <returns>匹配集合</returns>
    public static String[] LCSSearch(String key, String[] words)
    {
        if (IsNullOrWhiteSpace(key) || words == null || words.Length == 0) return [];

        var keys = key.Split(_separator2, StringSplitOptions.RemoveEmptyEntries).OrderBy(text => text.Length).ToArray();
        var query = from word in words
                    let distance = LCSDistance(word, keys)
                    where distance >= 0
                    orderby (distance + 0.5) / word.Length, word
                    select word;

        return [.. query];
    }

    /// <summary>计算多个关键字到指定单词的加权 LCS 距离</summary>
    /// <param name="word">被匹配单词</param>
    /// <param name="keys">多个关键字</param>
    /// <returns>距离，-1 表示排除</returns>
    public static Int32 LCSDistance(String word, String[] keys)
    {
        var sourceLength = word.Length;
        var result = sourceLength;
        var flags = new Boolean[sourceLength];
        var cache = new Int32[sourceLength + 1, keys[^1].Length + 1];
        foreach (var key in keys)
        {
            var keyLength = key.Length;
            Int32 first = 0, last = 0;
            Int32 i = 0, j = 0, lcsLength;
            for (i = 0; i < sourceLength; i++)
            {
                for (j = 0; j < keyLength; j++)
                {
                    if (word[i] == key[j])
                    {
                        cache[i + 1, j + 1] = cache[i, j] + 1;
                        if (first < cache[i, j])
                        {
                            last = i;
                            first = cache[i, j];
                        }
                    }
                    else
                        cache[i + 1, j + 1] = Math.Max(cache[i, j + 1], cache[i + 1, j]);
                }
            }

            lcsLength = cache[i, j];
            if (lcsLength <= keyLength >> 1) return -1;

            while (i > 0 && j > 0)
            {
                if (cache[i - 1, j - 1] + 1 == cache[i, j])
                {
                    i--;
                    j--;
                    if (!flags[i])
                    {
                        flags[i] = true;
                        result--;
                    }

                    first = i;
                }
                else if (cache[i - 1, j] == cache[i, j])
                    i--;
                else
                    j--;
            }

            if (lcsLength <= (last - first + 1) >> 1) return -1;
        }

        return result;
    }

    /// <summary>根据列表项成员计算距离</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="list">列表</param>
    /// <param name="keys">关键字</param>
    /// <param name="keySelector">选择器</param>
    /// <returns>距离结果</returns>
    public static IEnumerable<KeyValuePair<T, Double>> LCS<T>(this IEnumerable<T> list, String keys, Func<T, String> keySelector)
    {
        var result = new List<KeyValuePair<T, Double>>();

        if (list == null || !list.Any()) return result;
        if (keys.IsNullOrWhiteSpace()) return result;
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

        var keyList = keys.Split(' ').OrderBy(item => item.Length).ToArray();
        foreach (var item in list)
        {
            var name = keySelector(item);
            if (name.IsNullOrEmpty()) continue;

            var distance = LCSDistance(name, keyList);
            if (distance >= 0)
            {
                var value = (Double)distance / name.Length;
                result.Add(new KeyValuePair<T, Double>(item, value));
            }
        }

        return result;
    }

    /// <summary>在列表项中进行 LCS 模糊搜索</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="list">列表</param>
    /// <param name="keys">关键字</param>
    /// <param name="keySelector">选择器</param>
    /// <param name="count">数量</param>
    /// <returns>命中结果</returns>
    public static IEnumerable<T> LCSSearch<T>(this IEnumerable<T> list, String keys, Func<T, String> keySelector, Int32 count = -1)
    {
        var result = LCS(list, keys, keySelector);
        result = count >= 0 ? result.OrderBy(item => item.Value).Take(count) : result.OrderBy(item => item.Value);
        return result.Select(item => item.Key);
    }

    /// <summary>模糊匹配</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="list">列表</param>
    /// <param name="keys">关键字</param>
    /// <param name="keySelector">选择器</param>
    /// <returns>权重结果</returns>
    public static IList<KeyValuePair<T, Double>> Match<T>(this IEnumerable<T> list, String keys, Func<T, String> keySelector)
    {
        var result = new List<KeyValuePair<T, Double>>();

        if (list == null || !list.Any()) return result;
        if (keys.IsNullOrWhiteSpace()) return result;
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

        var keyList = keys.Split(' ').OrderBy(item => item.Length).ToArray();
        foreach (var item in list)
        {
            var name = keySelector(item);
            if (name.IsNullOrEmpty()) continue;

            var distance = keyList.Sum(key =>
            {
                var pair = Match(name, key, key.Length);
                return pair.Key - pair.Value * 0.1;
            });
            if (distance > 0)
            {
                var value = distance / keys.Length;
                result.Add(new KeyValuePair<T, Double>(item, value));
            }
        }

        return result;
    }

    /// <summary>字符串模糊匹配</summary>
    /// <param name="value">目标字符串</param>
    /// <param name="key">关键字</param>
    /// <param name="maxError">最大误差</param>
    /// <returns>命中数和跳过数</returns>
    public static KeyValuePair<Int32, Int32> Match(String value, String key, Int32 maxError = 0)
    {
        var matchIndex = 0;
        var keyIndex = 0;
        var match = 0;
        var skip = 0;

        while (skip <= maxError && keyIndex < key.Length)
        {
            for (var i = matchIndex; i < value.Length; i++)
            {
                if (value[i] == key[keyIndex])
                {
                    keyIndex++;
                    matchIndex = i + 1;
                    match++;
                    if (keyIndex == key.Length) break;
                }
            }

            if (keyIndex == key.Length) break;

            keyIndex++;
            skip++;
        }

        return new KeyValuePair<Int32, Int32>(match, skip);
    }

    /// <summary>模糊匹配并返回命中的元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="list">列表</param>
    /// <param name="keys">关键字</param>
    /// <param name="keySelector">选择器</param>
    /// <param name="count">数量</param>
    /// <param name="confidence">置信度阈值</param>
    /// <returns>命中结果</returns>
    public static IEnumerable<T> Match<T>(this IEnumerable<T> list, String keys, Func<T, String> keySelector, Int32 count, Double confidence = 0.5)
    {
        var result = Match(list, keys, keySelector).Where(item => item.Value >= confidence);
        result = count >= 0 ? result.OrderByDescending(item => item.Value).Take(count) : result.OrderByDescending(item => item.Value);
        return result.Select(item => item.Key);
    }
}