using System.Text.RegularExpressions;

using Pek;
using Pek.Collections;
using Pek.Extension;

namespace Pek.Web;

/// <summary>超链接</summary>
public class Link
{
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>全名</summary>
    public String? FullName { get; set; }

    /// <summary>超链接</summary>
    public String? Url { get; set; }

    /// <summary>原始超链接</summary>
    public String? RawUrl { get; set; }

    /// <summary>标题</summary>
    public String? Title { get; set; }

    /// <summary>版本</summary>
    public Version? Version { get; set; }

    /// <summary>时间</summary>
    public DateTime Time { get; set; }

    /// <summary>哈希</summary>
    public String? Hash { get; set; }

    /// <summary>原始Html</summary>
    public String? Html { get; set; }

    private static readonly Regex _regA = new("""<a[^>]* href=?\"(?<链接>[^>\"]*)?\"[^>]*>(?<名称>[^<]*)</a>\s*</td>[^>]*<td[^>]*>(?<哈希>[^<]*)</td>""", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex _regB = new("""<td>(?<时间>[^<]*)</td>\s*<td>(?<大小>[^<]*)</td>\s*<td>\s*<a[^>]* href=\"?(?<链接>[^>\"]*)\"?[^>]*>(?<名称>[^<]*)</a>\s*</td>[^>]*<td[^>]*>(?<哈希>[^<]*)</td>""", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>分析HTML中的链接</summary>
    /// <param name="html">Html文本</param>
    /// <param name="baseUrl">基础Url</param>
    /// <param name="filter">过滤器</param>
    /// <returns>链接数组</returns>
    public static Link[] Parse(String html, String? baseUrl = null, Func<Link, Boolean>? filter = null)
    {
        if (baseUrl != null && !baseUrl.EndsWith("/")) baseUrl += "/";
        if (baseUrl != null && baseUrl.StartsWithIgnoreCase("ftp://")) return ParseFtp(html, baseUrl, filter);

        var list = new List<Link>();
        var baseUri = baseUrl.IsNullOrEmpty() ? null : new Uri(baseUrl);
        var matches = _regB.Matches(html);
        if (matches.Count == 0) matches = _regA.Matches(html);
        foreach (Match match in matches)
        {
            var link = new Link
            {
                Html = match.Value,
                FullName = match.Groups["名称"].Value.Trim(),
                Url = match.Groups["链接"].Value.Trim(),
                Hash = match.Groups["哈希"].Value.Trim(),
                Time = match.Groups["时间"].Value.Trim().ToDateTime(),
            };
            if (link.Hash.Contains("&lt;")) link.Hash = null;
            link.RawUrl = link.Url;
            link.Name = link.FullName;

            if (filter != null && !filter(link)) continue;

            link.Url = link.Url.TrimStart('#');
            if (String.IsNullOrEmpty(link.Url)) continue;
            if (link.Url.StartsWithIgnoreCase("javascript:")) continue;

            if (baseUri != null)
                link.Url = new Uri(baseUri, link.RawUrl).ToString();
            else
                link.Url = link.RawUrl;

            if (link.Url.Contains("github.com") && link.Url.Contains("/blob/")) link.Url = link.Url.Replace("/blob/", "/raw/");

            link.ParseTime();
            link.ParseVersion();

            var name = link.Name;
            if (name.EndsWithIgnoreCase(".tar.gz"))
                link.Name = name[..^7];
            else
            {
                var position = name.LastIndexOf('.');
                if (position > 0) link.Name = name[..position];
            }

            list.Add(link);
        }

        return [.. list];
    }

    private static Link[] ParseFtp(String html, String? baseUrl, Func<Link, Boolean>? filter = null)
    {
        var list = new List<Link>();
        var lines = html.Split("\r\n", "\r", "\n");
        if (lines.Length == 0) return [.. list];

        var baseUri = baseUrl.IsNullOrEmpty() ? null : new Uri(baseUrl);
        foreach (var item in lines)
        {
            var link = new Link
            {
                FullName = item,
                Name = item,
            };

            if (filter != null && !filter(link)) continue;

            link.Title = Path.GetFileNameWithoutExtension(item);
            link.Url = baseUri != null ? new Uri(baseUri, item).ToString() : item;

            var timeIndex = link.ParseTime();
            if (timeIndex > 0 && link.Title != null) link.Title = link.Title[..timeIndex];

            var versionIndex = link.ParseVersion();
            if (versionIndex > 0 && link.Title != null) link.Title = link.Title[..versionIndex];

            var name = link.Name;
            if (name.EndsWithIgnoreCase(".tar.gz"))
                link.Name = name[..^7];
            else
            {
                var position = name.LastIndexOf('.');
                if (position > 0) link.Name = name[..position];
            }

            list.Add(link);
        }

        return [.. list];
    }

    /// <summary>分解文件</summary>
    /// <param name="file">文件路径</param>
    /// <returns>当前链接</returns>
    public Link Parse(String file)
    {
        RawUrl = file;
        Url = file.GetFullPath();
        FullName = Path.GetFileName(file);
        Name = FullName;

        ParseTime();
        ParseVersion();

        var name = Name;
        if (name.EndsWithIgnoreCase(".tar.gz"))
            Name = name[..^7];
        else
        {
            var position = name.LastIndexOf('.');
            if (position > 0) Name = name[..position];
        }

        if (Time.Year < 2000)
        {
            var fileInfo = file.AsFile();
            if (fileInfo.Exists) Time = fileInfo.LastWriteTime;
        }

        return this;
    }

    /// <summary>从名称分解时间</summary>
    /// <returns>位置</returns>
    public Int32 ParseTime()
    {
        var name = Name;
        if (name.IsNullOrEmpty()) return -1;

        var position = name.LastIndexOf("_");
        if (position <= 0) return -1;

        var text = name[(position + 1)..];
        if (text.StartsWith("20") && text.Length >= 14)
        {
            Time = new DateTime(text[..4].ToInt(), text.Substring(4, 2).ToInt(), text.Substring(6, 2).ToInt(), text.Substring(8, 2).ToInt(), text.Substring(10, 2).ToInt(), text.Substring(12, 2).ToInt());
            Name = name[..position] + name[(position + 15)..];
        }
        else if (text.StartsWith("20") && text.Length >= 8)
        {
            Time = new DateTime(text[..4].ToInt(), text.Substring(4, 2).ToInt(), text.Substring(6, 2).ToInt());
            Name = name[..position] + name[(position + 9)..];
        }

        return position;
    }

    /// <summary>从名称分解版本</summary>
    /// <returns>位置</returns>
    public Int32 ParseVersion()
    {
        var name = Name;
        if (name.IsNullOrEmpty()) return -1;

        var position = IndexOfAny(name, ["_v", "_V", ".v", ".V", " v", " V"], 0);
        if (position <= 0) return -1;

        var next = name.IndexOfAny([' ', '_', '-'], position + 2);
        if (next < 0)
        {
            next = name.LastIndexOf('.');
            if (next <= position) next = -1;
        }
        if (next < 0) next = name.Length;

        var text = name.Substring(position + 2, next - position - 2);
        var numbers = text.SplitAsInt(".");
        if (numbers.Length > 0)
        {
            Version = numbers.Length switch
            {
                1 => new Version(numbers[0], 0),
                2 => new Version(numbers[0], numbers[1]),
                3 => new Version(numbers[0], numbers[1], numbers[2]),
                4 => new Version(numbers[0], numbers[1], numbers[2], numbers[3]),
                _ => null,
            };

            var value = name[..position];
            if (next < name.Length) value += name[next..];
            Name = value;
        }

        return position;
    }

    private static Int32 IndexOfAny(String value, String[] anyOf, Int32 startIndex)
    {
        foreach (var item in anyOf)
        {
            var position = value.IndexOf(item, startIndex, StringComparison.Ordinal);
            if (position >= 0) return position;
        }

        return -1;
    }

    /// <summary>已重载</summary>
    /// <returns>字符串</returns>
    public override String ToString()
    {
        var builder = Pool.StringBuilder.Get();
        builder.AppendFormat("{0} {1}", Name, RawUrl);
        if (Version != null) builder.AppendFormat(" v{0}", Version);
        if (Time > DateTime.MinValue) builder.AppendFormat(" {0}", Time.ToFullString());
        return builder.Return(true);
    }
}