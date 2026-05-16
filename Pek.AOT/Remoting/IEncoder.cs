using System.IO;

using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Messaging;

namespace Pek.Remoting;

/// <summary>编码器</summary>
public interface IEncoder
{
    /// <summary>创建请求</summary>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <returns>请求消息</returns>
    IMessage CreateRequest(String action, Object? args);

    /// <summary>创建响应</summary>
    /// <param name="msg">请求消息</param>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">结果</param>
    /// <returns>响应消息</returns>
    IMessage CreateResponse(IMessage msg, String action, Int32 code, Object? value);

    /// <summary>解码请求或响应</summary>
    /// <param name="msg">消息</param>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">参数或结果</param>
    /// <returns>是否成功</returns>
    Boolean Decode(IMessage msg, out String action, out Int32 code, out IPacket? value);

    /// <summary>解码参数</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns>参数字典</returns>
    IDictionary<String, Object> DecodeParameters(String action, IPacket data, IMessage msg);

    /// <summary>解码结果</summary>
    /// <param name="action">动作</param>
    /// <param name="data">数据</param>
    /// <param name="msg">消息</param>
    /// <returns>结果对象</returns>
    Object? DecodeResult(String action, IPacket data, IMessage msg);

    /// <summary>转换为目标类型</summary>
    /// <param name="obj">源对象</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换结果</returns>
    Object? Convert(Object? obj, Type targetType);

    /// <summary>日志提供者</summary>
    ILog Log { get; set; }
}

/// <summary>编码器基类</summary>
public abstract class EncoderBase
{
    #region 方法
    /// <summary>解码请求或响应</summary>
    /// <param name="msg">消息</param>
    /// <param name="action">动作</param>
    /// <param name="code">错误码</param>
    /// <param name="value">参数或结果</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Decode(IMessage msg, out String action, out Int32 code, out IPacket? value)
    {
        if (msg == null) throw new ArgumentNullException(nameof(msg));

        code = 0;
        value = null;

        var payload = msg.Payload;
        if (payload == null) throw new InvalidOperationException("Payload is null.");

        using var stream = payload.GetStream();
        using var reader = new BinaryReader(stream);

        action = reader.ReadString();
        if (action.IsNullOrEmpty()) throw new InvalidOperationException("解码错误，无法找到服务名！");

        if (msg.Reply && msg.Error) code = reader.ReadInt32();

        if (stream.Length > stream.Position)
        {
            var length = reader.ReadInt32();
            if (length > 0) value = payload.Slice((Int32)stream.Position, length);
        }

        return true;
    }
    #endregion

    #region 日志
    /// <summary>日志提供者</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">参数</param>
    public virtual void WriteLog(String format, params Object?[] args) => Log.Info(format, args);
    #endregion
}