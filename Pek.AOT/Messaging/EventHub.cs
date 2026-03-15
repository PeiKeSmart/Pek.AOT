using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Pek.Data;
using Pek.Log;
using Pek.Serialization;

namespace Pek.Messaging;

/// <summary>事件枢纽。按主题把网络消息分发到事件总线或回调</summary>
/// <typeparam name="TEvent">事件类型</typeparam>
public class EventHub<TEvent> : IEventHandler<IPacket>, IEventHandler<String>, ILogFeature, ITracerFeature
{
    private readonly ConcurrentDictionary<String, IEventBus<TEvent>> _eventBuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<String, IEventHandler<TEvent>> _dispatchers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Byte[] _eventPrefix = Encoding.ASCII.GetBytes("event#");
    private static readonly Char[] _eventPrefix2 = "event#".ToCharArray();

    /// <summary>事件总线工厂</summary>
    public IEventBusFactory? Factory { get; set; }

    /// <summary>Json主机</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    /// <summary>添加事件总线到指定主题</summary>
    public void Add(String topic, IEventBus<TEvent> bus)
    {
        if (String.IsNullOrWhiteSpace(topic)) throw new ArgumentNullException(nameof(topic));
        if (bus == null) throw new ArgumentNullException(nameof(bus));

        _eventBuses[topic] = bus;
    }

