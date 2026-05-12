using Pek.Data;
using Pek.Http;
using Pek.Model;

namespace Pek.Net.Handlers;

/// <summary>WebSocket消息编码器</summary>
public class WebSocketCodec : Handler
{
    /// <summary>是否向上传递用户数据包</summary>
    public Boolean UserPacket { get; set; }

    /// <summary>打开连接</summary>
    /// <param name="context">上下文</param>
    /// <returns>是否成功</returns>
    public override Boolean Open(IHandlerContext context)
    {
        if (context.Owner is ISocketClient client)
        {
            if (client.Remote.Type == NetType.WebSocket && client is WebSocketClient webSocketClient)
                WebSocketClient.Handshake(client, webSocketClient.Uri);
        }

        return base.Open(context);
    }

    /// <summary>关闭连接</summary>
    /// <param name="context">上下文</param>
    /// <param name="reason">关闭原因</param>
    /// <returns>是否成功</returns>
    public override Boolean Close(IHandlerContext context, String reason)
    {
        if (context.Owner is IExtend ext) ext["Codec"] = null;

        return base.Close(context, reason);
    }

    /// <summary>读取数据</summary>
    /// <param name="context">上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理结果</returns>
    public override Object? Read(IHandlerContext context, Object message)
    {
        if (message is IPacket packet)
        {
            var webSocketMessage = new WebSocketMessage();
            if (webSocketMessage.Read(packet))
            {
                if (UserPacket)
                {
                    message = webSocketMessage.Payload!;
                    webSocketMessage.Payload = null;
                }
                else
                    message = webSocketMessage;
            }
        }

        return base.Read(context, message);
    }

    /// <summary>写入数据</summary>
    /// <param name="context">上下文</param>
    /// <param name="message">消息</param>
    /// <returns>处理结果</returns>
    public override Object? Write(IHandlerContext context, Object message)
    {
        if (UserPacket && message is IPacket packet)
            message = new WebSocketMessage { Type = WebSocketMessageType.Binary, Payload = packet };

        IPacket? owner = null;
        if (message is WebSocketMessage webSocketMessage)
            message = owner = webSocketMessage.ToPacket();

        try
        {
            return base.Write(context, message);
        }
        finally
        {
            owner.TryDispose();
        }
    }
}