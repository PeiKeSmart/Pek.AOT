using Pek.Log;

namespace Pek.Http;

/// <summary>支持APM跟踪的HttpClient处理器</summary>
/// <param name="innerHandler">内部处理器</param>
public class HttpTraceHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    /// <summary>APM跟踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>异常过滤器</summary>
    public Predicate<Exception>? ExceptionFilter { get; set; }

    /// <summary>发送请求</summary>
    /// <param name="request">请求消息</param>
    /// <param name="cancellationToken">取消标记</param>
    /// <returns>响应消息</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var tracer = Tracer;
        var uri = request.RequestUri;
        if (tracer == null || uri == null) return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var parent = DefaultSpan.Current;
        if (parent != null && parent.Tag == uri + String.Empty || request.Headers.Contains("traceparent"))
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        using var span = tracer.NewSpan(request);
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (tracer.Resolver is DefaultTracerResolver resolver && resolver.RequestContentAsTag)
                span?.AppendTag(response);

            return response;
        }
        catch (Exception ex)
        {
            if (ExceptionFilter == null || ExceptionFilter(ex)) span?.SetError(ex, null);
            throw;
        }
    }
}