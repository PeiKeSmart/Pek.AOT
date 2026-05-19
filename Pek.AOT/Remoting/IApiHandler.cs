using Pek.Data;
using Pek.Messaging;

namespace Pek.Remoting;

/// <summary>Api处理器</summary>
public interface IApiHandler
{
    /// <summary>执行</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <returns>执行结果</returns>
    Object? Execute(IApiSession session, String action, IPacket? args, IMessage msg);
}