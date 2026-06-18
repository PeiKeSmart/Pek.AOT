using System.Collections.Concurrent;

namespace Pek.Helpers;

/// <summary>对象激活工厂委托。AOT 安全版——使用工厂注册替代反射构造器解析</summary>
/// <param name="serviceProvider">服务提供者</param>
/// <param name="arguments">构造参数</param>
public delegate Object ObjectFactory(IServiceProvider serviceProvider, Object[] arguments);

/// <summary>对象激活辅助类。AOT 安全版，通过静态注册工厂替代 Activator.CreateInstance</summary>
public static class ActivatorHelper
{
    private static readonly ConcurrentDictionary<Type, ObjectFactory> _factories = new();

    /// <summary>注册类型的激活工厂。必须在首次创建实例前调用</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="factory">激活工厂函数</param>
    public static void Register<T>(Func<IServiceProvider, Object[], T> factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories[typeof(T)] = (sp, args) => factory(sp, args)!;
    }

    /// <summary>创建类型实例（需提前 Register）。AOT 安全版</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="parameters">构造参数</param>
    public static T? CreateInstance<T>(params Object[] parameters)
    {
        if (_factories.TryGetValue(typeof(T), out var factory))
            return (T?)factory(null!, parameters);

        throw new InvalidOperationException($"Type {typeof(T).Name} has not been registered for AOT activation. Call ActivatorHelper.Register<{typeof(T).Name}>() at startup.");
    }

    /// <summary>通过服务提供者创建实例（需提前 Register）</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="instanceType">实例类型</param>
    /// <param name="parameters">构造参数</param>
    public static Object CreateInstance(this IServiceProvider provider, Type instanceType, params Object[] parameters)
    {
        if (_factories.TryGetValue(instanceType, out var factory))
            return factory(provider, parameters);

        throw new InvalidOperationException($"Type {instanceType.Name} has not been registered for AOT activation.");
    }

    /// <summary>通过服务提供者创建泛型实例</summary>
    public static T CreateInstance<T>(this IServiceProvider provider, params Object[] parameters)
        => (T)CreateInstance(provider, typeof(T), parameters);

    /// <summary>从容器获取服务，若未注册则通过工厂创建</summary>
    public static T GetServiceOrCreateInstance<T>(this IServiceProvider provider)
    {
        var svc = provider.GetService(typeof(T));
        if (svc != null) return (T)svc;
        return CreateInstance<T>(provider);
    }

    /// <summary>从容器获取服务，若未注册则通过工厂创建</summary>
    public static Object GetServiceOrCreateInstance(this IServiceProvider provider, Type type)
    {
        var svc = provider.GetService(type);
        if (svc != null) return svc;
        return CreateInstance(provider, type);
    }
}
