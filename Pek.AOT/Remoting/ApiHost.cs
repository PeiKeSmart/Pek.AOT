using Pek;
using Pek.Collections;
using Pek.Data;
using Pek.Log;
using Pek.Model;
using Pek.Net.Handlers;

namespace Pek.Remoting;

/// <summary>Api主机</summary>
#pragma warning disable CS0618
public abstract class ApiHost : DisposeBase, IApiHost, IExtend3, ILogFeature
#pragma warning restore CS0618
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = String.Empty;

    /// <summary>编码器</summary>
    public IEncoder Encoder { get; set; } = new JsonEncoder();

    /// <summary>调用超时时间。请求发出后，等待响应的最大时间，默认15_000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>慢追踪。远程调用或处理时间超过该值时，输出慢调用日志，默认5000ms</summary>
    public Int32 SlowTrace { get; set; } = 5_000;

    /// <summary>用户会话数据</summary>
    public IDictionary<String, Object?> Items { get; set; } = new NullableDictionary<String, Object?>();

    /// <summary>获取/设置用户会话数据</summary>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public virtual Object? this[String key] { get => Items[key]; set => Items[key] = value; }

    /// <summary>启动时间</summary>
    public DateTime StartTime { get; set; } = DateTime.Now;
    #endregion

    #region 方法
    /// <summary>获取消息编码器。重载以指定不同的封包协议</summary>
    /// <returns>消息编码器</returns>
#pragma warning disable CS0618
    public virtual IHandler GetMessageCodec() => new HandlerAdapter(new StandardCodec { Timeout = Timeout, UserPacket = false });
#pragma warning restore CS0618
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>编码器日志</summary>
    public ILog EncoderLog { get; set; } = Logger.Null;

    /// <summary>显示调用和处理错误。默认false</summary>
    public Boolean ShowError { get; set; }

    /// <summary>写日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object[] args) => Log?.Info(Name + " " + format, args);

    /// <summary>已重载。返回具有本类特征的字符串</summary>
    /// <returns>文本说明</returns>
    public override String ToString() => Name;
    #endregion

    #region 辅助
#pragma warning disable CS0618
    private sealed class HandlerAdapter(IPipelineHandler inner) : IHandler
#pragma warning restore CS0618
    {
        public IPipelineHandler? Prev
        {
            get => inner.Prev;
            set => inner.Prev = value;
        }

        public IPipelineHandler? Next
        {
            get => inner.Next;
            set => inner.Next = value;
        }

        public Object? Read(IHandlerContext context, Object message) => inner.Read(context, message);

        public Object? Write(IHandlerContext context, Object message) => inner.Write(context, message);

        public Boolean Open(IHandlerContext context) => inner.Open(context);

        public Boolean Close(IHandlerContext context, String reason) => inner.Close(context, reason);

        public Boolean Error(IHandlerContext context, Exception exception) => inner.Error(context, exception);
    }
    #endregion
}