    /// <summary>按主题注册事件分发器</summary>
    public void Add(String topic, IEventHandler<TEvent> dispatcher)
    {
        if (String.IsNullOrWhiteSpace(topic)) throw new ArgumentNullException(nameof(topic));
        if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));

        _dispatchers[topic] = dispatcher;
    }

    /// <summary>获取指定主题的事件总线</summary>
    public IEventBus<TEvent> GetEventBus(String topic, String clientId = "")
    {
        if (_eventBuses.TryGetValue(topic, out var bus)) return bus;

        using var span = Tracer?.NewSpan($"event:{topic}:Create", new { clientId });
        WriteLog("注册主题：{0}，客户端：{1}", topic, clientId);
        bus = Factory?.CreateEventBus<TEvent>(topic, clientId) ?? new EventBus<TEvent>();

        return _eventBuses.GetOrAdd(topic, bus);
    }

    /// <summary>尝试获取分发器</summary>
    public Boolean TryGetValue(String topic, [MaybeNullWhen(false)] out IEventHandler<TEvent> action) => _dispatchers.TryGetValue(topic, out action);

    /// <summary>尝试获取事件总线</summary>
    public Boolean TryGetBus<T>(String topic, [MaybeNullWhen(false)] out IEventBus<T> eventBus)
    {
        if (_eventBuses.TryGetValue(topic, out var bus) && bus is IEventBus<T> bus2)
        {
            eventBus = bus2;
            return true;
        }

        if (_dispatchers.TryGetValue(topic, out var action))
        {
            eventBus = action as IEventBus<T>;
            if (eventBus != null) return true;
        }

        eventBus = default;
        return false;
    }

    /// <summary>处理接收到的数据包消息</summary>
    public virtual async Task<Int32> HandleAsync(IPacket data, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        var header = data.GetSpan();
        if (!header.StartsWith(_eventPrefix)) return 0;

        var p = header.IndexOf((Byte)'#');
        var header2 = header[(p + 1)..];
        var p2 = header2.IndexOf((Byte)'#');
        if (p2 <= 0) return 0;

        var topic = Encoding.UTF8.GetString(header2[..p2]);
        var header3 = header2[(p2 + 1)..];
        var p3 = header3.IndexOf((Byte)'#');
        if (p3 <= 0) return 0;

        var clientId = Encoding.UTF8.GetString(header3[..p3]);
        var headerCount = p + 1 + p2 + 1 + p3 + 1;
        using var span = Tracer?.NewSpan($"event:{topic}:Dispatch", new { clientId });

        var msg = data.Slice(headerCount);
        if (msg.Length == 0) return 0;

        if (context is IExtend ext) ext["Raw"] = data;

        if (msg[0] != '{' && msg.Total < 32)
        {
            if (await DispatchActionAsync(topic, clientId, msg.ToStr(), context, cancellationToken).ConfigureAwait(false)) return 1;
        }

        if (msg is TEvent @event) return await DispatchAsync(topic, clientId, @event, context, cancellationToken).ConfigureAwait(false);

        var msg2 = msg.ToStr();
        if (span is DefaultSpan defaultSpan1) defaultSpan1.AppendTag(msg2);
        if (msg2 is TEvent @event2) return await DispatchAsync(topic, clientId, @event2, context, cancellationToken).ConfigureAwait(false);

        return await OnDispatchAsync(topic, clientId, msg2, context, cancellationToken).ConfigureAwait(false);
    }

    Task IEventHandler<IPacket>.HandleAsync(IPacket @event, IEventContext? context, CancellationToken cancellationToken) => HandleAsync(@event, context, cancellationToken);

    /// <summary>处理接收到的字符串消息</summary>
    public virtual async Task<Int32> HandleAsync(String data, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        var header = data.AsSpan();
        if (!header.StartsWith(_eventPrefix2)) return 0;

        var p = header.IndexOf('#');
        var header2 = header[(p + 1)..];
        var p2 = header2.IndexOf('#');
        if (p2 <= 0) return 0;

        var topic = header2[..p2].ToString();
        var header3 = header2[(p2 + 1)..];
        var p3 = header3.IndexOf('#');
        if (p3 <= 0) return 0;

        var clientId = header3[..p3].ToString();
        var headerCount = p + 1 + p2 + 1 + p3 + 1;
        using var span = Tracer?.NewSpan($"event:{topic}:Dispatch", new { clientId });

        var msg = data[headerCount..];
        if (msg.Length == 0) return 0;

        if (context is IExtend ext) ext["Raw"] = data;

        if (msg[0] != '{' && msg.Length < 32)
        {
            if (await DispatchActionAsync(topic, clientId, msg, context, cancellationToken).ConfigureAwait(false)) return 1;
        }

        if (span is DefaultSpan defaultSpan2) defaultSpan2.AppendTag(msg);
        if (msg is TEvent @event) return await DispatchAsync(topic, clientId, @event, context, cancellationToken).ConfigureAwait(false);

        return await OnDispatchAsync(topic, clientId, msg, context, cancellationToken).ConfigureAwait(false);
    }

    Task IEventHandler<String>.HandleAsync(String @event, IEventContext? context, CancellationToken cancellationToken) => HandleAsync(@event, context, cancellationToken);

    /// <summary>处理动作指令</summary>
    public virtual Task<Boolean> DispatchActionAsync(String topic, String clientId, String action, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(action) || action[0] == '{') return Task.FromResult(false);

        using var span = Tracer?.NewSpan($"event:{topic}:{action}", new { clientId });
        switch (action)
        {
            case "subscribe":
                if ((context as IExtend)?["Handler"] is not IEventHandler<TEvent> handler)
                    throw new ArgumentNullException(nameof(context), "订阅动作时，必须在上下文中指定事件处理器");

                var bus = GetEventBus(topic, clientId);
                WriteLog("订阅主题：{0}，客户端：{1}", topic, clientId);
                bus.Subscribe(handler, clientId);
                return Task.FromResult(true);
            case "unsubscribe":
                WriteLog("取消订阅主题：{0}，客户端：{1}", topic, clientId);
                if (!TryGetBus<TEvent>(topic, out var bus2)) return Task.FromResult(false);

                bus2.Unsubscribe(clientId);
                if (bus2 is EventBus<TEvent> eventBus && eventBus.Handlers.Count == 0)
                {
                    _eventBuses.TryRemove(topic, out _);
                    _dispatchers.TryRemove(topic, out _);
                    WriteLog("注销主题：{0}，因订阅为空", topic);
                }
                return Task.FromResult(true);
            default:
                return Task.FromResult(false);
        }
    }

    /// <summary>分发字符串事件</summary>
    protected virtual Task<Int32> OnDispatchAsync(String topic, String clientId, String msg, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        var @event = JsonHost.Read<TEvent>(msg)!;
        if (@event is ITraceMessage traceMessage && DefaultSpan.Current is DefaultSpan span)
            span.Detach(traceMessage.TraceId);

        return DispatchAsync(topic, clientId, @event, context, cancellationToken);
    }

    /// <summary>分发事件</summary>
    public virtual async Task<Int32> DispatchAsync(String topic, String clientId, TEvent @event, IEventContext? context = null, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(topic)) throw new ArgumentNullException(nameof(topic));

        if (context is EventContext eventContext)
        {
            eventContext.Topic = topic;
            eventContext.ClientId = clientId;
        }
        else if (context is IExtend ext)
        {
            ext["Topic"] = topic;
            ext["ClientId"] = clientId;
        }

        if (_eventBuses.TryGetValue(topic, out var bus))
            return await bus.PublishAsync(@event, context, cancellationToken).ConfigureAwait(false);
        if (_dispatchers.TryGetValue(topic, out var action))
        {
            await action.HandleAsync(@event, context, cancellationToken).ConfigureAwait(false);
            return 1;
        }

        return 0;
    }

    /// <summary>写日志</summary>
    public void WriteLog(String format, params Object[] args)
    {
        var span = DefaultSpan.Current as DefaultSpan;
        span?.AppendTag(String.Format(format, args));
        Log?.Info(format, args);
    }
}