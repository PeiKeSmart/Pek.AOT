using System.Net.Http;

namespace Pek.Remoting;

/// <summary>加权轮询负载均衡器</summary>
public class WeightedRoundRobinLoadBalancer : LoadBalancerBase
{
    #region 属性
    /// <summary>负载均衡模式</summary>
    public override LoadBalanceMode Mode => LoadBalanceMode.RoundRobin;

    /// <summary>调度索引，当前使用该索引处的服务</summary>
    private volatile Int32 _serverIndex;
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

        ServiceEndpoint? service = null;
        for (var i = 0; i < services.Count; i++)
        {
            service = services[_serverIndex % services.Count];

            var hasQuota = service.Weight <= 0 || service.Index < service.Weight || services.Count == 1;
            var isAvailable = service.IsAvailable();
            if (hasQuota && isAvailable) break;

            service.Index = 0;
            service = null;
            _serverIndex++;
        }

        if (service == null && services.Count > 0) service = services[0];
        if (service == null) throw new InvalidOperationException("No available service nodes!");

        service.Times++;

        service.Index++;
        if (service.Index >= service.Weight && service.Weight > 0)
        {
            service.Index = 0;
            _serverIndex++;
        }

        if (_serverIndex >= services.Count) _serverIndex = 0;

        return service;
    }

    /// <summary>归还服务，报告请求结果</summary>
    /// <param name="services">服务列表</param>
    /// <param name="service">归还的服务</param>
    /// <param name="error">异常信息，null表示成功</param>
    public override void PutService(IList<ServiceEndpoint> services, ServiceEndpoint service, Exception? error)
    {
        base.PutService(services, service, error);

        var current = error;
        while (current is AggregateException aggregateException) current = aggregateException.InnerException;

        if (current is HttpRequestException or TaskCanceledException)
        {
            _serverIndex++;
            Log?.Debug("服务节点[{0}]网络异常，跳过该节点", service.Name);
        }
    }
    #endregion
}