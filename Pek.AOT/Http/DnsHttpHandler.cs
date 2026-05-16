using System.Net;

using Pek.Net;

namespace Pek.Http;

/// <summary>支持优化Dns解析的HttpClient处理器</summary>
/// <param name="innerHandler">下层处理器</param>
public class DnsHttpHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    /// <summary>DNS解析器</summary>
    public IDnsResolver Resolver { get; set; } = DnsResolver.Instance;

#if NET5_0_OR_GREATER
    private static readonly HttpRequestOptionsKey<Int32> _dnsIndexKey = new("dnsIndex");
#endif

    /// <summary>解析域名</summary>
    /// <param name="host">域名或主机</param>
    /// <returns>解析到的地址集合</returns>
    protected virtual IPAddress[]? Resolve(String host) => Resolver?.Resolve(host);

    /// <summary>发送请求</summary>
    /// <param name="request">请求消息</param>
    /// <param name="cancellationToken">取消标记</param>
    /// <returns>响应消息</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var uri = request.RequestUri;
        if (uri == null) return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (IPAddress.TryParse(uri.Host, out _)) return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var addrs = Resolve(uri.Host);
        if (addrs is { Length: > 0 })
        {
            IPAddress addr;
#if NET5_0_OR_GREATER
            if (!request.Options.TryGetValue(_dnsIndexKey, out var idx)) idx = 0;
            addr = addrs[idx % addrs.Length];
            request.Options.Set(_dnsIndexKey, unchecked(idx + 1));
#else
            var idx = request.Properties.TryGetValue("dnsIndex", out var obj) ? obj.ToInt() : 0;
            addr = addrs[idx % addrs.Length];
            request.Properties["dnsIndex"] = unchecked(idx + 1);
#endif

            if (!addr.ToString().Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Host ??= uri.Host;
                var builder = new UriBuilder(uri)
                {
                    Host = addr.ToString(),
                };

                request.RequestUri = builder.Uri;
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}