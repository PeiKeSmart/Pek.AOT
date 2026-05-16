using System.Diagnostics.CodeAnalysis;

namespace Pek.Http;

/// <summary>Http控制器静态注册约定</summary>
/// <typeparam name="TController">控制器类型</typeparam>
public interface IHttpControllerRegistration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TController> where TController : class
{
    /// <summary>注册控制器Actions</summary>
    /// <param name="server">Http服务器</param>
    /// <param name="path">控制器路径</param>
    static abstract void MapActions(HttpServer server, String? path = null);
}