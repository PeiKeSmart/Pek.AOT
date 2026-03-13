using Pek.Data;

namespace Pek.Net;

/// <summary>帧数据传输接口</summary>
public interface ITransport : IDisposable
{
    /// <summary>超时</summary>
    Int32 Timeout { get; set; }

    /// <summary>打开</summary>
    /// <returns>是否成功</returns>
    Boolean Open();

    /// <summary>关闭</summary>
    /// <returns>是否成功</returns>
    Boolean Close();

    /// <summary>写入数据</summary>
    /// <param name="data">数据包</param>
    /// <returns>是否成功</returns>
    Boolean Send(IPacket data);

    /// <summary>异步发送并等待响应</summary>
    /// <param name="data">数据包</param>
    /// <returns>响应数据包</returns>
    Task<IPacket?> SendAsync(IPacket? data);

    /// <summary>同步接收数据</summary>
    /// <returns>响应数据包</returns>
    IPacket? Receive();

    /// <summary>数据到达事件</summary>
    event EventHandler<ReceivedEventArgs>? Received;
}