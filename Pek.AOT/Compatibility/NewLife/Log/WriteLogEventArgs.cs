namespace NewLife.Log;

/// <summary>写日志事件参数</summary>
public class WriteLogEventArgs : EventArgs
{
    [ThreadStatic]
    private static String? _currentThreadName;

    /// <summary>日志等级</summary>
    public LogLevel Level { get; set; }

    /// <summary>日志消息</summary>
    public String? Message { get; set; }

    /// <summary>异常对象</summary>
    public Exception? Exception { get; set; }

    /// <summary>日志时间</summary>
    public DateTime Time { get; set; }

    /// <summary>线程编号</summary>
    public Int32 ThreadId { get; set; }

    /// <summary>线程名称</summary>
    public String? ThreadName { get; set; }

    /// <summary>当前线程日志名称</summary>
    public static String? CurrentThreadName
    {
        get => _currentThreadName;
        set => _currentThreadName = value;
    }

    /// <summary>设置等级</summary>
    /// <param name="level">日志等级</param>
    /// <returns>当前对象</returns>
    public WriteLogEventArgs Set(LogLevel level)
    {
        Level = level;
        return this;
    }

    /// <summary>设置内容</summary>
    /// <param name="message">日志消息</param>
    /// <param name="exception">异常对象</param>
    /// <returns>当前对象</returns>
    public WriteLogEventArgs Set(String? message, Exception? exception)
    {
        Message = message;
        Exception = exception;
        Time = DateTime.Now;
        ThreadId = Thread.CurrentThread.ManagedThreadId;
        ThreadName = CurrentThreadName ?? Thread.CurrentThread.Name;
        return this;
    }

    /// <summary>获取文本并重置</summary>
    /// <returns>日志文本</returns>
    public String GetAndReset()
    {
        var value = ToString();
        Message = null;
        Exception = null;
        Time = default;
        ThreadId = 0;
        ThreadName = null;
        return value;
    }

    /// <summary>转为日志文本</summary>
    /// <returns>日志文本</returns>
    public override String ToString()
    {
        var name = String.IsNullOrWhiteSpace(ThreadName) ? "-" : ThreadName;
        var message = Exception == null ? Message : (Message ?? String.Empty) + Exception;
        return $"{Time:HH:mm:ss.fff} {ThreadId:00} {name} [{Level}] {message}";
    }
}
