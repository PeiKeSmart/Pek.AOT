using System.Net;

using Pek.Collections;
using Pek.Data;

namespace Pek.Net;

/// <summary>收到数据时的事件参数</summary>
public class ReceivedEventArgs : EventArgs, IData
{
    private static readonly Pool<ReceivedEventArgs> _pool = new();

    /// <summary>本地地址</summary>
    public IPAddress? Local { get; set; }

    /// <summary>原始数据包</summary>
    public IPacket? Packet { get; set; }

    /// <summary>远程地址</summary>
    public IPEndPoint? Remote { get; set; }

    /// <summary>解码后的消息</summary>
    public Object? Message { get; set; }

    /// <summary>用户自定义数据</summary>
    public Object? UserState { get; set; }

    /// <summary>从池中借出事件参数</summary>
    /// <returns>事件参数实例</returns>
    public static ReceivedEventArgs Rent() => _pool.Get();

    /// <summary>归还事件参数</summary>
    /// <param name="value">事件参数实例</param>
    public static void Return(ReceivedEventArgs? value)
    {
        if (value == null) return;

        value.Reset();
        _pool.Return(value);
    }

    /// <summary>获取当前事件的原始数据</summary>
    /// <returns>字节数组副本</returns>
    public Byte[]? GetBytes() => Packet?.ToArray();

    /// <summary>重置状态</summary>
    public void Reset()
    {
        Local = null;
        Packet = null;
        Remote = null;
        Message = null;
        UserState = null;
    }
}