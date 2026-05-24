using System.Reflection;
using System.Diagnostics.CodeAnalysis;

using Pek.Model;

namespace Pek.Remoting;

/// <summary>接口管理器</summary>
public class ApiManager : IApiManager
{
    private readonly Dictionary<String, ApiAction> _services = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>可提供服务的方法</summary>
    public IDictionary<String, ApiAction> Services => _services;

    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TService>() => Register(typeof(TService), null, null, static serviceProvider => CreateController<TService>(serviceProvider));

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    [RequiresUnreferencedCode("Registering arbitrary controller instances relies on runtime method discovery. Prefer Register<TService>().")]
    public void Register(Object controller, String? method)
    {
        if (controller == null) throw new ArgumentNullException(nameof(controller));

        Register(controller.GetType(), controller, method, null);
    }

    /// <summary>查找服务</summary>
    /// <param name="action">动作名称</param>
    /// <returns>Api动作</returns>
    public ApiAction? Find(String action)
    {
        if (String.IsNullOrEmpty(action)) return null;

        _services.TryGetValue(action.TrimStart('/'), out var api);
        return api;
    }

    private void Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType, Object? controller, String? method, Func<IServiceProvider?, Object?>? controllerFactory)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

        if (!String.IsNullOrEmpty(method))
        {
            var api = CreateAction(serviceType, controller, controllerFactory, method!);
            if (api != null) _services[api.Name] = api;

            return;
        }

        foreach (var item in GetCandidateMethods(serviceType))
        {
            var api = new ApiAction(item, serviceType) { Controller = controller, ControllerFactory = controllerFactory };
            _services[api.Name] = api;
        }
    }

    private static ApiAction? CreateAction([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] Type serviceType, Object? controller, Func<IServiceProvider?, Object?>? controllerFactory, String method)
    {
        var match = GetCandidateMethods(serviceType)
            .FirstOrDefault(e => String.Equals(e.Name, method, StringComparison.OrdinalIgnoreCase) || String.Equals(ApiAction.GetName(serviceType, e), method, StringComparison.OrdinalIgnoreCase));
        if (match == null) return null;

        return new ApiAction(match, serviceType) { Controller = controller, ControllerFactory = controllerFactory };
    }

    private static IEnumerable<MethodInfo> GetCandidateMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type serviceType)
    {
        var methods = serviceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (var item in methods)
        {
            if (item.IsSpecialName) continue;
            if (item.DeclaringType == typeof(Object)) continue;
            if (item.ContainsGenericParameters) continue;

            yield return item;
        }
    }

    private static Object? CreateController<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(IServiceProvider? serviceProvider)
    {
        serviceProvider ??= ObjectContainer.Provider;

        return serviceProvider.GetService(typeof(TService)) ?? ObjectContainer.CreateInstance(typeof(TService), serviceProvider, null, false);
    }
}