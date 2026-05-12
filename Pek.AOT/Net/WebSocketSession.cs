using System.Net;
using System.Security.Cryptography;
using System.Text;

using Pek.Data;
using Pek.Extension;
using Pek.Http;

namespace Pek.Net;

/// <summary>WebSocket会话</summary>
public class WebSocketSession : NetSession
{
    private static readonly Byte[] _prefix = "GET /".GetBytes();
    private Boolean _handshake;

    /// <summary>收到客户端发来的数据</summary>
    /// <param name="e">接收事件参数</param>
    protected override void OnReceive(ReceivedEventArgs e)
    {
        var packet = e.Packet;
        if (packet == null) return;

        if (!_handshake)
        {
            if (TryHandshake(packet))
            {
                _handshake = true;
                return;
            }

            return;
        }

        using var message = new WebSocketMessage();
        if (!message.Read(packet)) return;

        switch (message.Type)
        {
            case WebSocketMessageType.Close:
                SendControl(WebSocketMessageType.Close);
                if (Session is SessionBase sessionBase)
                    sessionBase.Close("WebSocketClose");
                else
                    Session.Dispose();
                return;
            case WebSocketMessageType.Ping:
                SendControl(WebSocketMessageType.Pong, message.Payload);
                return;
            case WebSocketMessageType.Pong:
                return;
        }

        e.Packet = message.Payload;
        message.Payload = null;

        base.OnReceive(e);
    }

    /// <summary>发送数据包</summary>
    /// <param name="data">数据包</param>
    /// <returns>当前会话</returns>
    public override INetSession Send(IPacket data)
    {
        if (!_handshake) return base.Send(data);

        var packet = new WebSocketMessage { Type = WebSocketMessageType.Binary, Payload = data }.ToPacket();
        try
        {
            Session.Send(packet);
        }
        finally
        {
            packet.TryDispose();
        }

        return this;
    }

    /// <summary>发送字节数组</summary>
    /// <param name="data">数据</param>
    /// <param name="offset">偏移</param>
    /// <param name="count">长度</param>
    /// <returns>当前会话</returns>
    public override INetSession Send(Byte[] data, Int32 offset = 0, Int32 count = -1)
    {
        if (!_handshake) return base.Send(data, offset, count);

        if (count < 0) count = data.Length - offset;
        var packet = new WebSocketMessage { Type = WebSocketMessageType.Binary, Payload = new ArrayPacket(data, offset, count) }.ToPacket();
        try
        {
            Session.Send(packet);
        }
        finally
        {
            packet.TryDispose();
        }

        return this;
    }

    /// <summary>发送只读内存段</summary>
    /// <param name="data">数据</param>
    /// <returns>当前会话</returns>
    public override INetSession Send(ReadOnlySpan<Byte> data)
    {
        if (!_handshake) return base.Send(data);

        var packet = new WebSocketMessage { Type = WebSocketMessageType.Binary, Payload = new ArrayPacket(data.ToArray()) }.ToPacket();
        try
        {
            Session.Send(packet);
        }
        finally
        {
            packet.TryDispose();
        }

        return this;
    }

    /// <summary>发送字符串</summary>
    /// <param name="msg">消息</param>
    /// <param name="encoding">编码</param>
    /// <returns>当前会话</returns>
    public override INetSession Send(String msg, Encoding? encoding = null)
    {
        if (!_handshake) return base.Send(msg, encoding);

        encoding ??= Encoding.UTF8;
        var packet = new WebSocketMessage { Type = WebSocketMessageType.Text, Payload = new ArrayPacket(encoding.GetBytes(msg)) }.ToPacket();
        try
        {
            Session.Send(packet);
        }
        finally
        {
            packet.TryDispose();
        }

        return this;
    }

    private Boolean TryHandshake(IPacket packet)
    {
        if (packet.Total < _prefix.Length) return false;

        var prefix = packet.ReadBytes(0, _prefix.Length);
        if (!prefix.AsSpan().StartsWith(_prefix)) return false;

        using var request = new HttpRequest();
        if (!request.Parse(packet)) return false;
        if (!request.Method.EqualIgnoreCase("GET")) return false;
        if (!request.Headers.TryGetValue("Sec-WebSocket-Key", out var key) || key.IsNullOrEmpty()) return false;

        var accept = Convert.ToBase64String(SHA1.HashData((key.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").GetBytes()));
        using var response = new HttpResponse
        {
            StatusCode = HttpStatusCode.SwitchingProtocols,
            StatusDescription = "Switching Protocols",
        };
        response.Headers["Upgrade"] = "websocket";
        response.Headers["Connection"] = "Upgrade";
        response.Headers["Sec-WebSocket-Accept"] = accept;

        using var result = response.Build();
        Session.Send(result);
        return true;
    }

    private void SendControl(WebSocketMessageType type, IPacket? payload = null)
    {
        var packet = new WebSocketMessage { Type = type, Payload = payload }.ToPacket();
        try
        {
            Session.Send(packet);
        }
        finally
        {
            packet.TryDispose();
        }
    }
}