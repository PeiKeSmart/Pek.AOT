using System.Diagnostics.CodeAnalysis;

using Pek.Extension;
using Pek.Net;

namespace Pek.Http;

/// <summary>Http服务器</summary>
public class HttpServer : NetServer, IHttpHost
{
    /// <summary>Http响应头Server名称</summary>
    public String ServerName { get; set; }

    /// <summary>路由映射</summary>
    public IDictionary<String, IHttpHandler> Routes { get; set; } = new Dictionary<String, IHttpHandler>(StringComparer.OrdinalIgnoreCase);

    /// <summary>实例化Http服务器</summary>
    public HttpServer()
    {
        Name = "Http";
        Port = 80;
        ProtocolType = NetType.Http;

        var version = GetType().Assembly.GetName().Version ?? new Version();
        ServerName = $"Pek-HttpServer/{version.Major}.{version.Minor}";
    }

    /// <summary>为会话创建网络数据处理器</summary>
    /// <param name="session">会话</param>
    /// <returns>Http会话处理器</returns>
    public override INetHandler? CreateHandler(INetSession session) => new HttpSession();

    /// <summary>映射路由处理器</summary>
    /// <param name="path">路径</param>
    /// <param name="handler">处理器</param>
    public void Map(String path, IHttpHandler handler) => SetRoute(path, handler);

    /// <summary>映射路由处理委托</summary>
    /// <param name="path">路径</param>
    /// <param name="handler">处理委托</param>
    public void Map(String path, HttpProcessDelegate handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        SetRoute(path, new DelegateHandler { Callback = handler });
    }

    /// <summary>映射无参结果委托</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="path">路径</param>
    /// <param name="handler">处理委托</param>
    public void Map<TResult>(String path, Func<TResult> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        SetRoute(path, new DelegateHandler { Callback = () => (Object?)handler() });
    }

    /// <summary>映射带上下文的结果委托</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="path">路径</param>
    /// <param name="handler">处理委托</param>
    public void Map<TResult>(String path, Func<IHttpContext, TResult> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        SetRoute(path, new DelegateHandler { Callback = (Func<IHttpContext, Object?>)(context => handler(context)) });
    }

