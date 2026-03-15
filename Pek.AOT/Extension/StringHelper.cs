using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

using Pek.Collections;

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
}