using Pek.Collections;
using Pek.Log;
using Pek.Net;

namespace Pek.Remoting;

/// <summary>客户端单连接故障转移集群</summary>
public class ClientSingleCluster : ICluster<String, ISocketClient>, ILogFeature
{
    private readonly Object _syncRoot = new();
    private Int32 _index = -1;
    private ISocketClient? _client;

    /// <summary>最后使用资源</summary>
    public KeyValuePair<String, ISocketClient> Current { get; private set; }

    /// <summary>服务器地址列表</summary>
    public Func<IEnumerable<String>> GetItems { get; set; } = static () => [];

    /// <summary>创建回调</summary>
    public Func<String, ISocketClient>? OnCreate { get; set; }

    /// <summary>打开</summary>
    /// <returns>是否成功</returns>
    public virtual Boolean Open() => true;

    /// <summary>关闭</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Close(String reason)
    {
        var client = _client;
        if (client == null) return false;

        return client.Close(reason);
    }

    /// <summary>从集群中获取资源</summary>
    /// <returns>客户端连接</returns>
    public virtual ISocketClient Get()
    {
        var client = _client;
        if (client != null && client.Active && !client.Disposed) return client;

        lock (_syncRoot)
        {
            client = _client;
            if (client != null && client.Active && !client.Disposed) return client;

            client.TryDispose();
            client = CreateClient();
            _client = client;
            return client;
        }
    }

    /// <summary>归还资源</summary>
    /// <param name="value">客户端连接</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Put(ISocketClient value) => true;

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
                WriteLog("集群转移：{0}", server);

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

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}