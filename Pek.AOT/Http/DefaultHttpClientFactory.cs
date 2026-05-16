using System.Collections.Concurrent;
using System.Diagnostics;

using Pek.Log;
using Pek.Net;
using Pek.Threading;

namespace Pek.Http;

/// <summary>默认HttpClient工厂</summary>
public class DefaultHttpClientFactory : IHttpClientFactory
{
    /// <summary>处理器有效时间</summary>
    public TimeSpan HandlerLifetime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>是否使用代理</summary>
    public Boolean UseProxy { get; set; }

    /// <summary>是否使用Cookie</summary>
    public Boolean UseCookie { get; set; }

    /// <summary>是否忽略证书错误</summary>
    public Boolean IgnoreSsl { get; set; }

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; } = HttpHelper.Tracer;

    /// <summary>DNS解析器</summary>
    public IDnsResolver? Resolver { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private readonly Func<String, Lazy<ActiveHandlerTrackingEntry>> _entryFactory;
    private readonly TimeSpan _defaultCleanupInterval = TimeSpan.FromSeconds(10);
    private readonly Object _cleanupTimerLock = new();
    private readonly Object _cleanupActiveLock = new();
    private readonly TimerCallback _expiryCallback;
    private TimerX? _cleanupTimer;

    internal readonly ConcurrentDictionary<String, Lazy<ActiveHandlerTrackingEntry>> _activeHandlers;
    internal readonly ConcurrentQueue<ExpiredHandlerTrackingEntry> _expiredHandlers;

    /// <summary>实例化</summary>
    public DefaultHttpClientFactory()
    {
        _activeHandlers = new ConcurrentDictionary<String, Lazy<ActiveHandlerTrackingEntry>>(StringComparer.Ordinal);
        _entryFactory = name => new Lazy<ActiveHandlerTrackingEntry>(() => CreateHandlerEntry(name), LazyThreadSafetyMode.ExecutionAndPublication);
        _expiredHandlers = new ConcurrentQueue<ExpiredHandlerTrackingEntry>();
        _expiryCallback = ExpiryTimer_Tick;
    }

    /// <summary>创建HttpClient</summary>
    /// <param name="name">名称</param>
    /// <returns>HttpClient</returns>
    public virtual HttpClient CreateClient(String name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));

        var handler = CreateHandler(name);
        var client = new HttpClient(handler, disposeHandler: false);
        client.SetUserAgent();

        return client;
    }

    /// <summary>创建处理器</summary>
    /// <param name="name">名称</param>
    /// <returns>消息处理器</returns>
    public virtual HttpMessageHandler CreateHandler(String name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));

        var entry = _activeHandlers.GetOrAdd(name, _entryFactory).Value;
        StartHandlerEntryTimer(entry);

        return entry.Handler;
    }

    internal ActiveHandlerTrackingEntry CreateHandlerEntry(String name)
    {
        var handler = new LifetimeTrackingHttpMessageHandler(CreateInnerHandler(name));
        return new ActiveHandlerTrackingEntry(name, handler, HandlerLifetime);
    }

    /// <summary>创建内部处理器</summary>
    /// <param name="name">名称</param>
    /// <returns>内部处理器</returns>
    protected virtual HttpMessageHandler CreateInnerHandler(String name)
    {
        HttpMessageHandler handler = HttpHelper.CreateHandler(UseProxy, UseCookie, IgnoreSsl);
        var resolver = Resolver;
        if (resolver != null) handler = new DnsHttpHandler(handler) { Resolver = resolver };

        var tracer = Tracer;
        if (tracer != null) handler = new HttpTraceHandler(handler) { Tracer = tracer };

        return handler;
    }

    internal void ExpiryTimer_Tick(Object? state)
    {
        if (state is not ActiveHandlerTrackingEntry active) throw new ArgumentNullException(nameof(state));

        var removed = _activeHandlers.TryRemove(active.Name, out var found);
        Debug.Assert(removed, "Entry not found. We should always be able to remove the entry");
        Debug.Assert(found != null && Object.ReferenceEquals(active, found.Value), "Different entry found. The entry should not have been replaced");

        _expiredHandlers.Enqueue(new ExpiredHandlerTrackingEntry(active));
        StartCleanupTimer();
    }

    internal virtual void StartHandlerEntryTimer(ActiveHandlerTrackingEntry entry) => entry.StartExpiryTimer(_expiryCallback);

    internal virtual void StartCleanupTimer()
    {
        lock (_cleanupTimerLock)
        {
            _cleanupTimer ??= TimerX.Delay(CleanupTimer_Tick, (Int32)_defaultCleanupInterval.TotalMilliseconds);
        }
    }

    internal virtual void StopCleanupTimer()
    {
        lock (_cleanupTimerLock)
        {
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
        }
    }

    internal void CleanupTimer_Tick(Object? state)
    {
        StopCleanupTimer();

        if (!Monitor.TryEnter(_cleanupActiveLock))
        {
            StartCleanupTimer();
            return;
        }

        try
        {
            var initialCount = _expiredHandlers.Count;
            for (var i = 0; i < initialCount; i++)
            {
                _expiredHandlers.TryDequeue(out var entry);
                Debug.Assert(entry != null, "Entry was null, we should always get an entry back from TryDequeue");
                if (entry == null) continue;

                if (entry.CanDispose)
                {
                    try
                    {
                        entry.InnerHandler.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Dispose HttpMessageHandler error: {0}", ex.Message);
                        XTrace.WriteException(ex);
                    }
                }
                else
                {
                    _expiredHandlers.Enqueue(entry);
                }
            }
        }
        finally
        {
            Monitor.Exit(_cleanupActiveLock);
        }

        if (_expiredHandlers.Count > 0) StartCleanupTimer();
    }
}

