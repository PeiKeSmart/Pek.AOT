namespace Pek.Configuration;

/// <summary>配置提供者</summary>
public interface IConfigProvider
{
    /// <summary>名称</summary>
    String Name { get; set; }

    /// <summary>根元素</summary>
    IConfigSection Root { get; set; }

    /// <summary>所有键</summary>
    ICollection<String> Keys { get; }

    /// <summary>是否新的配置文件</summary>
    Boolean IsNew { get; set; }

    /// <summary>获取或设置配置值</summary>
    /// <param name="key">配置名，支持冒号分隔的多级名称</param>
    /// <returns>找到时返回配置值；未找到返回 null</returns>
    String? this[String key] { get; set; }

    /// <summary>查找配置项</summary>
    /// <param name="key">配置名，支持冒号分隔的多级名称</param>
    /// <returns>匹配的配置节；未找到时返回 null</returns>
    IConfigSection? GetSection(String key);

    /// <summary>配置改变事件</summary>
    event EventHandler? Changed;

    /// <summary>返回获取配置的委托</summary>
    GetConfigCallback GetConfig { get; }

    /// <summary>从数据源加载数据到配置树</summary>
    /// <returns>true 表示加载成功</returns>
    Boolean LoadAll();

    /// <summary>保存配置树到数据源</summary>
    /// <returns>true 表示保存成功</returns>
    Boolean SaveAll();

    /// <summary>加载配置到模型</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="path">路径</param>
    /// <returns>模型实例</returns>
    T? Load<T>(String? path = null) where T : new();

    /// <summary>保存模型实例</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="path">路径</param>
    /// <returns>true 表示保存成功</returns>
    Boolean Save<T>(T model, String? path = null);

    /// <summary>绑定模型</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="autoReload">是否自动更新</param>
    /// <param name="path">路径</param>
    void Bind<T>(T model, Boolean autoReload = true, String? path = null);

    /// <summary>绑定模型并指定变更回调</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="path">路径</param>
    /// <param name="onChange">配置改变时执行的委托</param>
    void Bind<T>(T model, String path, Action<IConfigSection> onChange);
}

/// <summary>配置提供者基类</summary>
public abstract class ConfigProvider : DisposeBase, IConfigProvider
{
    private readonly Object _syncRoot = new();
    private Boolean _loaded;

    /// <summary>名称</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>根元素</summary>
    public virtual IConfigSection Root { get; set; } = new ConfigSection { Childs = [] };

    /// <summary>所有键</summary>
    public virtual ICollection<String> Keys
    {
        get
        {
            EnsureLoad();

            var childs = Root.Childs;
            if (childs == null || childs.Count == 0) return [];

            var list = new List<String>(childs.Count);
            foreach (var item in childs)
            {
                if (!String.IsNullOrEmpty(item.Key)) list.Add(item.Key);
            }

            return list;
        }
    }

    /// <summary>是否新的配置文件</summary>
    public Boolean IsNew { get; set; }

    /// <summary>返回获取配置的委托</summary>
    public virtual GetConfigCallback GetConfig => key => this[key];

    /// <summary>配置改变事件</summary>
    public event EventHandler? Changed;

    /// <summary>实例化</summary>
    protected ConfigProvider() => Name = GetType().Name;

    /// <summary>获取或设置配置值</summary>
    /// <param name="key">键</param>
    public virtual String? this[String key]
    {
        get
        {
            EnsureLoad();
            return Root.Find(key, false)?.Value;
        }
        set
        {
            EnsureLoad();

            var section = Root.Find(key, true);
            if (section != null) section.Value = value;
        }
    }

    /// <summary>查找配置项</summary>
    /// <param name="key">键</param>
    /// <returns>配置节</returns>
    public virtual IConfigSection? GetSection(String key)
    {
        EnsureLoad();
        return Root.Find(key, false);
    }

    /// <summary>从数据源加载数据到配置树</summary>
    /// <returns>true 表示加载成功</returns>
    public virtual Boolean LoadAll() => true;

    /// <summary>保存配置树到数据源</summary>
    /// <returns>true 表示保存成功</returns>
    public virtual Boolean SaveAll()
    {
        NotifyChange();
        return true;
    }

    /// <summary>加载配置到模型</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="path">路径</param>
    /// <returns>模型实例</returns>
    public virtual T? Load<T>(String? path = null) where T : new() => throw new NotSupportedException("Pek.AOT 当前未在 IConfigProvider 上提供对象映射，请继续使用 ConfigManager/Config<T> 链路。");

    /// <summary>保存模型实例</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="path">路径</param>
    /// <returns>true 表示保存成功</returns>
    public virtual Boolean Save<T>(T model, String? path = null) => throw new NotSupportedException("Pek.AOT 当前未在 IConfigProvider 上提供对象映射，请继续使用 ConfigManager/Config<T> 链路。");

    /// <summary>绑定模型</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="autoReload">是否自动更新</param>
    /// <param name="path">路径</param>
    public virtual void Bind<T>(T model, Boolean autoReload = true, String? path = null) => throw new NotSupportedException("Pek.AOT 当前未在 IConfigProvider 上提供对象绑定，请继续使用 ConfigManager/Config<T> 链路。");

    /// <summary>绑定模型并指定变更回调</summary>
    /// <typeparam name="T">模型类型</typeparam>
    /// <param name="model">模型实例</param>
    /// <param name="path">路径</param>
    /// <param name="onChange">配置改变时执行的委托</param>
    public virtual void Bind<T>(T model, String path, Action<IConfigSection> onChange) => throw new NotSupportedException("Pek.AOT 当前未在 IConfigProvider 上提供对象绑定，请继续使用 ConfigManager/Config<T> 链路。");

    /// <summary>触发配置改变事件</summary>
    protected void NotifyChange() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>确保已加载</summary>
    protected void EnsureLoad()
    {
        if (_loaded) return;

        lock (_syncRoot)
        {
            if (_loaded) return;

            _loaded = LoadAll();
        }
    }
}