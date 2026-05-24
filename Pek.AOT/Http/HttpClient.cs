using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography;

using Pek.Data;
using Pek.Extension;
using Pek.Net;

namespace Pek.Http;

/// <summary>Http客户端</summary>
public class HttpClient : TcpSession
{
    #region 属性
    /// <summary>是否WebSocket</summary>
    public Boolean IsWebSocket { get; set; }

    /// <summary>是否启用SSL</summary>
    public Boolean IsSSL { get; set; }

    /// <summary>请求</summary>
    public HttpRequest Request { get; set; } = new();

    /// <summary>响应</summary>
    public HttpResponse? Response { get; set; }

    private Boolean _wsHandshake;
    #endregion

    #region 构造
    /// <summary>实例化增强TCP</summary>
    public HttpClient()
    {
        Name = GetType().Name;
        Remote.Port = 80;
    }
    #endregion

    #region 生命周期
    /// <summary>打开连接</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    protected override async Task<Boolean> OnOpenAsync(CancellationToken cancellationToken)
    {
        if (!Active && Remote.Port == 0) Remote.Port = 80;

        var requestUri = Request.RequestUri;
        if (requestUri != null)
        {
            if (Remote.Address.IsAny()) Remote = new NetUri(requestUri.ToString());

            if (requestUri.Scheme.EqualIgnoreCase("https", "wss") || IsSSL)
                SslProtocol = SslProtocols.Tls12;
        }

        return await base.OnOpenAsync(cancellationToken).ConfigureAwait(false);
    }
    #endregion

    #region 收发数据
    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>发送字节数</returns>
    public new Int32 Send(IPacket data)
    {
        if (IsWebSocket)
        {
            EnsureWebSocketHandshake();
            return base.Send(BuildWebSocketPacket(data));
        }

        return base.Send(BuildHttpPacket(data));
    }

    /// <summary>发送字节数组</summary>
    /// <param name="data">字节数组</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">字节数</param>
    /// <returns>发送字节数</returns>
    public new Int32 Send(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        if (count < 0) count = data.Length - offset;
        return Send(new ArrayPacket(data, offset, count));
    }

    /// <summary>发送数组段</summary>
    /// <param name="data">数组段</param>
    /// <returns>发送字节数</returns>
    public new Int32 Send(ArraySegment<Byte> data) => Send(new ArrayPacket(data.Array ?? [], data.Offset, data.Count));

    /// <summary>发送只读数据块</summary>
    /// <param name="data">数据</param>
    /// <returns>发送字节数</returns>
    public new Int32 Send(ReadOnlySpan<Byte> data) => Send(new ArrayPacket(data.ToArray()));

    /// <summary>异步发送并等待下一帧响应</summary>
    /// <param name="data">请求数据</param>
    /// <returns>响应数据包</returns>
    public override async Task<IPacket?> SendAsync(IPacket? data)
    {
        if (IsWebSocket)
        {
            await EnsureWebSocketHandshakeAsync().ConfigureAwait(false);

            var responsePacket = await base.SendAsync(BuildWebSocketPacket(data)).ConfigureAwait(false);
            if (responsePacket == null || responsePacket.Total == 0) return responsePacket;

            var message = new WebSocketMessage();
            if (!message.Read(responsePacket)) return responsePacket;

            return message.Payload;
        }

        var packet = await base.SendAsync(BuildHttpPacket(data)).ConfigureAwait(false);
        if (packet == null || packet.Total == 0) return packet;

        var response = new HttpResponse();
        if (!response.Parse(packet)) return packet;

        Response = response;
        return response.Body ?? packet;
    }
    #endregion

    #region 辅助
    private IOwnerPacket BuildHttpPacket(IPacket? data)
    {
        var originalBody = Request.Body;
        if (data != null) Request.Body = data;

        try
        {
            return Request.Build();
        }
        finally
        {
            if (data != null) Request.Body = originalBody;
        }
    }

    private IPacket BuildWebSocketPacket(IPacket? data)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Binary,
            Payload = data ?? new ArrayPacket([]),
        };

        return message.ToPacket();
    }

    private void EnsureWebSocketHandshake() => EnsureWebSocketHandshakeAsync().ConfigureAwait(false).GetAwaiter().GetResult();

    private async Task EnsureWebSocketHandshakeAsync()
    {
        if (_wsHandshake) return;

        var requestUri = Request.RequestUri ?? throw new InvalidOperationException("WebSocket 请求缺少 RequestUri。");
        var request = new HttpRequest
        {
            Method = "GET",
            RequestUri = requestUri,
            KeepAlive = Request.KeepAlive,
            Version = Request.Version,
        };

        foreach (var item in Request.Headers)
        {
            request.Headers[item.Key] = item.Value;
        }

        request.Headers["Connection"] = "Upgrade";
        request.Headers["Upgrade"] = "websocket";
        request.Headers["Sec-WebSocket-Version"] = "13";

        Span<Byte> keyBuffer = stackalloc Byte[16];
        RandomNumberGenerator.Fill(keyBuffer);
        var key = Convert.ToBase64String(keyBuffer);
        request.Headers["Sec-WebSocket-Key"] = key;

        var responsePacket = await base.SendAsync(request.Build()).ConfigureAwait(false);
        if (responsePacket == null || responsePacket.Total == 0) throw new Exception("WebSocket握手失败！");

        var response = new HttpResponse();
        if (!response.Parse(responsePacket)) throw new Exception("WebSocket握手失败！响应头不完整。");
        if (response.StatusCode != HttpStatusCode.SwitchingProtocols)
            throw new Exception("WebSocket握手失败！" + response.StatusDescription);

        var expect = Convert.ToBase64String(SHA1.HashData((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").GetBytes()));
        if (!response.Headers.TryGetValue("Sec-WebSocket-Accept", out var accept) || !accept.EqualIgnoreCase(expect))
            throw new Exception("WebSocket握手失败！");

        Response = response;
        _wsHandshake = true;
    }
    #endregion
}