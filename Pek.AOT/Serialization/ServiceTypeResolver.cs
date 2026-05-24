using System.Collections.Concurrent;

namespace Pek.Serialization;

/// <summary>支持服务提供者的类型解析器</summary>
/// <remarks>
/// AOT 版本不再修改 JsonTypeInfo.CreateObject，也不做运行时动态构造。
/// 它只负责把抽象/接口类型解析为一个已经显式注册或可由服务提供者识别出的具体实现类型，
/// 后续仍然交给现有 JsonTypeInfo 主链完成反序列化。
/// </remarks>
public class ServiceTypeResolver
{
    private readonly ConcurrentDictionary<Type, Type> _mappings = new();

    static ServiceTypeResolver()
    {
        var resolver = Default;
        resolver.Register<IList<Object?>, List<Object?>>();
        resolver.Register<ICollection<Object?>, List<Object?>>();
        resolver.Register<IEnumerable<Object?>, List<Object?>>();
        resolver.Register<IReadOnlyList<Object?>, List<Object?>>();
        resolver.Register<IReadOnlyCollection<Object?>, List<Object?>>();
        resolver.Register<IDictionary<String, Object?>, Dictionary<String, Object?>>();
        resolver.Register<IReadOnlyDictionary<String, Object?>, Dictionary<String, Object?>>();
    }

    /// <summary>默认解析器实例</summary>
    public static ServiceTypeResolver Default { get; } = new();

    /// <summary>服务提供者工厂</summary>
    public Func<IServiceProvider>? GetServiceProvider { get; set; }

    /// <summary>注册服务类型到实现类型的映射</summary>
    /// <typeparam name="TService">抽象服务类型</typeparam>
    /// <typeparam name="TImplementation">具体实现类型</typeparam>
    public void Register<TService, TImplementation>() where TImplementation : class, TService =>
        Register(typeof(TService), typeof(TImplementation));

    /// <summary>注册列表接口映射</summary>
    /// <typeparam name="TItem">元素类型</typeparam>
    public void RegisterList<TItem>()
    {
        Register<IList<TItem>, List<TItem>>();
        Register<ICollection<TItem>, List<TItem>>();
        Register<IEnumerable<TItem>, List<TItem>>();
        Register<IReadOnlyList<TItem>, List<TItem>>();
        Register<IReadOnlyCollection<TItem>, List<TItem>>();
    }

    /// <summary>注册字典接口映射</summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public void RegisterDictionary<TKey, TValue>() where TKey : notnull
    {
        Register<IDictionary<TKey, TValue>, Dictionary<TKey, TValue>>();
        Register<IReadOnlyDictionary<TKey, TValue>, Dictionary<TKey, TValue>>();
    }

    /// <summary>注册服务类型到实现类型的映射</summary>
    /// <param name="serviceType">抽象服务类型</param>
    /// <param name="implementationType">具体实现类型</param>
    public void Register(Type serviceType, Type implementationType)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw new ArgumentOutOfRangeException(nameof(implementationType), $"{implementationType.FullName} is not assignable to {serviceType.FullName}.");

        _mappings[serviceType] = implementationType;
    }

    /// <summary>尝试解析实际类型</summary>
    /// <param name="serviceType">声明类型</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="implementationType">实际实现类型</param>
    /// <returns>是否解析成功</returns>
    public Boolean TryResolve(Type serviceType, IServiceProvider? serviceProvider, out Type implementationType)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

        if (_mappings.TryGetValue(serviceType, out implementationType!)) return true;

        serviceProvider ??= GetServiceProvider?.Invoke();
        var instance = serviceProvider?.GetService(serviceType);
        if (instance != null)
        {
            implementationType = instance.GetType();
            if (serviceType.IsAssignableFrom(implementationType)) return true;
        }

        implementationType = null!;
        return false;
    }

    /// <summary>解析实际类型；如果没有显式映射则返回原类型</summary>
    /// <param name="serviceType">声明类型</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <returns>实际类型</returns>
    public Type Resolve(Type serviceType, IServiceProvider? serviceProvider = null) =>
        TryResolve(serviceType, serviceProvider, out var implementationType) ? implementationType : serviceType;
}