using System.Text;
using System.Text.RegularExpressions;

namespace Pek;

/// <summary>字符串扩展方法</summary>
public static partial class ExtString
{
    /// <summary>去除HTML标签</summary>
    /// <param name="htmlString">含HTML的字符串</param>
    public static String NoHTML(this String htmlString)
    {
        htmlString = MyRegex.ScriptRegex().Replace(htmlString, "");
        htmlString = MyRegex.NoscriptRegex().Replace(htmlString, "");
        htmlString = MyRegex.StyleRegex().Replace(htmlString, "");
        htmlString = MyRegex.HtmlTagRegex().Replace(htmlString, "");
        htmlString = MyRegex.HtmlTag2Regex().Replace(htmlString, " ");
        htmlString = MyRegex.NewlineSpaceRegex().Replace(htmlString, " ");
        htmlString = MyRegex.CommentEndRegex().Replace(htmlString, " ");
        htmlString = MyRegex.CommentStartRegex().Replace(htmlString, " ");
        htmlString = MyRegex.QuotRegex().Replace(htmlString, "\"");
        htmlString = MyRegex.AmpRegex().Replace(htmlString, "&");
        htmlString = MyRegex.LtRegex().Replace(htmlString, "<");
        htmlString = MyRegex.GtRegex().Replace(htmlString, ">");
        htmlString = MyRegex.NbspRegex().Replace(htmlString, "");
        htmlString = MyRegex.IexclRegex().Replace(htmlString, "\xa1");
        htmlString = MyRegex.CentRegex().Replace(htmlString, "\xa2");
        htmlString = MyRegex.PoundRegex().Replace(htmlString, "\xa3");
        htmlString = MyRegex.CopyRegex().Replace(htmlString, "\xa9");
        htmlString = MyRegex.DecRegex().Replace(htmlString, " ");
        return htmlString;
    }

    /// <summary>字符串转字节数组（UTF-8）</summary>
    /// <param name="value">字符串</param>
    public static Byte[] ToByte(this String value) => Encoding.UTF8.GetBytes(value);

    /// <summary>URL编码</summary>
    /// <param name="value">字符串</param>
    public static String UrlEncode(this String value)
    {
        var sb = new StringBuilder();
        var byStr = Encoding.UTF8.GetBytes(value);
        for (var i = 0; i < byStr.Length; i++)
        {
            sb.Append(@"%" + Convert.ToString(byStr[i], 16));
        }
        return sb.ToString();
    }

