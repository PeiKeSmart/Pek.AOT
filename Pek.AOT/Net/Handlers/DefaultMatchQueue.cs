using Pek.Data;
using Pek.Log;
using Pek.Threading;

namespace Pek.Net.Handlers;

/// <summary>消息匹配队列</summary>
public class DefaultMatchQueue : IMatchQueue
{
    private struct ItemWrap
    {
        public Item? Value;
    }

    private sealed class Item
    {
        public Object? Owner { get; set; }
        public Object? Request { get; set; }
        public Int64 EndTime { get; set; }
        public Object? Source { get; set; }
        public ISpan? Span { get; set; }
    }

    private readonly ItemWrap[] _items;
    private Int32 _count;
    private TimerX? _timer;
    private Int32 _cursor;

    /// <summary>按指定大小初始化队列</summary>
    /// <param name="size">队列大小</param>
    public DefaultMatchQueue(Int32 size = 256) => _items = new ItemWrap[size];

    /// <summary>加入请求队列</summary>
    /// <param name="owner">拥有者</param>
    /// <param name="request">请求消息</param>
    /// <param name="msTimeout">超时取消时间</param>
    /// <param name="source">任务源</param>
    public virtual void Add(Object? owner, Object request, Int32 msTimeout, Object source)
    {
        var now = Runtime.TickCount64;
        if (msTimeout <= 10) msTimeout = 15_000;

        var ext = owner as IExtend;
        var item = new Item
        {
            Owner = owner,
            Request = request,
            EndTime = now + msTimeout,
            Source = source,
            Span = ext?["Span"] as ISpan,
        };

        var items = _items;
        var length = items.Length;
        if (Volatile.Read(ref _count) >= length) Check(null);

        var start = Volatile.Read(ref _cursor);
        for (var offset = 0; offset < length; offset++)
        {
            var index = (start + offset) % length;
            if (Interlocked.CompareExchange(ref items[index].Value, item, null) != null) continue;

            Interlocked.Increment(ref _count);
            Volatile.Write(ref _cursor, (index + 1) % length);
            StartTimer();
            return;
        }

        Check(null);

        start = Volatile.Read(ref _cursor);
        for (var offset = 0; offset < length; offset++)
        {
            var index = (start + offset) % length;
            if (Interlocked.CompareExchange(ref items[index].Value, item, null) != null) continue;

            Interlocked.Increment(ref _count);
            Volatile.Write(ref _cursor, (index + 1) % length);
            StartTimer();
            return;
        }

        DefaultTracer.Instance?.NewError("net:MatchQueue:IsFull", new { items.Length });
        throw new XException("The matching queue is full [{0}]", items.Length);
    }

    private void StartTimer()
    {
        if (_timer != null) return;
        lock (this)
        {
            _timer ??= new TimerX(Check, null, 1000, 1000, "Match") { Async = true };
        }
    }

    /// <summary>检查请求队列是否有匹配该响应的请求</summary>
    /// <param name="owner">拥有者</param>
    /// <param name="response">响应消息</param>
    /// <param name="result">任务结果</param>
    /// <param name="callback">匹配回调</param>
    /// <returns>是否匹配成功</returns>
    public virtual Boolean Match(Object? owner, Object response, Object result, Func<Object?, Object?, Boolean> callback)
    {
        if (Volatile.Read(ref _count) <= 0) return false;

        var items = _items;
        var length = items.Length;
        var start = Volatile.Read(ref _cursor);

        for (var offset = 1; offset <= length; offset++)
        {
            var index = (start - offset + length) % length;
            var item = Volatile.Read(ref items[index].Value);
            if (item == null) continue;

            if (item.Owner != owner || !callback(item.Request, response)) continue;
            if (Interlocked.CompareExchange(ref items[index].Value, null, item) != item) continue;

            Interlocked.Decrement(ref _count);

            var source = item.Source;
            if (source != null) SetResult(source, result);

            return true;
        }

        if (SocketSetting.Current.Debug)
            XTrace.WriteLine("MatchQueue.Match 失败 [{0}] result={1} Items={2}", response, result, _count);

        return false;
    }

    private void Check(Object? state)
    {
        if (Volatile.Read(ref _count) <= 0) return;

        var now = Runtime.TickCount64;
        var items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            var item = Volatile.Read(ref items[i].Value);
            if (item == null || item.EndTime > now) continue;
            if (Interlocked.CompareExchange(ref items[i].Value, null, item) != item) continue;

            Interlocked.Decrement(ref _count);

            var source = item.Source;
            if (source != null) SetCanceled(source);
        }
    }

    /// <summary>清空队列</summary>
    public virtual void Clear()
    {
        var items = _items;
        for (var i = 0; i < items.Length; i++)
        {
            var item = Interlocked.Exchange(ref items[i].Value, null);
            if (item == null) continue;

            Interlocked.Decrement(ref _count);

            var source = item.Source;
            if (source != null) SetCanceled(source);
        }

        _count = 0;
    }

    private static void SetResult(Object source, Object result)
    {
        if (source is TaskCompletionSource<Object> taskSource && !taskSource.Task.IsCompleted)
            taskSource.TrySetResult(result);
        else if (source is Pek.Net.PooledValueTaskSource pooledSource && !pooledSource.IsCompleted)
            pooledSource.TrySetResult(result);
    }

    private static void SetCanceled(Object source)
    {
        if (source is TaskCompletionSource<Object> taskSource && !taskSource.Task.IsCompleted)
            taskSource.TrySetCanceled();
        else if (source is Pek.Net.PooledValueTaskSource pooledSource && !pooledSource.IsCompleted)
            pooledSource.TrySetCanceled();
    }
}