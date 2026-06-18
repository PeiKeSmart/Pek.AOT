namespace Pek.Model;

/// <summary>用于创建对象的工厂接口</summary>
/// <typeparam name="T">对象类型</typeparam>
public interface IFactory<T>
{
    /// <summary>创建对象实例</summary>
    /// <param name="args">参数</param>
    /// <returns>对象实例</returns>
    T Create(Object? args = null);
}

// AOT: skipped - Factory<T> 依赖 typeof(T).CreateInstance() 动态反射，AOT 环境下不可用
// 原始实现使用 NewLife.Reflection 的 CreateInstance 扩展方法
// public class Factory<T> : IFactory<T>
// {
//     public virtual T Create(Object? args = null) => (T)typeof(T).CreateInstance();
// }

/// <summary>模型扩展</summary>
public static class ModelExtension
{
    /// <summary>获取指定类型的服务对象</summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>服务实例</returns>
    public static T? GetService<T>(this IServiceProvider provider)
    {
        if (provider == null) return default;

        //// 服务类是否当前类的基类
        //if (provider.GetType().As<T>()) return (T)provider;

        return (T?)provider.GetService(typeof(T));
    }

    /// <summary>获取必要的服务，不存在时抛出异常</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务实例</returns>
    public static Object GetRequiredService(this IServiceProvider provider, Type serviceType)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

        return provider.GetService(serviceType) ?? throw new InvalidOperationException($"Unregistered type {serviceType.FullName}");
    }

    /// <summary>获取必要的服务，不存在时抛出异常</summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>服务实例</returns>
    public static T GetRequiredService<T>(this IServiceProvider provider) => provider == null ? throw new ArgumentNullException(nameof(provider)) : (T)provider.GetRequiredService(typeof(T));

    /// <summary>获取一批服务</summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>服务枚举</returns>
    public static IEnumerable<T> GetServices<T>(this IServiceProvider provider) => provider.GetServices(typeof(T)).Cast<T>();

    /// <summary>获取一批服务</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务枚举</returns>
    public static IEnumerable<Object> GetServices(this IServiceProvider provider, Type serviceType)
    {
        //var sp = provider as ServiceProvider;
        //if (sp == null && provider is MyServiceScope scope) sp = scope.MyServiceProvider as ServiceProvider;
        //var sp = provider.GetService<ServiceProvider>();
        //if (sp != null && sp.Container is ObjectContainer ioc)
        var ioc = GetService<ObjectContainer>(provider);
        if (ioc != null)
        {
            //var list = new List<Object>();
            //foreach (var item in ioc.Services)
            //{
            //    if (item.ServiceType == serviceType) list.Add(ioc.Resolve(item, provider));
            //}
            for (var i = ioc.Services.Count - 1; i >= 0; i--)
            {
                var item = ioc.Services[i];
                if (item.ServiceType == serviceType) yield return ioc.Resolve(item, provider);
            }
            //return list;
        }
        else
        {
            // AOT: MakeGenericType 在运行时动态泛型不可用，改为直接通过 ObjectContainer 解析
            // var serviceType2 = typeof(IEnumerable<>)!.MakeGenericType(serviceType);
            // var enums = (IEnumerable<Object>)provider.GetRequiredService(serviceType2);

            // 回退：从 ObjectContainer 直接查找
            var ioc2 = ObjectContainer.Current as ObjectContainer;
            if (ioc2 != null)
            {
                for (var i = ioc2.Services.Count - 1; i >= 0; i--)
                {
                    var item = ioc2.Services[i];
                    if (item.ServiceType == serviceType) yield return ioc2.Resolve(item, provider);
                }
            }
        }
    }

    /// <summary>创建范围作用域，该作用域内提供者解析一份数据</summary>
    /// <param name="provider">服务提供者</param>
    /// <returns>作用域实例</returns>
    public static IServiceScope? CreateScope(this IServiceProvider provider)
    {
        var factory = provider.GetService<IServiceScopeFactory>();

        // 如果工厂内提供者不是现在的提供者，则重新设置
        if (factory == null || factory is IServiceScopeFactory scopeFactory && scopeFactory != provider)
        {
            if (provider is Data.IExtend extend)
            {
                if (extend["__IServiceScopeFactory"] is not IServiceScopeFactory factory2)
                {
                    factory2 = new MyServiceScopeFactory { ServiceProvider = provider };
                    extend["__IServiceScopeFactory"] = factory2;
                }

                return factory2.CreateScope();
            }
        }

        return factory?.CreateScope();
    }

    /// <summary>创建服务对象，使用服务提供者来填充构造函数</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="serviceType">服务类型</param>
    /// <returns>服务实例</returns>
    public static Object? CreateInstance(this IServiceProvider provider, Type serviceType) => ObjectContainer.CreateInstance(serviceType, provider, null, false);
}