    /// <summary>转换为Unicode表示</summary>
    /// <param name="value">字符串</param>
    public static String ToUnicode(this String value)
    {
        if (String.IsNullOrEmpty(value)) return value;
        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            builder.Append("\\u" + ((Int32)value[i]).ToString("x"));
        }
        return builder.ToString();
    }

    private static readonly Regex EmailExpression = MyRegex.EmailRegex();
    private static readonly Regex WebUrlExpression = MyRegex.WebUrlRegex();
    private static readonly Regex StripHtmlExpression = MyRegex.StripHtmlRegex();
    private static readonly Char[] Separator = ['/', '\\'];

    /// <summary>格式化字符串</summary>
    /// <param name="instance">格式模板</param>
    /// <param name="args">参数</param>
    public static String FormatWith(this String instance, params Object[] args) => String.Format(instance, args);

    /// <summary>字符串转枚举</summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="instance">字符串</param>
    /// <param name="defaultValue">默认值</param>
    public static T ToEnum<T>(this String instance, T defaultValue) where T : struct, IComparable, IFormattable
    {
        var convertedValue = defaultValue;

        if (!String.IsNullOrWhiteSpace(instance) && !Enum.TryParse(instance.Trim(), true, out convertedValue))
        {
            convertedValue = defaultValue;
        }

        return convertedValue;
    }

    /// <summary>整数转枚举</summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <param name="instance">整数值</param>
    /// <param name="defaultValue">默认值</param>
    public static T ToEnum<T>(this Int32 instance, T defaultValue) where T : struct, IComparable, IFormattable
    {
        if (!Enum.TryParse(instance.ToString(), true, out T convertedValue))
        {
            convertedValue = defaultValue;
        }

        return convertedValue;
    }

    /// <summary>去除HTML标签（简单版）</summary>
    /// <param name="instance">含HTML的字符串</param>
    public static String StripHtml(this String instance) => StripHtmlExpression.Replace(instance, String.Empty);

    /// <summary>是否为Email</summary>
    /// <param name="instance">字符串</param>
    public static Boolean IsEmail(this String instance) => !String.IsNullOrWhiteSpace(instance) && EmailExpression.IsMatch(instance);

    /// <summary>是否为URL</summary>
    /// <param name="instance">字符串</param>
    public static Boolean IsWebUrl(this String instance) => !String.IsNullOrWhiteSpace(instance) && WebUrlExpression.IsMatch(instance);

    /// <summary>字符串转布尔</summary>
    /// <param name="instance">字符串</param>
    public static Boolean AsBool(this String instance)
    {
        _ = Boolean.TryParse(instance, out var result);
        return result;
    }

    /// <summary>字符串转日期</summary>
    /// <param name="instance">字符串</param>
    public static DateTime AsDateTime(this String instance)
    {
        _ = DateTime.TryParse(instance, out var result);
        return result;
    }

    /// <summary>字符串转Decimal</summary>
    /// <param name="instance">字符串</param>
    public static Decimal AsDecimal(this String instance)
    {
        _ = Decimal.TryParse(instance, out var result);
        return result;
    }

    /// <summary>字符串转Int32</summary>
    /// <param name="instance">字符串</param>
    public static Int32 AsInt(this String instance)
    {
        _ = Int32.TryParse(instance, out var result);
        return result;
    }

    /// <summary>是否为整数</summary>
    /// <param name="instance">字符串</param>
    public static Boolean IsIntT(this String instance) => Int32.TryParse(instance, out _);

    /// <summary>是否为浮点数</summary>
    /// <param name="instance">字符串</param>
    public static Boolean IsFloat(this String instance) => Single.TryParse(instance, out _);

    /// <summary>首字母小写</summary>
    /// <param name="instance">字符串</param>
    public static String FirstCharToLowerCase(this String instance)
    {
        if (!String.IsNullOrWhiteSpace(instance) && instance.Length > 2 && Char.IsUpper(instance[0]))
        {
            return Char.ToLower(instance[0]) + instance[1..];
        }
        if (instance.Length == 2)
        {
            return instance.ToLower();
        }
        return instance;
    }

    /// <summary>转换为文件路径</summary>
    /// <param name="path">路径字符串</param>
    public static String ToFilePath(this String path) => Path.Combine(path.Split(Separator, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>合并路径</summary>
    /// <param name="p">基础路径</param>
    /// <param name="path">子路径</param>
    public static String CombinePath(this String p, String path) => $"{p.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}{path.ToFilePath()}";
}

/// <summary>源生成正则表达式（AOT安全）</summary>
internal static partial class MyRegex
{
    [GeneratedRegex(@"<script[\s\S]*?</script>", RegexOptions.IgnoreCase)]
    internal static partial Regex ScriptRegex();

    [GeneratedRegex(@"<noscript[\s\S]*?</noscript>", RegexOptions.IgnoreCase)]
    internal static partial Regex NoscriptRegex();

    [GeneratedRegex(@"<style[\s\S]*?</style>", RegexOptions.IgnoreCase)]
    internal static partial Regex StyleRegex();

    [GeneratedRegex(@"<.*?>", RegexOptions.IgnoreCase)]
    internal static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<(.[^>]*)>", RegexOptions.IgnoreCase)]
    internal static partial Regex HtmlTag2Regex();

    [GeneratedRegex(@"([\r\n])[\s]+", RegexOptions.IgnoreCase)]
    internal static partial Regex NewlineSpaceRegex();

    [GeneratedRegex(@"-->", RegexOptions.IgnoreCase)]
    internal static partial Regex CommentEndRegex();

    [GeneratedRegex(@"<!--.*", RegexOptions.IgnoreCase)]
    internal static partial Regex CommentStartRegex();

    [GeneratedRegex(@"&(quot|#34);", RegexOptions.IgnoreCase)]
    internal static partial Regex QuotRegex();

    [GeneratedRegex(@"&(amp|#38);", RegexOptions.IgnoreCase)]
    internal static partial Regex AmpRegex();

    [GeneratedRegex(@"&(lt|#60);", RegexOptions.IgnoreCase)]
    internal static partial Regex LtRegex();

    [GeneratedRegex(@"&(gt|#62);", RegexOptions.IgnoreCase)]
    internal static partial Regex GtRegex();

    [GeneratedRegex(@"&(nbsp|#160);", RegexOptions.IgnoreCase)]
    internal static partial Regex NbspRegex();

    [GeneratedRegex(@"&(iexcl|#161);", RegexOptions.IgnoreCase)]
    internal static partial Regex IexclRegex();

    [GeneratedRegex(@"&(cent|#162);", RegexOptions.IgnoreCase)]
    internal static partial Regex CentRegex();

    [GeneratedRegex(@"&(pound|#163);", RegexOptions.IgnoreCase)]
    internal static partial Regex PoundRegex();

    [GeneratedRegex(@"&(copy|#169);", RegexOptions.IgnoreCase)]
    internal static partial Regex CopyRegex();

    [GeneratedRegex(@"&#(\d+);", RegexOptions.IgnoreCase)]
    internal static partial Regex DecRegex();

    [GeneratedRegex(@"^([0-9a-zA-Z]+[-._+&])*[0-9a-zA-Z]+@([-0-9a-zA-Z]+[.])+[a-zA-Z]{2,6}$", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    internal static partial Regex EmailRegex();

    [GeneratedRegex(@"(http|https)://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    internal static partial Regex WebUrlRegex();

    [GeneratedRegex(@"<\S[^><]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    internal static partial Regex StripHtmlRegex();
}
