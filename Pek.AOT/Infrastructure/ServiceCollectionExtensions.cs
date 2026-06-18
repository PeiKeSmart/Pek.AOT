namespace Pek.Infrastructure;

/// <summary>服务集合扩展（AOT安全版 - 仅标准 IServiceProvider 操作，已移除 ObjectContainer/MakeGenericType 依赖）</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>获取指定类型的服务对象</summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>服务实例</returns>
    public static T? GetPekService<T>(this IServiceProvider provider)
    {
        if (provider == null) return default;

        return (T?)provider.GetService(typeof(T));
    }

    /// <summary>获取必要的服务，不存在时抛出异常</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务实例</returns>
    public static Object GetPekRequiredService(this IServiceProvider provider, Type serviceType)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

        return provider.GetService(serviceType) ?? throw new InvalidOperationException($"Unregistered type {serviceType.FullName}");
    }

    /// <summary>获取必要的服务，不存在时抛出异常</summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>服务实例</returns>
    public static T GetPekRequiredService<T>(this IServiceProvider provider) => provider == null ? throw new ArgumentNullException(nameof(provider)) : (T)provider.GetPekRequiredService(typeof(T));

    // GetPekServices 方法依赖 ObjectContainer.Resolve 和 MakeGenericType，不适合 AOT 环境，已跳过。
    // 如需批量解析服务，请在 DI 注册时使用 IEnumerable<T> 注入。
}
