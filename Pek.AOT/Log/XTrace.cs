using Pek.Threading;
using Pek;

namespace Pek.Log;

/// <summary>XTrace 日志入口</summary>
public static class XTrace
{
    private static readonly Object _lock = new();
    private static ILog _log = Logger.Null;
    private static Boolean _useConsole;

    /// <summary>日志提供者</summary>
    public static ILog Log
    {
        get
        {
            InitLog();
            return _log;
        }
        set => _log = value ?? Logger.Null;
    }

    /// <summary>是否启用调试</summary>
    public static Boolean Debug => GetSetting().Debug;

    /// <summary>日志目录</summary>
    public static String LogPath => GetSetting().LogPath;

    static XTrace()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => OnProcessExit();
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            foreach (var item in e.Exception.Flatten().InnerExceptions)
            {
                WriteException(item);
            }

            e.SetObserved();
        };

        ThreadPoolX.Init();
    }

    /// <summary>输出日志</summary>
    /// <param name="message">日志消息</param>
    public static void WriteLine(String message)
    {
        if (message == null) return;
        Log.Info(message);
    }

    /// <summary>输出日志</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public static void WriteLine(String format, params Object?[] args)
    {
        if (format == null) return;
        Log.Info(format, args);
    }

    /// <summary>按统一前缀输出日志</summary>
    /// <param name="scope">模块范围，例如 Pek.Configuration</param>
    /// <param name="stage">阶段或子模块，例如 Config</param>
    /// <param name="message">日志消息</param>
    public static void WriteScope(String scope, String stage, String message)
    {
        if (String.IsNullOrWhiteSpace(scope) || String.IsNullOrWhiteSpace(stage) || message == null) return;
        var log = Log;
        if (!IsEnabled(log, LogLevel.Info)) return;
        log.Info(FormatScope(scope, stage, message));
    }

    /// <summary>按统一前缀输出格式化日志</summary>
    /// <param name="scope">模块范围，例如 Pek.Configuration</param>
    /// <param name="stage">阶段或子模块，例如 Config</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public static void WriteScope(String scope, String stage, String format, params Object?[] args)
    {
        if (String.IsNullOrWhiteSpace(scope) || String.IsNullOrWhiteSpace(stage) || format == null) return;
        var log = Log;
        if (!IsEnabled(log, LogLevel.Info)) return;
        log.Info(FormatScope(scope, stage, format), args);
    }

    /// <summary>按统一前缀输出格式化日志，并附加固定前缀</summary>
    /// <param name="scope">模块范围，例如 Pek.Configuration</param>
    /// <param name="stage">阶段或子模块，例如 Config</param>
    /// <param name="prefix">正文固定前缀</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    public static void WriteScope(String scope, String stage, String prefix, String format, params Object?[] args)
    {
        if (String.IsNullOrWhiteSpace(scope) || String.IsNullOrWhiteSpace(stage) || format == null) return;
        var log = Log;
        if (!IsEnabled(log, LogLevel.Info)) return;
        log.Info(FormatScope(scope, stage, prefix, format), args);
    }

    /// <summary>格式化统一前缀日志文本</summary>
    /// <param name="scope">模块范围</param>
    /// <param name="stage">阶段或子模块</param>
    /// <param name="message">日志正文</param>
    /// <returns>格式化后的日志文本</returns>
    public static String FormatScope(String scope, String stage, String message) => $"[{scope}][{stage}] {message}";

    /// <summary>格式化统一前缀日志文本，并附加固定前缀</summary>
    /// <param name="scope">模块范围</param>
    /// <param name="stage">阶段或子模块</param>
    /// <param name="prefix">正文固定前缀</param>
    /// <param name="message">日志正文</param>
    /// <returns>格式化后的日志文本</returns>
    public static String FormatScope(String scope, String stage, String prefix, String message) => $"[{scope}][{stage}] {prefix}{message}";

    /// <summary>输出异常</summary>
    /// <param name="exception">异常对象</param>
    public static void WriteException(Exception exception)
    {
        if (exception == null) return;
        Log.Error("{0}", exception);
    }

    /// <summary>启用控制台输出</summary>
    /// <param name="useColor">是否使用颜色</param>
    /// <param name="useFileLog">是否同时使用文件日志</param>
    public static void UseConsole(Boolean useColor = true, Boolean useFileLog = true)
    {
        if (_useConsole) return;
        _useConsole = true;

        var setting = GetSetting();
        var consoleLog = new ConsoleLog { UseColor = useColor, Level = setting.LogLevel };
        if (useFileLog)
            _log = new CompositeLog(consoleLog, Log);
        else
            _log = consoleLog;
    }

    /// <summary>关闭并释放当前日志提供者</summary>
    public static void Shutdown()
    {
        OnProcessExit();
        _log = Logger.Null;
        _useConsole = false;
    }

    private static void InitLog()
    {
        if (_log != Logger.Null) return;

        lock (_lock)
        {
            if (_log != Logger.Null) return;

            if (!TryGetSetting(out var setting)) return;

            _log = setting.LogFileFormat.Contains("{1}", StringComparison.Ordinal)
                ? new LevelLog(setting.LogPath, setting.LogFileFormat) { Level = setting.LogLevel }
                : TextFileLog.Create(setting.LogPath, setting.LogFileFormat);

            _log.Level = setting.LogLevel;
        }
    }

    private static Boolean IsEnabled(ILog log, LogLevel level) => log.Enable && level >= log.Level;

    internal static Setting GetSetting() => TryGetSetting(out var setting) ? setting : new Setting();

    private static Boolean TryGetSetting(out Setting setting)
    {
        try
        {
            setting = Setting.Current;
            return true;
        }
        catch
        {
            setting = new Setting();
            return false;
        }
    }

    private static void OnProcessExit()
    {
        if (_log is CompositeLog composite)
        {
            var fileLog = composite.Get<TextFileLog>();
            fileLog?.Dispose();
        }
        else if (_log is TextFileLog textFileLog)
        {
            textFileLog.Dispose();
        }
    }
}