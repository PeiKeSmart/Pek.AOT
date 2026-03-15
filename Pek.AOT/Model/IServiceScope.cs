using System.Collections.Concurrent;

namespace Pek.Model;

/// <summary>范围服务。该范围生命周期内，每个服务类型只有一个实例</summary>
/// <remarks>
/// 满足 Singleton 和 Scoped 的要求，暂时无法满足 Transient 的要求（仍然只有一份）。
/// </remarks>
public interface IServiceScope : IDisposable
{
    /// <summary>服务提供者</summary>
    IServiceProvider ServiceProvider { get; }
}

class MyServiceScope : IServiceScope, IServiceProvider
{
    public IServiceProvider? MyServiceProvider { get; set; }

    public IServiceProvider ServiceProvider => this;

    private readonly ConcurrentDictionary<Type, Object?> _cache = new();

    public void Dispose()
    {
        _cache.Clear();
    }

    public Object? GetService(Type serviceType)
    {
        while (true)
        {
            if (_cache.TryGetValue(serviceType, out var service)) return service;

            service = MyServiceProvider?.GetService(serviceType);

            if (_cache.TryAdd(serviceType, service)) return service;
        }
    }
}