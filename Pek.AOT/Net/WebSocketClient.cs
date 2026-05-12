using System.Security.Cryptography;

using Pek.Data;
using Pek.Extension;
using Pek.Http;
using Pek.Log;
using Pek.Net.Handlers;
using Pek.Threading;

namespace Pek.Net;

/// <summary>WebSocket客户端</summary>
public class WebSocketClient : TcpSession
{
    #region 属性
    /// <summary>资源地址</summary>
    public Uri Uri { get; set; } = null!;

    /// <summary>WebSocket心跳间隔</summary>
    public TimeSpan KeepAlive { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>请求头</summary>
    public IDictionary<String, String?>? RequestHeaders { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public WebSocketClient()
    {
        this.Add<WebSocketCodec>();
    }

    /// <summary>实例化</summary>
    /// <param name="uri">资源地址</param>
    public WebSocketClient(Uri uri) : this()
    {
        Uri = uri;
        Remote = new NetUri(uri.ToString());
    }

    /// <summary>实例化</summary>
    /// <param name="url">资源地址</param>
    public WebSocketClient(String url) : this(new Uri(url)) { }
    #endregion

    #region 生命周期
    /// <summary>打开连接</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override async Task<Boolean> OnOpenAsync(CancellationToken cancellationToken)
    {
        var remote = Remote;
        if (remote == null || remote.Address.IsAny() || remote.Port == 0)
            remote = Remote = new NetUri(Uri.ToString());

        var result = await base.OnOpenAsync(cancellationToken).ConfigureAwait(false);
        if (!result) return false;

        var period = (Int32)KeepAlive.TotalMilliseconds;
        if (period > 0)
            _timer = new TimerX(DoPing, null, 5_000, period) { Async = true };

        return true;
    }

    /// <summary>关闭连接</summary>
    /// <param name="reason">关闭原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override Task<Boolean> OnCloseAsync(String reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;

        return base.OnCloseAsync(reason, cancellationToken);
    }

    /// <summary>设置请求头</summary>
    /// <param name="headerName">请求头名称</param>
    /// <param name="headerValue">请求头值</param>
    public void SetRequestHeader(String headerName, String? headerValue)
    {
        RequestHeaders ??= new Dictionary<String, String?>();
        RequestHeaders[headerName] = headerValue;
    }
    #endregion

    #region 消息收发
    /// <summary>接收WebSocket消息</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>WebSocket消息</returns>
    public virtual async Task<WebSocketMessage?> ReceiveMessageAsync(CancellationToken cancellationToken = default)
    {
        using var result = await base.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (result == null) return null;

        var message = new WebSocketMessage();
        if (!message.Read(result)) return null;

        return message;
    }

    /// <summary>发送消息</summary>
    /// <param name="message">消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SendMessageAsync(WebSocketMessage message, CancellationToken cancellationToken = default)
    {
        SendMessage(message);
        return Task.CompletedTask;
    }

    /// <summary>发送文本</summary>
    /// <param name="data">数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SendTextAsync(IPacket data, CancellationToken cancellationToken = default)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Text,
            Payload = data,
        };

        return SendMessageAsync(message, cancellationToken);
    }

    /// <summary>发送文本</summary>
    /// <param name="data">数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SendTextAsync(Byte[] data, CancellationToken cancellationToken = default)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Text,
            Payload = (ArrayPacket)data,
        };

        return SendMessageAsync(message, cancellationToken);
    }

    /// <summary>发送文本</summary>
    /// <param name="text">文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SendTextAsync(String text, CancellationToken cancellationToken = default) => SendTextAsync(text.GetBytes(), cancellationToken);

    /// <summary>发送二进制数据</summary>
    /// <param name="data">数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task SendBinaryAsync(IPacket data, CancellationToken cancellationToken = default)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Binary,
            Payload = data,
        };

        return SendMessageAsync(message, cancellationToken);
    }

    /// <summary>发送关闭消息</summary>
    /// <param name="closeStatus">关闭状态</param>
    /// <param name="statusDescription">状态描述</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task CloseAsync(Int32 closeStatus, String? statusDescription = null, CancellationToken cancellationToken = default)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Close,
            CloseStatus = closeStatus,
            StatusDescription = statusDescription,
        };

        return SendMessageAsync(message, cancellationToken);
    }
    #endregion

    #region 心跳
    private TimerX? _timer;

    private void DoPing(Object? state)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Ping,
            Payload = (ArrayPacket)$"Ping {DateTime.UtcNow.ToFullString()}",
        };

        SendMessage(message);

        var period = (Int32)KeepAlive.TotalMilliseconds;
        if (_timer != null) _timer.Period = period;
    }
    #endregion

    #region 辅助
    /// <summary>WebSocket握手</summary>
    /// <param name="client">客户端</param>
    /// <param name="uri">资源地址</param>
    /// <returns>是否成功</returns>
    public static Boolean Handshake(ISocketClient client, Uri uri)
    {
        var request = new HttpRequest
        {
            Method = "GET",
            RequestUri = uri,
        };

        if (client is WebSocketClient webSocketClient && webSocketClient.RequestHeaders != null)
        {
            foreach (var item in webSocketClient.RequestHeaders)
            {
                if (item.Value != null) request.Headers[item.Key] = item.Value;
            }
        }

        request.Headers["Connection"] = "Upgrade";
        request.Headers["Upgrade"] = "websocket";
        request.Headers["Sec-WebSocket-Version"] = "13";

        Span<Byte> keyBuffer = stackalloc Byte[16];
        RandomNumberGenerator.Fill(keyBuffer);
        var key = Convert.ToBase64String(keyBuffer);
        request.Headers["Sec-WebSocket-Key"] = key;

        DefaultSpan.Current?.Attach(request.Headers);

        using var span = client.Tracer?.NewSpan($"net:{client.Name}:WebSocket", uri + String.Empty);
        try
        {
            using (var req = request.Build())
            {
                client.Send(req);
            }

            using var responsePacket = client.Receive();
            if (responsePacket == null || responsePacket.Length == 0) return false;

            using var response = new HttpResponse();
            if (!response.Parse(responsePacket)) return false;
            if (response.StatusCode != System.Net.HttpStatusCode.SwitchingProtocols)
                throw new Exception("WebSocket握手失败！" + response.StatusDescription);

            var expect = Convert.ToBase64String(SHA1.HashData((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").GetBytes()));
            if (!response.Headers.TryGetValue("Sec-WebSocket-Accept", out var accept) || accept != expect)
                throw new Exception("WebSocket握手失败！");
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            client.WriteLog("WebSocket握手失败！" + ex.Message);

            client.Close("WebSocket");
            client.Dispose();
            return false;
        }

        return true;
    }
    #endregion
}