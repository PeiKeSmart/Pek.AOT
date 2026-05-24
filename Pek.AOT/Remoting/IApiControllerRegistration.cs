using System.Diagnostics.CodeAnalysis;

namespace Pek.Remoting;

/// <summary>Api控制器静态注册约定</summary>
/// <typeparam name="TController">控制器类型</typeparam>
public interface IApiControllerRegistration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TController>
    where TController : class
{
    /// <summary>注册控制器Actions</summary>
    /// <param name="server">Api服务器</param>
    static abstract void MapActions(ApiServer server);
}