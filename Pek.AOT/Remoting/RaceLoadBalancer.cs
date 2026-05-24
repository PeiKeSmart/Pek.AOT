using System.Diagnostics;

using Pek.Log;

using BclHttpClient = System.Net.Http.HttpClient;

namespace Pek.Remoting;

/// <summary>竞速负载均衡器</summary>
public class RaceLoadBalancer : LoadBalancerBase, ITracerFeature
{
    #region 属性
    /// <summary>负载均衡模式</summary>
    public override LoadBalanceMode Mode => LoadBalanceMode.Race;

    /// <summary>RTT刷新间隔，秒。默认600秒</summary>
    public Int32 RefreshSeconds { get; set; } = 600;

    /// <summary>探测超时时间，毫秒。默认3000ms</summary>
    public Int32 ProbeTimeout { get; set; } = 3000;

    /// <summary>并行探测最大并发。默认8</summary>
    public Int32 MaxProbeConcurrency { get; set; } = 8;

    /// <summary>探测路径，附加到地址后。默认/cube/info</summary>
    public String ProbePath { get; set; } = "/cube/info";

    /// <summary>是否仅获取响应头进行探测。默认 false 使用完整 GET</summary>
    public Boolean ProbeHeadersOnly { get; set; }

    /// <summary>竞速启动延迟步长，毫秒。默认100ms</summary>
    public Int32 StartDelayStep { get; set; } = 100;

    /// <summary>自定义探测委托，返回RTT；返回null视为失败</summary>
    public Func<Uri, CancellationToken, Task<TimeSpan?>>? ProbeAsync { get; set; }

    /// <summary>链路追踪</summary>
    public ITracer? Tracer { get; set; }

    private readonly Object _lock = new();
    #endregion

    #region 方法
    /// <summary>获取一个服务用于处理请求</summary>
    /// <param name="services">服务列表</param>
    /// <returns>选中的服务节点</returns>
    public override ServiceEndpoint GetService(IList<ServiceEndpoint> services)
    {
        if (services == null || services.Count == 0)
            throw new InvalidOperationException("No available service nodes!");

        EnsureAvailable(services);

        foreach (var service in services)
        {
            if (service.IsAvailable())
            {
                service.Times++;
                return service;
            }
        }

        var first = services[0];
        first.Times++;
        return first;
    }

    /// <summary>获取所有可用服务用于竞速调用，按优先级和RTT排序</summary>
    /// <param name="services">服务列表</param>
    /// <param name="forceProbe">是否强制探测全部地址</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>已排序的可用服务列表</returns>
    public async Task<IList<ServiceEndpoint>> GetAllServicesAsync(IList<ServiceEndpoint> services, Boolean forceProbe, CancellationToken cancellationToken)
    {
        EnsureAvailable(services);

        var available = services.Where(item => item.IsAvailable()).ToList();
        if (available.Count == 0) return [];

        var hasUsable = available.Any(item => item.IsAvailable());
        var hasStale = available.Any(ShouldProbe);

        if (forceProbe || (!hasUsable && hasStale))
        {
            await ProbeEndpointsAsync(available, forceProbe, cancellationToken).ConfigureAwait(false);
            available = services.Where(item => item.IsAvailable()).ToList();
        }
        else if (hasStale)
        {
            _ = Task.Run(() => ProbeEndpointsAsync(available, false, CancellationToken.None), cancellationToken);
        }

        var sorted = available
            .OrderBy(item => (Int32)item.Category)
            .ThenBy(item => item.Rtt ?? TimeSpan.MaxValue)
            .ThenBy(item => item.Errors)
            .ThenBy(item => item.Address.AbsoluteUri)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].Score = i * StartDelayStep;
        }

        return sorted;
    }

    /// <summary>标记服务成功，更新RTT</summary>
    /// <param name="service">服务节点</param>
    /// <param name="elapsed">耗时</param>
    public void MarkSuccess(ServiceEndpoint service, TimeSpan elapsed)
    {
        if (service == null) return;

        lock (_lock)
        {
            service.LastSuccess = DateTime.Now;
            service.Errors = 0;
            service.NextProbe = DateTime.Now.AddSeconds(RefreshSeconds);
            service.Rtt = service.Rtt == null
                ? elapsed
                : TimeSpan.FromMilliseconds((service.Rtt.Value.TotalMilliseconds * 3 + elapsed.TotalMilliseconds) / 4);
        }
    }

    /// <summary>标记服务失败</summary>
    /// <param name="service">服务节点</param>
    /// <param name="error">异常</param>
    public void MarkFailure(ServiceEndpoint service, Exception? error)
    {
        if (service == null) return;

        lock (_lock)
        {
            service.LastFailure = DateTime.Now;
            service.Errors++;
            service.Rtt = null;
            service.NextProbe = DateTime.Now.AddSeconds(ShieldingTime);
        }
    }
    #endregion

    #region 探测
    private static Boolean ShouldProbe(ServiceEndpoint service) => service.NextProbe <= DateTime.Now;

    private async Task ProbeEndpointsAsync(IList<ServiceEndpoint> services, Boolean forceProbe, CancellationToken cancellationToken)
    {
        using var span = Tracer?.NewSpan("race:ProbeEndpoints", null, services.Count);

        var tasks = new List<Task>();
        using var semaphore = new SemaphoreSlim(MaxProbeConcurrency > 0 ? MaxProbeConcurrency : 1);

        foreach (var service in services)
        {
            if (forceProbe || ShouldProbe(service))
                tasks.Add(ProbeOneAsync(service, semaphore, cancellationToken));
        }

        if (tasks.Count > 0) await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ProbeOneAsync(ServiceEndpoint service, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var uri = new Uri(service.Address, ProbePath + String.Empty);
            var probe = ProbeAsync ?? ExecuteProbeAsync;
            var rtt = await probe(uri, cancellationToken).ConfigureAwait(false);
            if (rtt != null)
                MarkSuccess(service, rtt.Value);
            else
                MarkFailure(service, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<TimeSpan?> ExecuteProbeAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(ProbeTimeout > 0 ? ProbeTimeout : 1000);

        try
        {
            var watch = Stopwatch.StartNew();
            using var client = new BclHttpClient { Timeout = TimeSpan.FromMilliseconds(ProbeTimeout > 0 ? ProbeTimeout : 1000) };
            var completion = ProbeHeadersOnly ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
            using var response = await client.GetAsync(uri, completion, source.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return watch.Elapsed;
        }
        catch
        {
            return null;
        }
    }
    #endregion
}