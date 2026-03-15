namespace Pek.Log;

/// <summary>控制台日志</summary>
public class ConsoleLog : Logger
{
    private static readonly Object _lock = new();

    /// <summary>是否使用颜色</summary>
    public Boolean UseColor { get; set; } = true;

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        var item = WriteLogEventArgs.Current.Set(level);
        if (args.Length == 1 && args[0] is Exception ex && format == "{0}")
            item.Set(null, ex);
        else
            item.Set(Format(format, args), null);

        lock (_lock)
        {
            var previous = Console.ForegroundColor;
            if (UseColor) Console.ForegroundColor = GetColor(level);
            Console.WriteLine(item.GetAndReset());
            if (UseColor) Console.ForegroundColor = previous;
        }
    }

    private static ConsoleColor GetColor(LogLevel level) => level switch
    {
        LogLevel.Debug => ConsoleColor.DarkGray,
        LogLevel.Warn => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Fatal => ConsoleColor.DarkRed,
        _ => ConsoleColor.Gray,
    };
}
