using System.Net.Http;
using System.Reflection;
using System.Text;

using Pek.Extension;

namespace Pek.Http;

/// <summary>Http帮助类</summary>
public static class HttpHelper
{
    /// <summary>默认用户浏览器UserAgent</summary>
    public static String? DefaultUserAgent { get; set; }

    static HttpHelper()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        if (assembly != null)
        {
            var name = assembly.GetName();
            var os = Environment.OSVersion?.ToString();
            if (!os.IsNullOrEmpty() && os.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)) os = os[10..];
            if (!os.IsNullOrEmpty() && Encoding.UTF8.GetByteCount(os) == os.Length)
                DefaultUserAgent = $"{name.Name}/{name.Version} ({os})";
            else
                DefaultUserAgent = $"{name.Name}/{name.Version}";
        }
    }

    /// <summary>设置浏览器UserAgent</summary>
    /// <param name="client">客户端</param>
    /// <returns>客户端</returns>
    public static HttpClient SetUserAgent(this HttpClient client)
    {
        var userAgent = DefaultUserAgent;
        if (!userAgent.IsNullOrEmpty()) client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }
}