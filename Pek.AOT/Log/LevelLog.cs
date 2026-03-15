namespace Pek.Log;

/// <summary>按等级分文件输出日志</summary>
public class LevelLog : Logger
{
    private readonly Dictionary<LogLevel, ILog> _logs = [];

    /// <summary>实例化</summary>
    /// <param name="logPath">日志目录</param>
    /// <param name="fileFormat">文件格式</param>
    public LevelLog(String logPath, String fileFormat)
    {
        foreach (var item in Enum.GetValues<LogLevel>())
        {
            if (item is > LogLevel.All and < LogLevel.Off)
            {
                _logs[item] = new TextFileLog(logPath, false, fileFormat) { Level = item };
            }
        }
    }

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        if (_logs.TryGetValue(level, out var log)) log.Write(level, format, args);
    }
}
