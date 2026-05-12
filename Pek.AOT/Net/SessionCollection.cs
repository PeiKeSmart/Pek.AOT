using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Pek.Threading;

namespace Pek.Net;

/// <summary>会话集合</summary>
internal class SessionCollection : DisposeBase, IDictionary<String, ISocketSession>
{
    private readonly ConcurrentDictionary<String, ISocketSession> _dic = new();

    /// <summary>服务端</summary>
    public ISocketServer Server { get; private set; }

    /// <summary>清理周期。默认10秒</summary>
    public Int32 ClearPeriod { get; set; } = 10;

    private TimerX? _clearTimer;

    /// <summary>实例化会话集合</summary>
    /// <param name="server">所属服务端</param>
    public SessionCollection(ISocketServer server) => Server = server;

    /// <summary>释放资源</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _clearTimer.TryDispose();
        _clearTimer = null;

        var reason = GetType().Name + (disposing ? "Dispose" : "GC");
        try
        {
            CloseAll(reason);
        }
        catch { }
    }

    /// <summary>添加新会话</summary>
    /// <param name="session">会话实例</param>
    /// <returns>是否成功</returns>
    public Boolean Add(ISocketSession session)
    {
        var key = session.Remote.EndPoint + String.Empty;
        if (!_dic.TryAdd(key, session)) return false;

        var period = ClearPeriod * 1000;
        _clearTimer ??= new TimerX(RemoveNotAlive, null, period, period) { Async = true };

        session.OnDisposed += (s, e) =>
        {
            if (s is ISocketSession socketSession)
                _dic.TryRemove(socketSession.Remote.EndPoint + String.Empty, out _);
        };

        return true;
    }

    /// <summary>获取会话</summary>
    /// <param name="key">远程地址端口标识</param>
    /// <returns>会话实例</returns>
    public ISocketSession? Get(String key)
    {
        if (!_dic.TryGetValue(key, out var session)) return null;

        return session;
    }

    /// <summary>关闭所有会话</summary>
    /// <param name="reason">关闭原因</param>
    public void CloseAll(String reason)
    {
        if (!_dic.Any()) return;

        foreach (var item in _dic.Values.ToArray())
        {
            if (item == null || item.Disposed) continue;

            if (item is INetSession netSession) netSession.Close(reason);

            item.TryDispose();
        }
    }

    private void RemoveNotAlive(Object? state)
    {
        if (!_dic.Any()) return;

        var timeout = Server?.SessionTimeout ?? 30;
        var keys = new List<String>();
        var values = new List<ISocketSession>();

        foreach (var item in _dic)
        {
            var session = item.Value;
            if (session == null || session.Disposed || timeout > 0 && IsNotAlive(session, timeout))
            {
                keys.Add(item.Key);
                values.Add(item.Value);
            }
        }

        foreach (var item in keys)
        {
            _dic.TryRemove(item, out _);
        }

        foreach (var item in values)
        {
            item.WriteLog("超过{0}秒不活跃销毁 {1}", timeout, item);

            if (item is ISocketClient socketClient) socketClient.Close(nameof(RemoveNotAlive));
            item.TryDispose();
        }
    }

    private static Boolean IsNotAlive(ISocketSession session, Int32 timeout) =>
        session.LastTime > DateTime.MinValue && session.LastTime.AddSeconds(timeout) < DateTime.Now;

    /// <summary>清空会话集合</summary>
    public void Clear() => _dic.Clear();

    /// <summary>会话数量</summary>
    public Int32 Count => _dic.Count;

    /// <summary>是否只读</summary>
    public Boolean IsReadOnly => false;

    /// <summary>获取枚举器</summary>
    /// <returns>枚举器</returns>
    public IEnumerator<ISocketSession> GetEnumerator() => _dic.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _dic.GetEnumerator();

    void IDictionary<String, ISocketSession>.Add(String key, ISocketSession value) => Add(value);

    Boolean IDictionary<String, ISocketSession>.ContainsKey(String key) => _dic.ContainsKey(key);

    ICollection<String> IDictionary<String, ISocketSession>.Keys => _dic.Keys;

    Boolean IDictionary<String, ISocketSession>.Remove(String key)
    {
        if (!_dic.TryRemove(key, out var session)) return false;

        if (session is INetSession netSession) netSession.Close("Remove");
        session.Dispose();

        return true;
    }

#if NETFRAMEWORK || NETSTANDARD
    Boolean IDictionary<String, ISocketSession>.TryGetValue(String key, out ISocketSession value) => _dic.TryGetValue(key, out value);
#else
    Boolean IDictionary<String, ISocketSession>.TryGetValue(String key, [MaybeNullWhen(false)] out ISocketSession value) => _dic.TryGetValue(key, out value);
#endif

    ICollection<ISocketSession> IDictionary<String, ISocketSession>.Values => _dic.Values;

    ISocketSession IDictionary<String, ISocketSession>.this[String key] { get => _dic[key]; set => _dic[key] = value; }

    void ICollection<KeyValuePair<String, ISocketSession>>.Add(KeyValuePair<String, ISocketSession> item) => throw new XException("不支持！请使用Add(ISocketSession session)方法！");

    Boolean ICollection<KeyValuePair<String, ISocketSession>>.Contains(KeyValuePair<String, ISocketSession> item) => _dic.ContainsKey(item.Key);

    void ICollection<KeyValuePair<String, ISocketSession>>.CopyTo(KeyValuePair<String, ISocketSession>[] array, Int32 arrayIndex) =>
        ((ICollection<KeyValuePair<String, ISocketSession>>)_dic).CopyTo(array, arrayIndex);

    Boolean ICollection<KeyValuePair<String, ISocketSession>>.Remove(KeyValuePair<String, ISocketSession> item) => throw new XException("不支持！请直接销毁会话对象！");

    IEnumerator<KeyValuePair<String, ISocketSession>> IEnumerable<KeyValuePair<String, ISocketSession>>.GetEnumerator() => _dic.GetEnumerator();
}