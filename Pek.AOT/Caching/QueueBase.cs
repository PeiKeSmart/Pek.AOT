#nullable enable

using Pek.Extension;
using Pek.Log;
using Pek.Net;

namespace Pek.Caching;

/// <summary>消息队列基类</summary>
public abstract class QueueBase : RedisBase
{
    /// <summary>追踪名。默认为 Key</summary>
    public String TraceName { get; set; }

    /// <summary>是否自动附加 TraceId。当前仅保留兼容属性表面</summary>
    public Boolean AttachTraceId { get; set; } = true;

    /// <summary>失败时是否抛出异常</summary>
    public Boolean ThrowOnFailure { get; set; }

    /// <summary>发送失败重试次数</summary>
    public Int32 RetryTimesWhenSendFailed { get; set; } = 3;

    /// <summary>发送失败重试间隔，毫秒</summary>
    public Int32 RetryIntervalWhenSendFailed { get; set; } = 1_000;

    /// <summary>消息主题</summary>
    public String Topic => Key;

    /// <summary>埋点使用的主机名</summary>
    protected String _traceHost;

    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    protected QueueBase(Redis redis, String key) : base(redis, key)
    {
        TraceName = key;

        _traceHost = redis.Name;
        if (_traceHost.IsNullOrEmpty() || _traceHost.EqualIgnoreCase("Redis", "FullRedis"))
        {
            var server = redis.Server;
            if (!server.IsNullOrEmpty())
            {
                var index = server.IndexOfAny([',', ';']);
                if (index > 0) server = server[..index];

                var uri = new NetUri(server);
                _traceHost = uri.Host ?? uri.Address.ToString();
            }
        }
    }

    /// <summary>验证发送失败</summary>
    /// <param name="span">埋点</param>
    protected void ValidWhenSendFailed(ISpan? span)
    {
        var exception = new InvalidOperationException($"发布到队列[{Topic}]失败！");
        span?.SetError(exception, null);
        if (ThrowOnFailure) throw exception;
    }
}

#nullable restore