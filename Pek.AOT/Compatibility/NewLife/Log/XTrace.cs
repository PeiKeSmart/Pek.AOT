namespace NewLife.Log;

/// <summary>最小可用的 XTrace 兼容实现</summary>
public static class XTrace
{
    private static ILog _log = new ConsoleLog();

    /// <summary>是否启用调试</summary>
    public static Boolean Debug { get; set; } = true;

    /// <summary>日志目录</summary>
    public static String LogPath { get; set; } = "Log";

    /// <summary>日志提供者</summary>
    public static ILog Log
    {
        get => _log;
        set => _log = value ?? Logger.Null;
    }

    /// <summary>输出普通日志</summary>
    /// <param name="msg">日志消息</param>
    public static void WriteLine(String msg)
    {
        if (msg == null) return;

        Log.Info(msg);
    }

    /// <summary>输出格式化日志</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    public static void WriteLine(String format, params Object?[] args)
    {
        if (format == null) return;

        Log.Info(format, args);
    }

    /// <summary>输出异常日志</summary>
    /// <param name="ex">异常信息</param>
    public static void WriteException(Exception ex)
    {
        if (ex == null) return;

        Log.Error("{0}", ex);
    }
}