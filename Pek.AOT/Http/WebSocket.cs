using System.Net;
using System.Security.Cryptography;

using Pek.Data;
using Pek.Extension;
using Pek.Net;

namespace Pek.Http;

/// <summary>WebSocket消息处理</summary>
/// <param name="socket">WebSocket会话</param>
/// <param name="message">消息</param>
public delegate void WebSocketDelegate(WebSocket socket, WebSocketMessage message);

/// <summary>WebSocket会话管理</summary>
public class WebSocket
{
    /// <summary>是否还在连接</summary>
    public Boolean Connected { get; set; }

    /// <summary>消息处理器</summary>
    public WebSocketDelegate? Handler { get; set; }

    /// <summary>Http上下文</summary>
    public IHttpContext? Context { get; set; }

    /// <summary>版本</summary>
    public String? Version { get; set; }

    /// <summary>协议</summary>
    public String? Protocol { get; set; }

    /// <summary>活跃时间</summary>
    public DateTime ActiveTime { get; set; }

    /// <summary>执行WebSocket握手</summary>
    /// <param name="context">Http上下文</param>
    /// <returns>WebSocket会话</returns>
    public static WebSocket? Handshake(IHttpContext context)
    {
        var request = context.Request;
        if (!request.Headers.TryGetValue("Sec-WebSocket-Key", out var key) || key.IsNullOrEmpty()) return null;

        var manager = new WebSocket();
        manager.ProcessRequest(context);
        return manager;
    }

    /// <summary>处理WebSocket握手</summary>
    /// <param name="context">Http上下文</param>
    /// <returns>是否成功</returns>
    public Boolean ProcessRequest(IHttpContext context)
    {
        var request = context.Request;
        if (!request.Headers.TryGetValue("Sec-WebSocket-Key", out var key) || key.IsNullOrEmpty()) return false;

        var accept = Convert.ToBase64String(SHA1.HashData((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").GetBytes()));

        var response = context.Response;
        response.StatusCode = HttpStatusCode.SwitchingProtocols;
        response.Headers["Upgrade"] = "websocket";
        response.Headers["Connection"] = "Upgrade";
        response.Headers["Sec-WebSocket-Accept"] = accept;

        if (context is DefaultHttpContext defaultHttpContext) defaultHttpContext.WebSocket = this;

        if (!Protocol.IsNullOrEmpty()) response.Headers["Sec-WebSocket-Protocol"] = Protocol;
        if (!Version.IsNullOrEmpty()) response.Headers["Sec-WebSocket-Version"] = Version;

        Context = context;
        Connected = true;
        ActiveTime = DateTime.Now;
        return true;
    }

    /// <summary>处理数据包</summary>
    /// <param name="packet">数据包</param>
    public void Process(IPacket packet)
    {
        using var message = new WebSocketMessage();
        if (!message.Read(packet)) return;

        Process(message);
    }

    /// <summary>处理消息</summary>
    /// <param name="message">消息</param>
    public void Process(WebSocketMessage message)
    {
        ActiveTime = DateTime.Now;

        Handler?.Invoke(this, message);
        message.Payload?.TryDispose();

        var session = Context?.Connection;
        var socket = Context?.Socket;
        if (session == null && socket == null) return;

        switch (message.Type)
        {
            case WebSocketMessageType.Close:
                Close(1000, "Finished");
                session?.Dispose();
                socket?.Dispose();
                Connected = false;
                break;
            case WebSocketMessageType.Ping:
                var pong = new WebSocketMessage
                {
                    Type = WebSocketMessageType.Pong,
                    Payload = (ArrayPacket)$"Pong {DateTime.UtcNow.ToFullString()}",
                };
                Send(pong);
                break;
        }
    }

    private void Send(WebSocketMessage message)
    {
        var session = Context?.Connection;
        var socket = Context?.Socket;
        if (session == null && socket == null) throw new ObjectDisposedException(nameof(Context));

        var packet = message.ToPacket();
        try
        {
            if (session != null)
                session.Send(packet);
            else
                socket?.Send(packet);
        }
        finally
        {
            packet.TryDispose();
        }
    }

    /// <summary>发送消息</summary>
    /// <param name="data">数据</param>
    /// <param name="type">消息类型</param>
    public void Send(IPacket data, WebSocketMessageType type)
    {
        var message = new WebSocketMessage { Type = type, Payload = data };
        Send(message);
    }

    /// <summary>发送消息</summary>
    /// <param name="data">数据</param>
    /// <param name="type">消息类型</param>
    public void Send(Byte[] data, WebSocketMessageType type)
    {
        var message = new WebSocketMessage { Type = type, Payload = (ArrayPacket)data };
        Send(message);
    }

    /// <summary>发送文本消息</summary>
    /// <param name="message">文本</param>
    public void Send(String message) => Send(message.GetBytes(), WebSocketMessageType.Text);

    /// <summary>向所有连接发送消息</summary>
    /// <param name="data">数据</param>
    /// <param name="type">类型</param>
    /// <param name="predicate">过滤器</param>
    public void SendAll(IPacket data, WebSocketMessageType type, Func<INetSession, Boolean>? predicate = null)
    {
        var session = Context?.Connection ?? throw new ObjectDisposedException(nameof(Context));
        var message = new WebSocketMessage { Type = type, Payload = data };
        var packet = message.ToPacket();
        try
        {
            session.Host.SendAllAsync(packet, predicate).Wait(30_000);
        }
        finally
        {
            packet.TryDispose();
            data.TryDispose();
        }
    }

    /// <summary>向所有连接发送文本消息</summary>
    /// <param name="message">文本</param>
    /// <param name="predicate">过滤器</param>
    public void SendAll(String message, Func<INetSession, Boolean>? predicate = null) => SendAll((ArrayPacket)message.GetBytes(), WebSocketMessageType.Text, predicate);

    /// <summary>发送关闭连接</summary>
    /// <param name="closeStatus">关闭状态</param>
    /// <param name="statusDescription">状态描述</param>
    public void Close(Int32 closeStatus, String statusDescription)
    {
        var message = new WebSocketMessage
        {
            Type = WebSocketMessageType.Close,
            CloseStatus = closeStatus,
            StatusDescription = statusDescription,
        };
        Send(message);
    }
}