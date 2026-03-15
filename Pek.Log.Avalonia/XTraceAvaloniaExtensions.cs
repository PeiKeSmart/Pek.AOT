namespace Pek.Log.Avalonia;

/// <summary>XTrace Avalonia 扩展</summary>
public static class XTraceAvaloniaExtensions
{
    /// <summary>启用 Avalonia 日志输出</summary>
    /// <param name="log">Avalonia 日志</param>
    /// <param name="keepExistingLog">是否保留现有日志输出</param>
    public static void UseAvalonia(AvaloniaCollectionLog log, Boolean keepExistingLog = true)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));

        var current = XTrace.Log;
        if (keepExistingLog && current != Logger.Null)
            XTrace.Log = new CompositeLog(log, current) { Level = current.Level };
        else
            XTrace.Log = log;
    }
}