namespace Pek.Remoting;

/// <summary>服务解析器。根据服务名获取通信客户端，用于服务发现场景</summary>
public interface IServiceResolver
{
    /// <summary>为指定服务获取客户端，自动从注册中心订阅服务地址并支持动态更新</summary>
    /// <param name="serviceName">服务名。用于在配置中心或注册中心定位服务地址</param>
    /// <param name="tag">特性标签。用于区分同一服务的不同环境或分组，为空时不区分</param>
    /// <returns>与服务通信的客户端实例</returns>
    Task<IApiClient> GetClientAsync(String serviceName, String? tag = null);

    /// <summary>解析服务的地址列表，供调用方自行创建客户端</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="tag">特性标签。为空时返回所有地址</param>
    /// <returns>地址列表，未找到时返回空数组</returns>
    Task<String[]> ResolveAddressesAsync(String serviceName, String? tag = null);
}