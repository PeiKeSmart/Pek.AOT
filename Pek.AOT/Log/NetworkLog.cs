using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using BclHttpClient = System.Net.Http.HttpClient;

using Pek.Http;
using Pek.Net;

namespace Pek.Log;

/// <summary>网络日志</summary>
public class NetworkLog : Logger, IDisposable
{
    private readonly ConcurrentQueue<String> _logs = new();
    private volatile Int32 _logCount;
    private Int32 _writing;
    private NetClient? _client;
    private BclHttpClient? _httpClient;
    private Boolean _inited;

    /// <summary>服务端</summary>
    public String? Server { get; set; }

    /// <summary>应用标识</summary>
    public String? AppId { get; set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>实例化网络日志。默认广播到 514 端口</summary>
    public NetworkLog() => Server = new NetUri(NetType.Udp, IPAddress.Broadcast, 514) + String.Empty;

    /// <summary>指定日志服务器地址来实例化网络日志</summary>
    /// <param name="server">服务地址</param>
    public NetworkLog(String server) => Server = server;

    /// <summary>销毁</summary>
    public void Dispose()
    {
        if (_logCount > 0)
        {
            if (Interlocked.CompareExchange(ref _writing, 1, 0) == 0)
                PushLog();
            else
                Thread.Sleep(500);
        }

        _client?.Dispose();
        _httpClient?.Dispose();
    }

    /// <summary>写日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        if (_logCount > 1024) return;

        var item = WriteLogEventArgs.Current.Set(level);
        if (args.Length == 1 && args[0] is Exception ex && (String.IsNullOrEmpty(format) || format == "{0}"))
            item.Set(null, ex);
        else
            item.Set(Format(format, args), null);

        _logs.Enqueue(item.GetAndReset());
        Interlocked.Increment(ref _logCount);

        if (Interlocked.CompareExchange(ref _writing, 1, 0) != 0) return;

        ThreadPool.UnsafeQueueUserWorkItem(_ =>
        {
            try
            {
                PushLog();
            }
            catch
            {
            }
            finally
            {
                _writing = 0;
            }
        }, null);
    }

    private void Init()
    {
        if (_inited) return;

        if (String.IsNullOrWhiteSpace(AppId))
            AppId = Assembly.GetEntryAssembly()?.GetName().Name ?? AppDomain.CurrentDomain.FriendlyName;
        if (String.IsNullOrWhiteSpace(ClientId))
            ClientId = Runtime.ClientId;

        if (String.IsNullOrWhiteSpace(Server)) return;
        var uri = new NetUri(Server);
        switch (uri.Type)
        {
            case NetType.Tcp:
            case NetType.Udp:
            case NetType.WebSocket:
                _client = new NetClient(uri);
                break;
            case NetType.Http:
            case NetType.Https:
                if (!Uri.TryCreate(Server, UriKind.Absolute, out var httpUri)) return;

                var handler = HttpHelper.CreateHandler(false, false);
                _httpClient = new BclHttpClient(handler) { BaseAddress = httpUri };
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-AppId", AppId);
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-ClientId", ClientId);
                _httpClient.SetUserAgent();
                break;
        }

        if (_client == null && _httpClient == null) return;

        Send(GetHead());
        _inited = true;
    }

    private void PushLog()
    {
        Init();
        if (_client == null && _httpClient == null) return;

        var max = _httpClient != null ? 8192 : 1460;
        var builder = new StringBuilder();
        while (_logs.TryDequeue(out var message))
        {
            Interlocked.Decrement(ref _logCount);
            if (builder.Length > 0 && builder.Length + message.Length >= max)
            {
                Send(builder.ToString());
                builder.Clear();
            }

            if (builder.Length > 0) builder.AppendLine();
            builder.Append(message);
        }

        if (builder.Length > 0) Send(builder.ToString());
    }

    private void Send(String value)
    {
        if (String.IsNullOrEmpty(value)) return;

        if (_client != null)
        {
            _client.Send(value);
            return;
        }

        if (_httpClient != null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, String.Empty)
            {
                Content = new StringContent(value, Encoding.UTF8, "text/plain")
            };
            _httpClient.SendAsync(request).Wait(30_000);
        }
    }
}