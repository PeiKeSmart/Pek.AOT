namespace NewLife.Log;

/// <summary>日志基类</summary>
public abstract class Logger : ILog
{
    /// <summary>调试日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public virtual void Debug(String format, params Object?[] args) => Write(LogLevel.Debug, format, args);

    /// <summary>信息日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public virtual void Info(String format, params Object?[] args) => Write(LogLevel.Info, format, args);

    /// <summary>警告日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public virtual void Warn(String format, params Object?[] args) => Write(LogLevel.Warn, format, args);

    /// <summary>错误日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public virtual void Error(String format, params Object?[] args) => Write(LogLevel.Error, format, args);

    /// <summary>严重错误日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public virtual void Fatal(String format, params Object?[] args) => Write(LogLevel.Fatal, format, args);

    /// <summary>写日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public virtual void Write(LogLevel level, String format, params Object?[] args)
    {
        if (!Enable || level < Level) return;

        OnWrite(level, format, args);
    }

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected abstract void OnWrite(LogLevel level, String format, params Object?[] args);

    /// <summary>格式化日志内容</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化后的字符串</returns>
    protected String Format(String format, Object?[]? args)
    {
        if (String.IsNullOrEmpty(format)) return String.Empty;
        if (args == null || args.Length == 0) return format;

        if (args.Length == 1 && args[0] is Exception ex && format == "{0}") return ex.ToString();

        return String.Format(format, args);
    }

    /// <summary>是否启用日志</summary>
    public virtual Boolean Enable { get; set; } = true;

    /// <summary>日志等级</summary>
    public virtual LogLevel Level { get; set; } = LogLevel.Info;

    /// <summary>空日志实现</summary>
    public static ILog Null { get; } = new NullLogger();

    private sealed class NullLogger : Logger
    {
        public override Boolean Enable { get => false; set { } }

        protected override void OnWrite(LogLevel level, String format, params Object?[] args) { }
    }
}
