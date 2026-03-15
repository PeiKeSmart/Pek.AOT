namespace Pek.Log;

/// <summary>XXTrace 兼容入口</summary>
public static class XXTrace
{
    /// <summary>日志提供者</summary>
    public static ILog Log { get => XTrace.Log; set => XTrace.Log = value; }

    /// <summary>是否启用调试</summary>
    public static Boolean Debug => XTrace.Debug;

    /// <summary>日志目录</summary>
    public static String LogPath => XTrace.LogPath;

    /// <summary>输出日志</summary>
    /// <param name="message">日志消息</param>
    public static void WriteLine(String message) => XTrace.WriteLine(message);

    /// <summary>输出日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public static void WriteLine(String format, params Object?[] args) => XTrace.WriteLine(format, args);

    /// <summary>按统一前缀输出日志</summary>
    /// <param name="scope">模块范围，例如 Pek.Configuration</param>
    /// <param name="stage">阶段或子模块，例如 Config</param>
    /// <param name="message">日志消息</param>
    public static void WriteScope(String scope, String stage, String message) => XTrace.WriteScope(scope, stage, message);

    /// <summary>按统一前缀输出格式化日志</summary>
    /// <param name="scope">模块范围，例如 Pek.Configuration</param>
    /// <param name="stage">阶段或子模块，例如 Config</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public static void WriteScope(String scope, String stage, String format, params Object?[] args) => XTrace.WriteScope(scope, stage, format, args);

    /// <summary>按统一前缀输出格式化日志，并附加固定前缀</summary>
    /// <param name="scope">模块范围，例如 Pek.Configuration</param>
    /// <param name="stage">阶段或子模块，例如 Config</param>
    /// <param name="prefix">正文固定前缀</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public static void WriteScope(String scope, String stage, String prefix, String format, params Object?[] args) => XTrace.WriteScope(scope, stage, prefix, format, args);

    /// <summary>格式化统一前缀日志文本</summary>
    /// <param name="scope">模块范围</param>
    /// <param name="stage">阶段或子模块</param>
    /// <param name="message">日志正文</param>
    /// <returns>格式化后的日志文本</returns>
    public static String FormatScope(String scope, String stage, String message) => XTrace.FormatScope(scope, stage, message);

    /// <summary>格式化统一前缀日志文本，并附加固定前缀</summary>
    /// <param name="scope">模块范围</param>
    /// <param name="stage">阶段或子模块</param>
    /// <param name="prefix">正文固定前缀</param>
    /// <param name="message">日志正文</param>
    /// <returns>格式化后的日志文本</returns>
    public static String FormatScope(String scope, String stage, String prefix, String message) => XTrace.FormatScope(scope, stage, prefix, message);

    /// <summary>输出异常</summary>
    /// <param name="exception">异常对象</param>
    public static void WriteException(Exception exception) => XTrace.WriteException(exception);

    /// <summary>启用控制台输出</summary>
    /// <param name="useColor">是否使用颜色</param>
    /// <param name="useFileLog">是否同时使用文件日志</param>
    public static void UseConsole(Boolean useColor = true, Boolean useFileLog = true) => XTrace.UseConsole(useColor, useFileLog);

    /// <summary>关闭并释放当前日志提供者</summary>
    public static void Shutdown() => XTrace.Shutdown();

    internal static Setting GetSetting() => XTrace.GetSetting();
}