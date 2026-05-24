using Pek.Collections;
using Pek.Log;
using Pek.Net;

namespace Pek.Remoting;

/// <summary>客户端连接池负载均衡集群</summary>
public class ClientPoolCluster : ICluster<String, ISocketClient>, ILogFeature
{
    private Int32 _index = -1;

    /// <summary>最后使用资源</summary>
    public KeyValuePair<String, ISocketClient> Current { get; private set; }

    /// <summary>服务器地址列表</summary>
    public Func<IEnumerable<String>> GetItems { get; set; } = static () => [];

    /// <summary>创建回调</summary>
    public Func<String, ISocketClient>? OnCreate { get; set; }

    /// <summary>连接池</summary>
    public IPool<ISocketClient> Pool { get; }

    /// <summary>实例化连接池集群</summary>
    public ClientPoolCluster() => Pool = new ClientPool(this);

    /// <summary>打开</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean Open() => true;

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Close(String reason) => Pool.Clear() > 0;

    /// <summary>从集群中获取资源</summary>
    /// <returns>客户端连接</returns>
    public virtual ISocketClient Get() => Pool.Get();

    /// <summary>归还资源</summary>
    /// <param name="value">客户端连接</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Put(ISocketClient value)
    {
        if (value == null) return false;

        return Pool.Return(value);
    }

    /// <summary>为连接池创建连接</summary>
    /// <returns>客户端连接</returns>
    protected virtual ISocketClient CreateClient()
    {
        var servers = GetItems()?.ToArray();
        if (servers == null || servers.Length == 0) throw new InvalidOperationException("没有设置服务端地址Servers");

        var index = Interlocked.Increment(ref _index);
        Exception? lastException = null;
        for (var i = 0; i < servers.Length; i++)
        {
            var currentIndex = (index + i) % servers.Length;
            var server = servers[currentIndex];
            try
            {
                WriteLog("集群均衡：{0}", server);

                var factory = OnCreate ?? throw new InvalidOperationException("未设置客户端创建回调 OnCreate");
                var client = factory(server);
                client.Open();

                Current = new KeyValuePair<String, ISocketClient>(server, client);
                return client;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        throw lastException ?? new InvalidOperationException("没有可用的服务端地址");
    }

    private sealed class ClientPool : ObjectPool<ISocketClient>
    {
        private readonly ClientPoolCluster _host;

        public ClientPool(ClientPoolCluster host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Min = 0;
            Max = 100_000;
        }

        protected override ISocketClient? OnCreate() => _host.CreateClient();

        protected override Boolean OnReturn(ISocketClient value) => value != null && !value.Disposed;

        protected override void OnDispose(ISocketClient? value)
        {
            if (value == null) return;

            try
            {
                if (value.Active) value.Close("ClientPool.Dispose");
            }
            catch { }

            base.OnDispose(value);
        }

    }

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}