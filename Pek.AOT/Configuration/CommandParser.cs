using Pek.Extension;

namespace Pek.Configuration;

/// <summary>命令分析器</summary>
public class CommandParser
{
    /// <summary>不区分大小写</summary>
    public Boolean IgnoreCase { get; set; }

    /// <summary>去除前导横杠。默认 true</summary>
    public Boolean TrimStart { get; set; } = true;

    /// <summary>分析参数数组，得到名值字段</summary>
    /// <param name="args">参数数组</param>
    /// <returns>名值字典</returns>
    public IDictionary<String, String?> Parse(String[] args)
    {
        args ??= Environment.GetCommandLineArgs();

        var dic = IgnoreCase ?
            new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase) :
            [];
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];

            if (key[0] == '-')
            {
                var p = key.IndexOf('=');
                if (p > 0)
                {
                    var value = key[(p + 1)..];
                    key = key[..p];
                    if (TrimStart) key = key.TrimStart('-');
                    dic[key] = TrimQuote(value);
                }
                else
                {
                    if (TrimStart) key = key.TrimStart('-');
                    var value = i + 1 < args.Length && args[i + 1][0] != '-' ? args[++i] : null;
                    dic[key] = TrimQuote(value);
                }
            }
            else
            {
                if (TrimStart) key = key.TrimStart('-');
                var value = i + 1 < args.Length && args[i + 1][0] != '-' ? args[++i] : null;
                dic[key] = TrimQuote(value);
            }
        }

        return dic;
    }

    /// <summary>去除两头的引号</summary>
    /// <param name="value">原始字符串</param>
    /// <returns>去引号后的字符串</returns>
    public static String? TrimQuote(String? value)
    {
        if (value.IsNullOrEmpty()) return value;

        if (value.Length >= 2)
        {
            if (value[0] == '"' && value[value.Length - 1] == '"') value = value[1..^1];
            if (value[0] == '\'' && value[value.Length - 1] == '\'') value = value[1..^1];
        }

        return value;
    }

    /// <summary>把字符串分割为参数数组，支持双引号</summary>
    /// <param name="value">命令行字符串</param>
    /// <returns>参数数组</returns>
    public static String[] Split(String? value)
    {
        value = value?.Trim();
        if (value.IsNullOrEmpty()) return [];

        var args = new List<String>();
        var p = 0;
        while (p < value.Length)
        {
            var p2 = value.IndexOf(' ', p);
            if (p2 < 0)
            {
                args.Add(value[p..].Trim().Trim('"'));
                break;
            }
            else if (p2 != p)
            {
                if (value[p] == '"')
                {
                    var p3 = value.IndexOf('"', p + 1);
                    if (p3 >= 0 && p3 > p2)
                    {
                        if (p3 == value.Length - 1 || value[p3 + 1] == ' ')
                        {
                            p++;
                            p2 = p3;
                        }
                    }
                }

                args.Add(value.Substring(p, p2 - p).Trim());
            }

            p = p2 + 1;
        }

        return [.. args];
    }
}