internal class ActiveHandlerTrackingEntry
{
    private readonly Object _lock = new();
    private Boolean _timerInitialized;
    private TimerX? _timer;
    private TimerCallback? _callback;

    public ActiveHandlerTrackingEntry(String name, LifetimeTrackingHttpMessageHandler handler, TimeSpan lifetime)
    {
        Name = name;
        Handler = handler;
        Lifetime = lifetime;
    }

    public LifetimeTrackingHttpMessageHandler Handler { get; }

    public TimeSpan Lifetime { get; }

    public String Name { get; }

    public void StartExpiryTimer(TimerCallback callback)
    {
        if (Lifetime <= TimeSpan.Zero) return;
        if (Volatile.Read(ref _timerInitialized)) return;

        lock (_lock)
        {
            if (Volatile.Read(ref _timerInitialized)) return;

            _callback = callback;
            _timer = TimerX.Delay(Timer_Tick, (Int32)Lifetime.TotalMilliseconds);
            _timerInitialized = true;
        }
    }

    private void Timer_Tick(Object? state)
    {
        Debug.Assert(_callback != null);

        lock (_lock)
        {
            if (_timer == null) return;

            _timer.Dispose();
            _timer = null;
            _callback?.Invoke(this);
        }
    }
}

internal class ExpiredHandlerTrackingEntry
{
    private readonly WeakReference _livenessTracker;

    public ExpiredHandlerTrackingEntry(ActiveHandlerTrackingEntry other)
    {
        Name = other.Name;
        _livenessTracker = new WeakReference(other.Handler);
        InnerHandler = other.Handler.InnerHandler ?? throw new InvalidOperationException("InnerHandler is null.");
    }

    public Boolean CanDispose => !_livenessTracker.IsAlive;

    public HttpMessageHandler InnerHandler { get; }

    public String Name { get; }
}

internal class LifetimeTrackingHttpMessageHandler : DelegatingHandler
{
    public LifetimeTrackingHttpMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    protected override void Dispose(Boolean disposing)
    {
        // 生命周期由 DefaultHttpClientFactory 跟踪，避免 HttpClient 提前释放共享内部处理器。
    }
}