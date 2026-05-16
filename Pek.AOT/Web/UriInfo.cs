using System.Net;

using Pek.Extension;

namespace Pek.Web;

/// <summary>资源定位。无限制解析Url地址</summary>
public class UriInfo
{
    #region 属性
    /// <summary>协议</summary>
    public String? Scheme { get; set; }

    /// <summary>主机</summary>
    public String? Host { get; set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; }

    /// <summary>路径</summary>
    public String? AbsolutePath { get; set; }

    /// <summary>查询</summary>
    public String? Query { get; set; }

    /// <summary>路径与查询</summary>
    public String? PathAndQuery
    {
        get
        {
            if (Query.IsNullOrEmpty()) return AbsolutePath;
            if (Query[0] == '?') return AbsolutePath + Query;

            return $"{AbsolutePath}?{Query}";
        }
    }

    /// <summary>主机与端口。省略默认端口</summary>
    public String? Authority
    {
        get
        {
            if (Host.IsNullOrEmpty()) return Host;

            var isIPv6 = Host.Contains(':');
            var hostPart = isIPv6 ? $"[{Host}]" : Host;

            if (Port == 0) return hostPart;

            if (Scheme.EqualIgnoreCase("http", "ws"))
                return Port == 80 ? hostPart : $"{hostPart}:{Port}";
            else if (Scheme.EqualIgnoreCase("https", "wss"))
                return Port == 443 ? hostPart : $"{hostPart}:{Port}";

            return $"{hostPart}:{Port}";
        }
    }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public UriInfo() { }

    /// <summary>实例化</summary>
    /// <param name="value"></param>
    public UriInfo(String value) => Parse(value);
    #endregion

    #region 方法
    /// <summary>尝试解析Url字符串</summary>
    public static Boolean TryParse(String? value, out UriInfo? uriInfo)
    {
        uriInfo = null;
        if (value.IsNullOrWhiteSpace()) return false;

        uriInfo = new UriInfo();
        return uriInfo.Parse(value);
    }

    /// <summary>解析Url字符串</summary>
    /// <param name="value"></param>
    public Boolean Parse(String value)
    {
        if (value.IsNullOrWhiteSpace()) return false;

        var span = value.AsSpan();
        var p = 0;

        var schemeIndex = span.IndexOf("://".AsSpan());
        if (schemeIndex >= 0)
        {
            Scheme = span[..schemeIndex].ToString();
            p = schemeIndex + 3;
        }

        var slashIndex = span[p..].IndexOf('/');
        if (slashIndex >= 0)
        {
            slashIndex += p;
            ParseHost(span[p..slashIndex]);
            ParsePath(span, slashIndex);
        }
        else
        {
            var queryIndex = span[p..].IndexOf('?');
            if (queryIndex >= 0)
            {
                queryIndex += p;
                ParseHost(span[p..queryIndex]);
                Query = span[queryIndex..].ToString();
            }
            else
            {
                ParseHost(span[p..]);
            }
        }

        if (Scheme.IsNullOrEmpty() && Host.IsNullOrEmpty() && AbsolutePath.IsNullOrEmpty())
            return false;

        if (AbsolutePath.IsNullOrEmpty()) AbsolutePath = "/";

        return true;
    }

    private void ParsePath(ReadOnlySpan<Char> span, Int32 p)
    {
        var queryIndex = span[p..].IndexOf('?');
        if (queryIndex >= 0)
        {
            queryIndex += p;
            AbsolutePath = span[p..queryIndex].ToString();
            Query = span[queryIndex..].ToString();
        }
        else
        {
            AbsolutePath = span[p..].ToString();
        }
    }

    private void ParseHost(ReadOnlySpan<Char> span)
    {
        if (span.Length <= 0) return;

        if (span[0] == '[')
        {
            var closeBracketIndex = span.IndexOf(']');
            if (closeBracketIndex > 0)
            {
                Host = span[1..closeBracketIndex].ToString();

                if (closeBracketIndex + 1 < span.Length && span[closeBracketIndex + 1] == ':')
                    Port = span[(closeBracketIndex + 2)..].ToString().ToInt();
            }
            else
            {
                Host = span[1..].ToString();
            }
        }
        else
        {
            var colonIndex = span.LastIndexOf(':');
            if (colonIndex > 0)
            {
                Host = span[..colonIndex].ToString();
                Port = span[(colonIndex + 1)..].ToString().ToInt();
            }
            else
            {
                Host = span.ToString();
            }
        }
    }

    /// <summary>拼接请求参数</summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public UriInfo Append(String name, Object? value)
    {
        var str = WebUtility.UrlEncode(value + "");

        var q = Query;
        Query = q.IsNullOrEmpty() ? $"{name}={str}" : $"{q}&{name}={str}";

        return this;
    }

    /// <summary>拼接请求参数（非空）</summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public UriInfo AppendNotEmpty(String name, Object? value)
    {
        if (value == null) return this;

        var str = value + "";
        if (str.IsNullOrEmpty()) return this;
        str = WebUtility.UrlEncode(str);

        var q = Query;
        Query = q.IsNullOrEmpty() ? $"{name}={str}" : $"{q}&{name}={str}";

        return this;
    }

    /// <summary>转换为 Uri 对象</summary>
    /// <returns>Uri 对象。如果无法构造有效的 Uri，则返回 null</returns>
    public Uri? ToUri()
    {
        if (Scheme.IsNullOrEmpty()) return null;
        if (Host.IsNullOrEmpty()) return null;

        var url = ToString();
        if (url.IsNullOrEmpty()) return null;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String? ToString()
    {
        var authority = Authority;
        if (Scheme.IsNullOrEmpty())
        {
            if (authority.IsNullOrEmpty()) return PathAndQuery;

            return $"{authority}{PathAndQuery}";
        }

        return $"{Scheme}://{authority}{PathAndQuery}";
    }
    #endregion
}