using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Pek.Log;

/// <summary>网络日志</summary>
public class NetworkLog : Logger, IDisposable
{
    private readonly ConcurrentQueue<String> _logs = new();
    private volatile Int32 _logCount;
    private Int32 _writing;
    private HttpClient? _httpClient;
    private TcpClient? _tcpClient;
    private UdpClient? _udpClient;
    private IPEndPoint? _udpEndPoint;
    private Uri? _serverUri;
    private Boolean _inited;

    /// <summary>服务端</summary>
    public String? Server { get; set; }

    /// <summary>应用标识</summary>
    public String? AppId { get; set; }

    /// <summary>客户端标识</summary>
    public String? ClientId { get; set; }

    /// <summary>实例化网络日志。默认广播到 514 端口</summary>
    public NetworkLog() => Server = "udp://255.255.255.255:514";

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

        _httpClient?.Dispose();
        _tcpClient?.Dispose();
        _udpClient?.Dispose();
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

        AppId ??= Assembly.GetEntryAssembly()?.GetName().Name ?? AppDomain.CurrentDomain.FriendlyName;
        ClientId ??= Runtime.ClientId;

        if (String.IsNullOrWhiteSpace(Server)) return;
        if (!Uri.TryCreate(Server, UriKind.Absolute, out var uri)) return;

        _serverUri = uri;
        switch (uri.Scheme.ToLowerInvariant())
        {
            case "http":
            case "https":
                _httpClient = new HttpClient { BaseAddress = uri };
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-AppId", AppId);
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-ClientId", ClientId);
                break;
            case "tcp":
                _tcpClient = new TcpClient();
                _tcpClient.Connect(uri.Host, uri.Port > 0 ? uri.Port : 514);
                break;
            case "udp":
                var addresses = Dns.GetHostAddresses(uri.Host);
                var address = addresses.FirstOrDefault() ?? IPAddress.Broadcast;
                _udpEndPoint = new IPEndPoint(address, uri.Port > 0 ? uri.Port : 514);
                _udpClient = new UdpClient();
                if (IPAddress.Broadcast.Equals(address)) _udpClient.EnableBroadcast = true;
                break;
        }

        if (_httpClient == null && _tcpClient == null && _udpClient == null) return;

        Send(CreateHead());
        _inited = true;
    }

    private void PushLog()
    {
        Init();
        if (_httpClient == null && _tcpClient == null && _udpClient == null) return;

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

        if (_httpClient != null)
        {
            _httpClient.PostAsync(String.Empty, new StringContent(value, Encoding.UTF8, "text/plain")).GetAwaiter().GetResult();
            return;
        }

        var data = Encoding.UTF8.GetBytes(value);
        if (_udpClient != null && _udpEndPoint != null)
        {
            _udpClient.Send(data, data.Length, _udpEndPoint);
            return;
        }

        if (_tcpClient?.Connected == true)
        {
            var stream = _tcpClient.GetStream();
            stream.Write(data, 0, data.Length);
            stream.Flush();
            return;
        }

        if (_serverUri != null && _serverUri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase))
        {
            _tcpClient?.Dispose();
            _tcpClient = new TcpClient();
            _tcpClient.Connect(_serverUri.Host, _serverUri.Port > 0 ? _serverUri.Port : 514);
            var stream = _tcpClient.GetStream();
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
    }

    private static String CreateHead()
    {
        var process = Environment.ProcessId;
        var name = AppDomain.CurrentDomain.FriendlyName;
        var builder = new StringBuilder();
        builder.AppendFormat("#Software: {0}\r\n", name);
        builder.AppendFormat("#ProcessID: {0}{1}\r\n", process, Environment.Is64BitProcess ? " x64" : String.Empty);
        builder.AppendFormat("#BaseDirectory: {0}\r\n", AppDomain.CurrentDomain.BaseDirectory);
        builder.AppendFormat("#CurrentDirectory: {0}\r\n", Environment.CurrentDirectory);
        builder.AppendFormat("#ApplicationType: {0}\r\n", Runtime.IsWeb ? "Web" : Runtime.IsConsole ? "Console" : "Service");
        builder.AppendFormat("#CLR: {0}\r\n", Environment.Version);
        builder.AppendFormat("#OS: {0}, {1}/{2}\r\n", Environment.OSVersion, Environment.MachineName, Environment.UserName);
        builder.AppendFormat("#CPU: {0}\r\n", Environment.ProcessorCount);
        builder.AppendFormat("#Date: {0:yyyy-MM-dd}\r\n", DateTime.Now.AddHours(XTrace.GetSetting().UtcIntervalHours));
        return builder.ToString();
    }
}