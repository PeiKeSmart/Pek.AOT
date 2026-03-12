namespace Pek.Logging;

/// <summary>复合日志</summary>
public class CompositeLog : Logger
{
    /// <summary>日志提供者集合</summary>
    public List<ILog> Logs { get; } = [];

    /// <summary>日志等级</summary>
    public override LogLevel Level
    {
        get => base.Level;
        set
        {
            base.Level = value;
            foreach (var item in Logs)
            {
                item.Level = value;
            }
        }
    }

    /// <summary>实例化</summary>
    public CompositeLog() { }

    /// <summary>实例化</summary>
    /// <param name="log">日志提供者</param>
    public CompositeLog(ILog log)
    {
        Add(log);
        Level = log.Level;
    }

    /// <summary>实例化</summary>
    /// <param name="log1">日志提供者1</param>
    /// <param name="log2">日志提供者2</param>
    public CompositeLog(ILog log1, ILog log2)
    {
        Add(log1);
        Add(log2);
        Level = log1.Level > log2.Level ? log2.Level : log1.Level;
    }

    /// <summary>添加日志提供者</summary>
    /// <param name="log">日志提供者</param>
    /// <returns>当前对象</returns>
    public CompositeLog Add(ILog log)
    {
        Logs.Add(log);
        return this;
    }

    /// <summary>获取指定类型的日志提供者</summary>
    /// <typeparam name="TLog">日志类型</typeparam>
    /// <returns>日志提供者</returns>
    public TLog? Get<TLog>() where TLog : class
    {
        foreach (var item in Logs)
        {
            if (item is TLog log) return log;
            if (item is CompositeLog composite)
            {
                var nested = composite.Get<TLog>();
                if (nested != null) return nested;
            }
        }

        return null;
    }

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        foreach (var item in Logs)
        {
            item.Write(level, format, args);
        }
    }
}
