using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Pek;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Model;

namespace Pek.Net;

/// <summary>网络服务器。可同时支持多个Socket服务器，同时支持IPv4和IPv6，同时支持Tcp和Udp</summary>
public class NetServer : DisposeBase, IExtend, ILogFeature
{
    #region 属性
    /// <summary>服务名</summary>
    public String Name { get; set; }

    private NetUri _local = new();

    /// <summary>本地绑定地址</summary>
    public NetUri Local
    {
        get => _local;
        set
        {
            _local = value;
            if (AddressFamily <= AddressFamily.Unspecified && value.Host != "*")
                AddressFamily = value.Address.AddressFamily;
        }
    }

    /// <summary>监听端口</summary>
    public Int32 Port { get => _local.Port; set => _local.Port = value; }

    /// <summary>协议类型</summary>
    public NetType ProtocolType { get => _local.Type; set => _local.Type = value; }

    /// <summary>地址族</summary>
    public AddressFamily AddressFamily { get; set; }

    /// <summary>是否活动</summary>
    public Boolean Active { get; protected set; }

    /// <summary>会话超时时间（秒）</summary>
    public Int32 SessionTimeout { get; set; }

    /// <summary>消息管道</summary>
    public IPipeline? Pipeline { get; set; }

    /// <summary>是否使用会话集合</summary>
    public Boolean UseSession { get; set; } = true;

    /// <summary>地址重用</summary>
    public Boolean ReuseAddress { get; set; }

    /// <summary>SSL协议版本</summary>
    public SslProtocols SslProtocol { get; set; } = SslProtocols.None;

    /// <summary>X509证书</summary>
    public X509Certificate? Certificate { get; set; }

    /// <summary>APM性能追踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>Socket层性能追踪器</summary>
    public ITracer? SocketTracer { get; set; }

    /// <summary>是否输出发送日志</summary>
    public Boolean LogSend { get; set; }

    /// <summary>是否输出接收日志</summary>
    public Boolean LogReceive { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    private ConcurrentDictionary<String, Object?>? _items;

    /// <summary>扩展数据字典</summary>
    public IDictionary<String, Object?> Items => _items ??= new();

    /// <summary>获取/设置扩展数据</summary>
    /// <param name="key">数据键名</param>
    /// <returns>数据值，不存在时返回null</returns>
    public Object? this[String key] { get => _items != null && _items.TryGetValue(key, out var obj) ? obj : null; set => Items[key] = value; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;
    #endregion

    #region 构造
    /// <summary>实例化一个网络服务器</summary>
    public NetServer()
    {
        Name = GetType().Name.TrimEnd("Server");

        if (SocketSetting.Current.Debug) Log = XTrace.Log;
    }
    #endregion

    #region 方法
    /// <summary>为会话创建网络数据处理器</summary>
    /// <remarks>可作为业务处理实现，也可以作为前置协议解析。子类可重载返回自定义处理器</remarks>
    /// <param name="session">网络会话</param>
    /// <returns>处理器实例，默认返回null</returns>
    public virtual INetHandler? CreateHandler(INetSession session) => null;
    #endregion
}