using Pek.Log;
using Pek.Model;

namespace Pek.Remoting;

/// <summary>Api主机</summary>
public interface IApiHost
{
    /// <summary>编码器</summary>
    IEncoder Encoder { get; set; }

    /// <summary>获取消息编码器。重载以指定不同的封包协议</summary>
    /// <returns>消息编码器</returns>
#pragma warning disable CS0618
    IHandler GetMessageCodec();
#pragma warning restore CS0618

    /// <summary>日志</summary>
    ILog Log { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">参数</param>
    void WriteLog(String format, params Object[] args);
}