using System.Diagnostics.Tracing;

namespace Pek.Log;

/// <summary>日志事件监听器。用于监听 EventSource 并写入日志</summary>
public class LogEventListener : EventListener
{
    private const String LogScope = "Pek.Log";
    private readonly HashSet<String> _sources = [];
    private readonly HashSet<String> _knownSources = [];

    /// <summary>实例化</summary>
    /// <param name="sources">要监听的事件源名称集合</param>
    public LogEventListener(String[] sources)
    {
        foreach (var item in sources)
        {
            if (!String.IsNullOrWhiteSpace(item)) _sources.Add(item);
        }
    }

    /// <summary>创建事件源时决定是否订阅</summary>
    /// <param name="eventSource">事件源</param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (_sources.Contains(eventSource.Name))
        {
            var log = XTrace.Log;
            var level = log.Level switch
            {
                LogLevel.All => EventLevel.LogAlways,
                LogLevel.Debug => EventLevel.Verbose,
                LogLevel.Info => EventLevel.Informational,
                LogLevel.Warn => EventLevel.Warning,
                LogLevel.Error => EventLevel.Error,
                LogLevel.Fatal => EventLevel.Critical,
                _ => EventLevel.Informational,
            };

            EnableEvents(eventSource, level);
            return;
        }

        if (_knownSources.Add(eventSource.Name)) XTrace.WriteScope(LogScope, nameof(LogEventListener), "Source={0}", eventSource.Name);
    }

    /// <summary>写入事件</summary>
    /// <param name="eventData">事件数据</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        var log = XTrace.Log;
        var level = eventData.Level switch
        {
            EventLevel.LogAlways => LogLevel.All,
            EventLevel.Critical => LogLevel.Fatal,
            EventLevel.Error => LogLevel.Error,
            EventLevel.Warning => LogLevel.Warn,
            EventLevel.Verbose => LogLevel.Debug,
            _ => LogLevel.Info,
        };

        var sourceName = eventData.EventSource?.Name ?? String.Empty;
        XTrace.WriteScope(LogScope, nameof(LogEventListener), "#{0} ThreadID = {1} ID = {2} Name = {3}", sourceName, eventData.OSThreadId, eventData.EventId, eventData.EventName);

        var payload = eventData.Payload;
        var names = eventData.PayloadNames;
        if (payload != null && names != null)
        {
            for (var i = 0; i < payload.Count && i < names.Count; i++)
            {
                XTrace.WriteScope(LogScope, nameof(LogEventListener), "\tName = \"{0}\" Value = \"{1}\"", names[i], payload[i]);
            }
        }

        if (!String.IsNullOrWhiteSpace(eventData.Message)) log.Write(level, XXTrace.FormatScope(LogScope, nameof(LogEventListener), eventData.Message));
    }
}