using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Pek.Extension;

namespace Pek;

/// <summary>字符串扩展。AOT 安全版</summary>
public static partial class DHStringExtensions
{
    /// <summary>如果给定字符串的结尾不以字符结尾，则将字符添加到该字符串的结尾</summary>
    public static String EnsureEndsWith(this String str, Char c, StringComparison comparisonType = StringComparison.Ordinal)
    {
        if (str.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(str));
        if (str.EndsWith(c.ToString(), comparisonType)) return str;
        return str + c;
    }

    /// <summary>如果给定字符串的开头不以字符开头，则将字符添加到该字符串的开头</summary>
    public static String EnsureStartsWith(this String str, Char c, StringComparison comparisonType = StringComparison.Ordinal)
    {
        if (str.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(str));
        if (str.StartsWith(c.ToString(), comparisonType)) return str;
        return c + str;
    }

    /// <summary>将字符串中的行尾转换为 Environment.NewLine</summary>
    public static String NormalizeLineEndings(this String str) => str.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);

    /// <summary>获取字符串中第 n 个字符的索引</summary>
    public static Int32 NthIndexOf(this String str, Char c, Int32 n)
    {
        if (str.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(str));
        var count = 0;
        for (var i = 0; i < str.Length; i++)
        {
            if (str[i] == c && ++count == n) return i;
        }
        return -1;
    }

    /// <summary>移除字符串末尾的指定后缀</summary>
    public static String RemovePostFix(this String str, params String[] postFixes) => RemovePostFix(str, StringComparison.Ordinal, postFixes);

    /// <summary>移除字符串末尾的指定后缀</summary>
    public static String RemovePostFix(this String str, StringComparison comparisonType, params String[] postFixes)
    {
        if (str.IsNullOrEmpty()) return String.Empty;
        if (postFixes == null || postFixes.Length == 0) return str;
        foreach (var postFix in postFixes)
        {
            if (str.EndsWith(postFix, comparisonType)) return str[..^postFix.Length];
        }
        return str;
    }

    /// <summary>移除字符串开头的指定前缀</summary>
    public static String RemovePreFix(this String str, params String[] preFixes) => RemovePreFix(str, StringComparison.Ordinal, preFixes);

    /// <summary>移除字符串开头的指定前缀</summary>
    public static String RemovePreFix(this String str, StringComparison comparisonType, params String[] preFixes)
    {
        if (str.IsNullOrEmpty()) return String.Empty;
        if (preFixes == null || preFixes.Length == 0) return str;
        foreach (var preFix in preFixes)
        {
            if (str.StartsWith(preFix, comparisonType)) return str[preFix.Length..];
        }
        return str;
    }

    /// <summary>替换字符串中第一个匹配项</summary>
    public static String ReplaceFirst(this String str, String search, String replace, StringComparison comparisonType = StringComparison.Ordinal)
    {
        if (str.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(str));
        var pos = str.IndexOf(search, comparisonType);
        if (pos < 0) return str;
        return str[..pos] + replace + str[(pos + search.Length)..];
    }

    /// <summary>拆分字符串</summary>
    public static String[] Split(this String str, String separator) => str.Split(new[] { separator }, StringSplitOptions.None);

    /// <summary>拆分字符串</summary>
    public static String[] Split(this String str, String separator, StringSplitOptions options) => str.Split(new[] { separator }, options);

    /// <summary>按行拆分字符串</summary>
    public static String[] SplitToLines(this String str) => str.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

    /// <summary>按行拆分字符串</summary>
    public static String[] SplitToLines(this String str, StringSplitOptions options) => str.Split(["\r\n", "\r", "\n"], options);

    /// <summary>将字符串转换为驼峰命名</summary>
    public static String ToCamelCase(this String str, Boolean useCurrentCulture = false, Boolean handleAbbreviations = false)
    {
        if (String.IsNullOrWhiteSpace(str)) return str;
        if (str.Length == 1) return useCurrentCulture ? str.ToLower() : str.ToLowerInvariant();
        return (useCurrentCulture ? Char.ToLower(str[0]) : Char.ToLowerInvariant(str[0])) + str[1..];
    }

    /// <summary>将字符串转换为句子格式（首字母大写）</summary>
    public static String ToSentenceCase(this String str, Boolean useCurrentCulture = false)
    {
        if (String.IsNullOrWhiteSpace(str)) return str;
        return (useCurrentCulture ? Char.ToUpper(str[0]) : Char.ToUpperInvariant(str[0])) + str[1..];
    }

    /// <summary>将字符串转换为 Kebab-Case 命名</summary>
    public static String ToKebabCase(this String str, Boolean useCurrentCulture = false)
    {
        if (String.IsNullOrWhiteSpace(str)) return str;
        var sb = new StringBuilder();
        for (var i = 0; i < str.Length; i++)
        {
            if (Char.IsUpper(str[i]))
            {
                if (i > 0) sb.Append('-');
                sb.Append(useCurrentCulture ? Char.ToLower(str[i]) : Char.ToLowerInvariant(str[i]));
            }
            else
            {
                sb.Append(str[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>将字符串转换为蛇形命名</summary>
    public static String ToSnakeCase(this String str)
    {
        if (String.IsNullOrWhiteSpace(str)) return str;
        var sb = new StringBuilder();
        for (var i = 0; i < str.Length; i++)
        {
            if (Char.IsUpper(str[i]))
            {
                if (i > 0) sb.Append('_');
                sb.Append(Char.ToLowerInvariant(str[i]));
            }
            else
            {
                sb.Append(str[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>将字符串转换为枚举值</summary>
    public static T ToEnum<T>(this String value) where T : struct => (T)Enum.Parse(typeof(T), value);

    /// <summary>将字符串转换为枚举值</summary>
    public static T ToEnum<T>(this String value, Boolean ignoreCase) where T : struct => (T)Enum.Parse(typeof(T), value, ignoreCase);

    /// <summary>计算字符串的 MD5 哈希值</summary>
    public static String ToMd5(this String str)
    {
        if (String.IsNullOrEmpty(str)) return String.Empty;
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(str));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    /// <summary>将字符串转换为 PascalCase 命名（首字母大写）</summary>
    public static String ToPascalCase(this String str, Boolean useCurrentCulture = false)
    {
        if (String.IsNullOrWhiteSpace(str)) return str;
        return (useCurrentCulture ? Char.ToUpper(str[0]) : Char.ToUpperInvariant(str[0])) + str[1..];
    }

    /// <summary>截断字符串</summary>
    public static String Truncate(this String str, Int32 maxLength) => str.TruncateWithPostfix(maxLength, String.Empty);

    /// <summary>从开头截断字符串</summary>
    public static String TruncateFromBeginning(this String str, Int32 maxLength)
    {
        if (str.IsNullOrEmpty()) return str ?? String.Empty;
        if (str.Length <= maxLength) return str;
        return str[^maxLength..];
    }

    /// <summary>截断字符串并添加后缀</summary>
    public static String TruncateWithPostfix(this String str, Int32 maxLength) => str.TruncateWithPostfix(maxLength, "...");

    /// <summary>截断字符串并添加后缀</summary>
    public static String TruncateWithPostfix(this String str, Int32 maxLength, String postfix)
    {
        if (str.IsNullOrEmpty()) return str ?? String.Empty;
        if (str.Length <= maxLength) return str;
        return str[..(maxLength - postfix.Length)] + postfix;
    }

    /// <summary>获取字符串的 UTF-8 字节数组</summary>
    public static Byte[] GetBytes(this String str) => str.GetBytes(Encoding.UTF8);

    /// <summary>获取字符串的指定编码字节数组</summary>
    public static Byte[] GetBytes([NotNull] this String str, [NotNull] Encoding encoding) => encoding.GetBytes(str);
}
