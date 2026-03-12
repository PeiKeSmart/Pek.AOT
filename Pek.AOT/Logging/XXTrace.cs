using NewLife.Threading;

namespace Pek.Logging;

/// <summary>XXTrace 日志入口</summary>
public static class XXTrace
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

    static XXTrace()
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

    internal static XXTraceSetting GetSetting() => TryGetSetting(out var setting) ? setting : XXTraceSetting.CreateDefault();

    private static Boolean TryGetSetting(out XXTraceSetting setting)
    {
        try
        {
            setting = XXTraceSetting.Current;
            setting.Normalize();
            return true;
        }
        catch
        {
            setting = XXTraceSetting.CreateDefault();
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