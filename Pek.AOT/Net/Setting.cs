using System.ComponentModel;
using System.Text.Json.Serialization;

using Pek.Configuration;

namespace Pek.Net;

/// <summary>网络设置</summary>
[Obsolete("=>SocketSetting")]
public class Setting : SocketSetting { }

/// <summary>网络设置</summary>
[DisplayName("网络设置")]
[Config("Socket")]
public class SocketSetting : Config<SocketSetting, SocketSettingJsonContext>
{
    /// <summary>网络调试</summary>
    [Description("网络调试")]
    public Boolean Debug { get; set; }

    /// <summary>会话超时时间。每个Tcp/Udp连接会话，超过一定时间不活跃时做超时下线处理，默认20*60秒</summary>
    [Description("会话超时时间。每个Tcp/Udp连接会话，超过一定时间不活跃时做超时下线处理，默认20*60秒")]
    public Int32 SessionTimeout { get; set; } = 20 * 60;

    /// <summary>缓冲区大小。每个异步接收缓冲区的大小，默认8k</summary>
    [Description("缓冲区大小。每个异步接收缓冲区的大小，较大的值能减少小包合并，但是当连接数很多时会浪费大量内存，默认8k")]
    public Int32 BufferSize { get; set; } = 8 * 1024;

    /// <summary>收发日志数据体长度。应用于日志发送和接收时的数据 HEX 长度，默认64字节</summary>
    [Description("收发日志数据体长度。应用于日志发送和接收时的数据HEX长度，默认64字节")]
    public Int32 LogDataLength { get; set; } = 64;

    /// <summary>自动启用 GZip 压缩的请求体大小，默认1024，用0表示不压缩</summary>
    [Description("自动启用GZip压缩的请求体大小。应用于请求发送时，默认1024，用0表示不压缩")]
    public Int32 AutoGZip { get; set; } = 1024;
}

/// <summary>SocketSetting 的 AOT 序列化上下文</summary>
[JsonSerializable(typeof(SocketSetting))]
public partial class SocketSettingJsonContext : JsonSerializerContext
{
}