using System.Diagnostics.CodeAnalysis;

namespace Pek.Remoting;

/// <summary>接口管理器</summary>
public interface IApiManager
{
    /// <summary>可提供服务的方法</summary>
    IDictionary<String, ApiAction> Services { get; }

    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors)] TService>();

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    [RequiresUnreferencedCode("Registering arbitrary controller instances relies on runtime method discovery. Prefer Register<TService>().")]
    void Register(Object controller, String? method);

    /// <summary>查找服务</summary>
    /// <param name="action">动作名称</param>
    /// <returns>Api动作</returns>
    ApiAction? Find(String action);
}