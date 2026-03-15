using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Pek.Model;

/// <summary>对象容器助手。扩展方法专用</summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class ObjectContainerHelper
{
    #region 单实例注册
    /// <summary>添加单实例，指定实现类型</summary>
    /// <param name="container">对象容器</param>
    /// <param name="serviceType">服务类型</param>
    /// <param name="implementationType">实现类型</param>
    /// <returns>对象容器</returns>
    public static IObjectContainer AddSingleton(this IObjectContainer container, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));

        var item = new ServiceDescriptor(serviceType, implementationType)
        {
            Lifetime = ObjectLifetime.Singleton,
        };
        container.Add(item);

        return container;
    }

    /// <summary>添加单实例，指定实现类型</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <typeparam name="TImplementation">实现类型</typeparam>
    /// <param name="container">对象容器</param>
    /// <returns>对象容器</returns>
    public static IObjectContainer AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IObjectContainer container) where TService : class where TImplementation : class, TService => container.AddSingleton(typeof(TService), typeof(TImplementation));

    /// <summary>添加单实例，指定实例工厂</summary>
    /// <param name="container">对象容器</param>
    /// <param name="serviceType">服务类型</param>
    /// <param name="factory">实例工厂</param>
    /// <returns>对象容器</returns>
    public static IObjectContainer AddSingleton(this IObjectContainer container, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType, Func<IServiceProvider, Object> factory)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        var item = new ServiceDescriptor(serviceType)
        {
            Factory = factory,
            Lifetime = ObjectLifetime.Singleton,
        };
        container.Add(item);

        return container;
    }

    /// <summary>添加单实例，指定实例工厂</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <param name="container">对象容器</param>
    /// <param name="factory">实例工厂</param>
    /// <returns>对象容器</returns>
    public static IObjectContainer AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IObjectContainer container, Func<IServiceProvider, TService> factory) where TService : class => container.AddSingleton(typeof(TService), factory);

    /// <summary>添加单实例，指定实例</summary>
    /// <param name="container">对象容器</param>
    /// <param name="serviceType">服务类型</param>
    /// <param name="instance">实例</param>
    /// <returns>对象容器</returns>
    public static IObjectContainer AddSingleton(this IObjectContainer container, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType, Object? instance)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
        if (instance == null && (serviceType.IsAbstract || serviceType.IsInterface)) throw new ArgumentNullException(nameof(instance));

        var item = new ServiceDescriptor(serviceType)
        {
            Instance = instance,
            Lifetime = ObjectLifetime.Singleton,
        };
        container.Add(item);

        return container;
    }

    /// <summary>添加单实例，指定实例</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <param name="container">对象容器</param>
    /// <param name="instance">实例</param>
    /// <returns>对象容器</returns>
    public static IObjectContainer AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IObjectContainer container, TService? instance = null) where TService : class => container.AddSingleton(typeof(TService), instance);
    #endregion

    #region 构建
    /// <summary>从对象容器创建服务提供者</summary>
    /// <param name="container">对象容器</param>
    /// <returns>服务提供者</returns>
    public static IServiceProvider BuildServiceProvider(this IObjectContainer container) => BuildServiceProvider(container, null);

    /// <summary>从对象容器创建服务提供者，支持传入另一个服务提供者作为内部提供者</summary>
    /// <param name="container">对象容器</param>
    /// <param name="innerServiceProvider">内部服务提供者</param>
    /// <returns>服务提供者</returns>
    public static IServiceProvider BuildServiceProvider(this IObjectContainer container, IServiceProvider? innerServiceProvider)
    {
        if (container == null) throw new ArgumentNullException(nameof(container));

        var provider = container.GetService<IServiceProvider>();
        if (provider != null) return provider;

        var provider2 = new ServiceProvider(container, innerServiceProvider);

        container.TryAddSingleton(provider2);

        provider = container.GetService<IServiceProvider>();
        if (provider != null) return provider;

        return provider2;
    }

    /// <summary>获取指定类型的服务对象</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    /// <param name="container">对象容器</param>
    /// <returns>服务实例</returns>
    public static TService? GetService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IObjectContainer container) => (TService?)container.GetService(typeof(TService));
    #endregion

    #region 旧版方法
    /// <summary>解析类型的实例</summary>
    /// <typeparam name="TService">接口类型</typeparam>
    /// <param name="container">对象容器</param>
    /// <returns>服务实例</returns>
    [Obsolete("=>GetService")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TService? Resolve<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IObjectContainer container) => (TService?)container.GetService(typeof(TService));
    #endregion

    private static IObjectContainer TryAddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IObjectContainer container, TService? instance = null) where TService : class
    {
        if (container == null) throw new ArgumentNullException(nameof(container));

        var item = new ServiceDescriptor(typeof(TService))
        {
            Instance = instance,
            Lifetime = ObjectLifetime.Singleton,
        };
        if (instance == null) item.ImplementationType = typeof(TService);
        container.TryAdd(item);

        return container;
    }
}