    /// <summary>映射控制器</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="path">路径</param>
    public void MapController<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String? path = null) where TController : class
        => MapController(typeof(TController), path);

    /// <summary>映射控制器</summary>
    /// <param name="controllerType">控制器类型</param>
    /// <param name="path">路径</param>
    public void MapController([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type controllerType, String? path = null)
    {
        if (controllerType == null) throw new ArgumentNullException(nameof(controllerType));

        if (path.IsNullOrEmpty())
        {
            var name = controllerType.Name;
            if (name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)) name = name[..^10];
            path = "/" + name;
        }

        var path2 = path.EnsureStart("/").EnsureEnd("/*");
        SetRoute(path2, new ControllerHandler { ControllerType = controllerType });
    }

    /// <summary>映射控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Action<TController> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射带上下文的控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Action<TController, IHttpContext> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, Object?> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射带上下文的控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, Object?> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射异步控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, Task<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射带上下文的异步控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, Task<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射ValueTask控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, ValueTask<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射带上下文的ValueTask控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String actionName, Func<TController, IHttpContext, ValueTask<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, handler);
    }

    /// <summary>映射单参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Func<TController, T1, Object?> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, handler);
    }

    /// <summary>映射单参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Action<TController, T1> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, handler);
    }

    /// <summary>映射单参数异步控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Func<TController, T1, Task<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, handler);
    }

    /// <summary>映射单参数ValueTask控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1>(String actionName, HttpParameterBinder<T1> binder1, Func<TController, T1, ValueTask<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, handler);
    }

    /// <summary>映射双参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Func<TController, T1, T2, Object?> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, handler);
    }

    /// <summary>映射双参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Action<TController, T1, T2> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, handler);
    }

    /// <summary>映射双参数异步控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Func<TController, T1, T2, Task<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, handler);
    }

    /// <summary>映射双参数ValueTask控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, Func<TController, T1, T2, ValueTask<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, handler);
    }

    /// <summary>映射三参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Func<TController, T1, T2, T3, Object?> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, handler);
    }

    /// <summary>映射三参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Action<TController, T1, T2, T3> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, handler);
    }

    /// <summary>映射三参数异步控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Func<TController, T1, T2, T3, Task<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, handler);
    }

    /// <summary>映射三参数ValueTask控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, Func<TController, T1, T2, T3, ValueTask<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, handler);
    }

    /// <summary>映射四参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, Object?> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, binder4, handler);
    }

    /// <summary>映射四参数控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Action<TController, T1, T2, T3, T4> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, binder4, handler);
    }

    /// <summary>映射四参数异步控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, Task<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, binder4, handler);
    }

    /// <summary>映射四参数ValueTask控制器Action</summary>
    /// <typeparam name="TController">控制器类型</typeparam>
    /// <typeparam name="T1">参数1类型</typeparam>
    /// <typeparam name="T2">参数2类型</typeparam>
    /// <typeparam name="T3">参数3类型</typeparam>
    /// <typeparam name="T4">参数4类型</typeparam>
    /// <param name="actionName">Action名称</param>
    /// <param name="binder1">参数1绑定器</param>
    /// <param name="binder2">参数2绑定器</param>
    /// <param name="binder3">参数3绑定器</param>
    /// <param name="binder4">参数4绑定器</param>
    /// <param name="handler">处理器</param>
    /// <param name="path">控制器路径</param>
    public void MapAction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController, T1, T2, T3, T4>(String actionName, HttpParameterBinder<T1> binder1, HttpParameterBinder<T2> binder2, HttpParameterBinder<T3> binder3, HttpParameterBinder<T4> binder4, Func<TController, T1, T2, T3, T4, ValueTask<Object?>> handler, String? path = null) where TController : class
    {
        EnsureControllerMapped<TController>(path);
        ControllerActionRegistry.Register(actionName, binder1, binder2, binder3, binder4, handler);
    }

    /// <summary>映射静态文件目录</summary>
    /// <param name="path">映射路径</param>
    /// <param name="contentPath">内容目录</param>
    public void MapStaticFiles(String path, String contentPath)
    {
        if (contentPath.IsNullOrEmpty()) throw new ArgumentNullException(nameof(contentPath));

        path = path.EnsureStart("/");
        var path2 = path.EnsureEnd("/").EnsureEnd("*");
        SetRoute(path2, new StaticFilesHandler { Path = path.EnsureEnd("/"), ContentPath = contentPath });
    }

    private void EnsureControllerMapped<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController>(String? path) where TController : class
    {
        var controllerType = typeof(TController);
        var route = path;
        if (route.IsNullOrEmpty())
        {
            var name = controllerType.Name;
            if (name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)) name = name[..^10];
            route = "/" + name;
        }

        var routeKey = route.EnsureStart("/").EnsureEnd("/*");
        if (Routes.TryGetValue(routeKey, out var handler) && handler is ControllerHandler existing && existing.ControllerType == controllerType) return;

        MapController<TController>(route);
    }

    private void SetRoute(String path, IHttpHandler handler)
    {
        if (path.IsNullOrEmpty()) throw new ArgumentNullException(nameof(path));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        path = path.EnsureStart("/");
        Routes[path] = handler;
    }

    private readonly IDictionary<String, String> _pathCache = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

    /// <summary>匹配处理器</summary>
    /// <param name="path">请求路径</param>
    /// <param name="request">Http请求</param>
    /// <returns>处理器</returns>
    public IHttpHandler? MatchHandler(String path, HttpRequest? request)
    {
        if (path.IsNullOrEmpty()) return null;

        if (Routes.TryGetValue(path, out var handler)) return handler;

        if (_pathCache.TryGetValue(path, out var cached) && Routes.TryGetValue(cached, out handler)) return handler;

        foreach (var item in Routes)
        {
            var key = item.Key;
            if (!key.Contains('*')) continue;
            if (!key.IsMatch(path)) continue;

            if (Routes.TryGetValue(key, out handler))
            {
                if (handler is StaticFilesHandler || path.Split('/').Length <= 3) _pathCache[path] = key;
                return handler;
            }
        }

        return null;
    }
}