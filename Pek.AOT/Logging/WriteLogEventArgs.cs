namespace Pek.Logging;

/// <summary>写日志事件参数</summary>
public class WriteLogEventArgs : EventArgs
{
    [ThreadStatic]
    private static WriteLogEventArgs? _current;

    [ThreadStatic]
    private static String? _currentThreadName;

    private static String[]? _cachedLines;
    private static String? _cachedFormat;

    /// <summary>线程局部实例</summary>
    public static WriteLogEventArgs Current => _current ??= new WriteLogEventArgs();

    /// <summary>当前线程日志名</summary>
    public static String? CurrentThreadName
    {
        get => _currentThreadName;
        set => _currentThreadName = value;
    }

    /// <summary>日志等级</summary>
    public LogLevel Level { get; set; }

    /// <summary>日志消息</summary>
    public String? Message { get; set; }

    /// <summary>异常</summary>
    public Exception? Exception { get; set; }

    /// <summary>日志时间</summary>
    public DateTime Time { get; set; }

    /// <summary>线程编号</summary>
    public Int32 ThreadId { get; set; }

    /// <summary>是否线程池线程</summary>
    public Boolean IsPool { get; set; }

    /// <summary>线程名称</summary>
    public String? ThreadName { get; set; }

    /// <summary>任务编号</summary>
    public Int32 TaskId { get; set; }

    /// <summary>设置等级</summary>
    /// <param name="level">日志等级</param>
    /// <returns>当前对象</returns>
    public WriteLogEventArgs Set(LogLevel level)
    {
        Level = level;
        return this;
    }

    /// <summary>设置消息与异常</summary>
    /// <param name="message">日志消息</param>
    /// <param name="exception">异常对象</param>
    /// <returns>当前对象</returns>
    public WriteLogEventArgs Set(String? message, Exception? exception)
    {
        Message = message;
        Exception = exception;
        Init();
        return this;
    }

    /// <summary>获取文本并重置</summary>
    /// <returns>日志文本</returns>
    public String GetAndReset()
    {
        var value = ToString();
        Reset();
        return value;
    }

    /// <summary>重置对象状态</summary>
    public void Reset()
    {
        Level = LogLevel.Info;
        Message = null;
        Exception = null;
        Time = default;
        ThreadId = 0;
        IsPool = false;
        ThreadName = null;
        TaskId = 0;
    }

    /// <summary>转为文本</summary>
    /// <returns>日志文本</returns>
    public override String ToString()
    {
        var message = Exception == null ? Message : (Message ?? String.Empty) + Exception;
        var name = ResolveName();
        var fields = GetFields();
        var parts = new List<String>(fields.Length);
        foreach (var field in fields)
        {
            switch (field)
            {
                case "Time":
                    parts.Add(Time.ToString("HH:mm:ss.fff"));
                    break;
                case "ThreadId":
                    parts.Add(ThreadId.ToString("00"));
                    break;
                case "Kind":
                    parts.Add(IsPool ? "Y" : "N");
                    break;
                case "Name":
                    parts.Add(name);
                    break;
                case "Level":
                    parts.Add($"[{Level}]");
                    break;
                case "Message":
                    parts.Add(message ?? String.Empty);
                    break;
            }
        }

        return String.Join(' ', parts.Where(e => !String.IsNullOrEmpty(e)));
    }

    private void Init()
    {
        var setting = XXTraceSetting.Current;
        Time = DateTime.Now.AddHours(setting.UtcIntervalHours);
        var thread = Thread.CurrentThread;
        ThreadId = thread.ManagedThreadId;
        IsPool = thread.IsThreadPoolThread;
        ThreadName = CurrentThreadName ?? thread.Name;
        TaskId = Task.CurrentId ?? -1;
    }

    private String ResolveName()
    {
        if (String.IsNullOrWhiteSpace(ThreadName)) return TaskId >= 0 ? TaskId.ToString() : "-";

        if (ThreadName.StartsWith(".NET TP", StringComparison.OrdinalIgnoreCase) ||
            ThreadName.StartsWith("Thread Pool", StringComparison.OrdinalIgnoreCase) ||
            ThreadName.StartsWith(".NET ThreadPool", StringComparison.OrdinalIgnoreCase))
            return TaskId >= 0 ? TaskId.ToString() : "P";

        return ThreadName;
    }

    private static String[] GetFields()
    {
        var format = XXTraceSetting.Current.LogLineFormat;
        if (String.IsNullOrWhiteSpace(format)) format = "Time|ThreadId|Kind|Name|Message";

        if (!String.Equals(format, _cachedFormat, StringComparison.Ordinal))
        {
            _cachedFormat = format;
            _cachedLines = format.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return _cachedLines ?? ["Time", "ThreadId", "Kind", "Name", "Message"];
    }
}
