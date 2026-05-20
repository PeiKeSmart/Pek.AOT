using System.Net.Http;

namespace Pek.Remoting;

/// <summary>故障转移负载均衡器</summary>
public class FailoverLoadBalancer : LoadBalancerBase
{
    #region 属性
    /// <summary>负载均衡模式</summary>
    public override LoadBalanceMode Mode => LoadBalanceMode.Failover;

    /// <summary>当前服务索引</summary>
    private volatile Int32 _currentIndex;
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

        var index = _currentIndex;
        if (index > 0 && services[0].IsAvailable())
        {
            index = _currentIndex = 0;
            Log?.Debug("主节点[{0}]恢复可用，切回主节点", services[0].Name);
        }

        var service = services[index % services.Count];
        service.Times++;

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
            var nextIndex = _currentIndex + 1;
            if (nextIndex < services.Count)
            {
                _currentIndex = nextIndex;
                Log?.Debug("服务节点[{0}]网络异常，切换到节点[{1}]，使用地址：{2}", service.Name, services[nextIndex].Name, services[nextIndex].Address);
            }
        }
    }
    #endregion
}