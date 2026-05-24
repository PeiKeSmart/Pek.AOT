using Pek;
using Pek.Log;
using NewLife.Reflection;

namespace Pek.Model;

/// <summary>通用插件接口</summary>
/// <remarks>
/// 为了方便构建一个简单通用的插件系统，先规定如下：
/// 1，负责加载插件的宿主，在加载插件后会进行插件实例化，此时可在插件构造函数中做一些事情，但不应该开始业务处理，因为宿主的准备工作可能尚未完成。
/// 2，宿主一切准备就绪后，会顺序调用插件的 Init 方法，并将宿主标识传入，插件通过标识区分是否自己的目标宿主。
/// 3，如果插件实现了 IDisposable 接口，宿主最后会清理资源。
/// </remarks>
public interface IPlugin
{
    /// <summary>初始化</summary>
    /// <param name="identity">插件宿主标识</param>
    /// <param name="provider">服务提供者</param>
    /// <returns>初始化是否成功</returns>
    Boolean Init(String? identity, IServiceProvider provider);
}

/// <summary>插件特性。用于判断插件实现类是否支持某个宿主</summary>
/// <param name="identity">宿主标识</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class PluginAttribute(String identity) : Attribute
{
    /// <summary>插件宿主标识</summary>
    public String Identity { get; set; } = identity;
}

/// <summary>插件管理器</summary>
public class PluginManager : DisposeBase, IServiceProvider
{
    #region 属性
    /// <summary>宿主标识，用于供插件区分不同宿主</summary>
    public String? Identity { get; set; }

    /// <summary>宿主服务提供者</summary>
    public IServiceProvider? Provider { get; set; }

    /// <summary>插件集合</summary>
    public IPlugin[]? Plugins { get; set; }

    /// <summary>日志提供者</summary>
    public ILog Log { get; set; } = XTrace.Log;
    #endregion

    #region 构造
    /// <summary>实例化一个插件管理器</summary>
    public PluginManager() { }

    /// <summary>销毁资源</summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (!disposing) return;

        var plugins = Plugins;
        if (plugins == null) return;

        for (var i = plugins.Length - 1; i >= 0; i--)
        {
            plugins[i].TryDispose();
        }

        Plugins = null;
    }
    #endregion

    #region 方法
    /// <summary>加载插件。仅保留属于当前宿主且实例化成功的插件</summary>
    public void Load()
    {
        List<IPlugin> list = [];
        foreach (var type in LoadPlugins())
        {
            if (type == null) continue;

            try
            {
                var obj = Provider?.GetService(type);
                if (obj is IPlugin plugin) list.Add(plugin);
            }
            catch (Exception ex)
            {
                Log?.Debug(String.Empty, ex);
            }
        }

        Plugins = [.. list];
    }

    /// <summary>加载插件类型。仅保留属于当前宿主的插件</summary>
    /// <returns>插件类型序列</returns>
    public IEnumerable<Type> LoadPlugins()
    {
        foreach (var type in AssemblyX.FindAllPlugins(typeof(IPlugin), true))
        {
            if (type == null) continue;

            var attributes = type.GetCustomAttributes(typeof(PluginAttribute), true).Cast<PluginAttribute>().ToArray();
            if (attributes.Length > 0 && attributes.All(item => item.Identity != Identity)) continue;

            yield return type;
        }
    }

    /// <summary>初始化插件。仅保留初始化成功的插件</summary>
    public void Init()
    {
        var plugins = Plugins;
        if (plugins == null || plugins.Length == 0) return;

        List<IPlugin> list = [];
        foreach (var item in plugins)
        {
            try
            {
                if (item.Init(Identity, this)) list.Add(item);
            }
            catch (Exception ex)
            {
                Log?.Debug(String.Empty, ex);
            }
        }

        Plugins = [.. list];
    }
    #endregion

    #region IServiceProvider
    Object? IServiceProvider.GetService(Type serviceType)
    {
        if (serviceType == typeof(PluginManager)) return this;

        return Provider?.GetService(serviceType);
    }
    #endregion
}