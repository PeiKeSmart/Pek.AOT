using Pek.Data;
using Pek.Log;

namespace Pek.Http;

/// <summary>WebSocket处理器</summary>
public class WebSocketHandler : IHttpHandler
{
    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    public virtual void ProcessRequest(IHttpContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var webSocket = context.WebSocket;
        if (webSocket != null) webSocket.Handler = ProcessMessage;

        WriteLog("WebSocket连接 {0}", context.Connection?.Remote);
    }

    /// <summary>处理消息</summary>
    /// <param name="socket">WebSocket会话</param>
    /// <param name="message">消息</param>
    public virtual void ProcessMessage(WebSocket socket, WebSocketMessage message)
    {
        if (socket == null) throw new ArgumentNullException(nameof(socket));
        if (message == null) throw new ArgumentNullException(nameof(message));

        var remote = socket.Context?.Connection?.Remote ?? throw new ObjectDisposedException(nameof(socket.Context));
        var payload = message.Payload;

        switch (message.Type)
        {
            case WebSocketMessageType.Text:
                var text = payload?.ToStr();
                WriteLog("WebSocket收到[{0}] {1}", remote, text);
                socket.SendAll($"[{remote}]说，{text}");
                break;
            case WebSocketMessageType.Binary:
                WriteLog("WebSocket收到[{0}] {1} bytes", remote, payload?.Total ?? 0);
                break;
            case WebSocketMessageType.Close:
                WriteLog("WebSocket关闭[{0}] [{1}] {2}", remote, message.CloseStatus, message.StatusDescription);
                break;
            case WebSocketMessageType.Ping:
            case WebSocketMessageType.Pong:
                WriteLog("WebSocket心跳[{0}] {1}", message.Type, payload?.ToStr());
                break;
            default:
                WriteLog("WebSocket收到[{0}] {1} bytes", message.Type, payload?.Total ?? 0);
                break;
        }
    }

    private static void WriteLog(String format, params Object?[] args) => XTrace.WriteLine(format, args);
}