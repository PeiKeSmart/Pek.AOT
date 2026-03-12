using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;

using NewLife;
using NewLife.Threading;

namespace Pek.Logging;

/// <summary>文本文件日志</summary>
public class TextFileLog : Logger, IDisposable
{
    private static readonly ConcurrentDictionary<String, TextFileLog> _cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly Boolean _isFile;
    private readonly ConcurrentQueue<String> _logs = new();
    private readonly TimerX _timer;
    private StreamWriter? _writer;
    private String? _currentLogFile;
    private Int32 _logFileError;
    private Int32 _writing;
    private Int32 _logCount;
    private DateTime _nextClose;
    private Boolean _isFirst = true;

    /// <summary>日志目录</summary>
    public String LogPath { get; set; }

    /// <summary>日志文件格式</summary>
    public String FileFormat { get; set; }

    /// <summary>日志文件大小上限，单位 MB</summary>
    public Int32 MaxBytes { get; set; }

    /// <summary>日志文件备份数量</summary>
    public Int32 Backups { get; set; }

    /// <summary>实例化</summary>
    public TextFileLog() : this(String.Empty, false, null) { }

    internal TextFileLog(String path, Boolean isFile, String? fileFormat = null)
    {
        var setting = XXTraceSetting.Current;
        LogPath = path;
        _isFile = isFile;
        FileFormat = String.IsNullOrWhiteSpace(fileFormat) ? setting.LogFileFormat : fileFormat;
        MaxBytes = setting.LogFileMaxBytes;
        Backups = setting.LogFileBackups;
        _timer = new TimerX(DoWriteAndClose, null, 0, 5000) { Async = true };
    }

    /// <summary>获取目录日志实例</summary>
    /// <param name="path">日志目录</param>
    /// <param name="fileFormat">文件格式</param>
    /// <returns>日志实例</returns>
    public static TextFileLog Create(String path, String? fileFormat = null)
    {
        if (String.IsNullOrWhiteSpace(path)) path = "Log";
        var key = (path + fileFormat).ToLowerInvariant();
        return _cache.GetOrAdd(key, _ => new TextFileLog(path, false, fileFormat));
    }

    /// <summary>获取单文件日志实例</summary>
    /// <param name="path">日志文件路径</param>
    /// <returns>日志实例</returns>
    public static TextFileLog CreateFile(String path)
    {
        if (String.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        return _cache.GetOrAdd(path, _ => new TextFileLog(path, true));
    }

    /// <summary>销毁日志</summary>
    public void Dispose()
    {
        _timer.Dispose();
        if (Interlocked.CompareExchange(ref _writing, 1, 0) == 0) WriteAndClose(DateTime.MinValue);
        GC.SuppressFinalize(this);
    }

    /// <summary>写入日志</summary>
    /// <param name="level">日志等级</param>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    protected override void OnWrite(LogLevel level, String format, params Object?[] args)
    {
        if (_logCount > 1024) return;

        var item = WriteLogEventArgs.Current.Set(level);
        if (args.Length == 1 && args[0] is Exception ex && (String.IsNullOrEmpty(format) || format == "{0}"))
            item.Set(null, ex);
        else
            item.Set(Format(format, args), null);

        _logs.Enqueue(item.GetAndReset());
        Interlocked.Increment(ref _logCount);

        if (Interlocked.CompareExchange(ref _writing, 1, 0) != 0) return;

        if (XXTraceSetting.Current.LogLevel <= LogLevel.Debug || level >= LogLevel.Error)
        {
            try
            {
                WriteFile();
            }
            finally
            {
                _writing = 0;
            }
        }
        else
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                try
                {
                    WriteFile();
                }
                catch
                {
                }
                finally
                {
                    _writing = 0;
                }
            }, null);
        }
    }

    private void WriteFile()
    {
        var now = TimerX.Now.AddHours(XXTraceSetting.Current.UtcIntervalHours);
        var logFile = GetLogFile();
        if (String.IsNullOrWhiteSpace(logFile)) return;

        if (!_isFile && !String.Equals(logFile, _currentLogFile, StringComparison.OrdinalIgnoreCase))
        {
            _writer?.Dispose();
            _writer = null;
            _currentLogFile = logFile;
            _logFileError = 0;
        }

        if (_writer == null && _logFileError >= 3) return;

        _writer ??= InitWriter(logFile);
        if (_writer == null) return;

        while (_logs.TryDequeue(out var item))
        {
            Interlocked.Decrement(ref _logCount);
            _writer.Write(item);
            _writer.WriteLine();
        }

        _writer.Flush();
        _nextClose = now.AddSeconds(5);
    }

    private void DoWriteAndClose(Object? state)
    {
        if (Interlocked.CompareExchange(ref _writing, 1, 0) == 0) WriteAndClose(_nextClose);
        if (!_isFile && Backups > 0) CleanupBackups();
    }

    private void WriteAndClose(DateTime closeTime)
    {
        try
        {
            if (!_logs.IsEmpty) WriteFile();
            if (_writer != null && closeTime < TimerX.Now.AddHours(XXTraceSetting.Current.UtcIntervalHours))
            {
                _writer.Dispose();
                _writer = null;
            }
        }
        finally
        {
            _writing = 0;
        }
    }

    private StreamWriter? InitWriter(String logFile)
    {
        try
        {
            var directory = Path.GetDirectoryName(logFile);
            if (!String.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(stream, new UTF8Encoding(false));
            if (_isFirst)
            {
                _isFirst = false;
                if (writer.BaseStream.Length > 0) writer.WriteLine();
                writer.Write(GetHead());
            }

            _logFileError = 0;
            return writer;
        }
        catch (Exception ex)
        {
            _logFileError++;
            Console.WriteLine($"创建日志文件失败: {ex.Message}");
            return null;
        }
    }

    private String? GetLogFile()
    {
        if (_isFile) return GetBasePath(LogPath);

        var basePath = GetBasePath(LogPath);
        var candidate = Path.Combine(basePath, String.Format(FileFormat, TimerX.Now.AddHours(XXTraceSetting.Current.UtcIntervalHours), Level));
        if (MaxBytes == 0) return candidate;

        var maxBytes = MaxBytes * 1024L * 1024L;
        var extension = Path.GetExtension(candidate);
        var prefix = candidate[..^extension.Length];
        for (var index = 1; index < 1024; index++)
        {
            var path = index == 1 ? candidate : $"{prefix}_{index}{extension}";
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < maxBytes) return path;
        }

        return null;
    }

    private void CleanupBackups()
    {
        var directoryPath = GetBasePath(LogPath);
        var directory = new DirectoryInfo(directoryPath);
        if (!directory.Exists) return;

        var extension = Path.GetExtension(FileFormat);
        if (String.IsNullOrWhiteSpace(extension)) extension = ".log";

        FileInfo[] files;
        try
        {
            files = directory.GetFiles($"*{extension}");
        }
        catch
        {
            return;
        }

        if (files.Length <= Backups) return;

        foreach (var item in files.OrderBy(e => e.CreationTimeUtc).Take(files.Length - Backups))
        {
            try
            {
                item.Delete();
            }
            catch
            {
            }
        }
    }

    private static String GetBasePath(String path) => path.GetBasePath();

    private static String GetHead()
    {
        var setting = XXTraceSetting.Current;
        var process = Process.GetCurrentProcess();
        var assembly = Assembly.GetEntryAssembly();
        var name = assembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        if (String.IsNullOrWhiteSpace(name)) name = assembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        if (String.IsNullOrWhiteSpace(name)) name = assembly?.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        if (String.IsNullOrWhiteSpace(name)) name = process.ProcessName;

        var framework = RuntimeInformation.FrameworkDescription;
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;
        var fileName = String.Empty;
        try
        {
            fileName = process.MainModule?.FileName ?? String.Empty;
        }
        catch
        {
        }

        if (String.IsNullOrWhiteSpace(fileName) || fileName.EndsWith("dotnet", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                fileName = process.StartInfo.FileName;
            }
            catch
            {
            }
        }

        var machine = MachineInfo.Current;
        var os = !String.IsNullOrWhiteSpace(machine.OSName)
            ? $"{machine.OSName} {machine.OSVersion}".Trim()
            : Environment.OSVersion.ToString();

        var builder = new StringBuilder();
        builder.AppendFormat("#Software: {0}\r\n", name);
        builder.AppendFormat("#ProcessID: {0}{1}\r\n", process.Id, Environment.Is64BitProcess ? " x64" : String.Empty);
        builder.AppendFormat("#AppDomain: {0}\r\n", AppDomain.CurrentDomain.FriendlyName);
        if (!String.IsNullOrWhiteSpace(fileName)) builder.AppendFormat("#FileName: {0}\r\n", fileName);
        builder.AppendFormat("#BaseDirectory: {0}\r\n", baseDirectory);
        if (!String.Equals(baseDirectory.TrimEnd('\\', '/'), currentDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            builder.AppendFormat("#CurrentDirectory: {0}\r\n", currentDirectory);

        var basePath = PathHelper.BasePath;
        if (!String.IsNullOrWhiteSpace(basePath) && !String.Equals(basePath.TrimEnd('\\', '/'), baseDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            builder.AppendFormat("#BasePath: {0}\r\n", basePath);

        builder.AppendFormat("#TempPath: {0}\r\n", Path.GetTempPath());
        if (!String.IsNullOrWhiteSpace(Environment.CommandLine)) builder.AppendFormat("#CommandLine: {0}\r\n", Environment.CommandLine);

        var applicationType = Runtime.IsWeb
            ? "Web"
            : !Environment.UserInteractive
                ? "Service"
                : Runtime.IsConsole
                    ? "Console"
                    : "WinForm";
        if (Runtime.Container) applicationType += "(Container)";

        builder.AppendFormat("#ApplicationType: {0}\r\n", applicationType);
        builder.AppendFormat("#CLR: {0}, {1}\r\n", Environment.Version, framework);
        builder.AppendFormat("#OS: {0}, {1}/{2}\r\n", os, Environment.MachineName, Environment.UserName);
        builder.AppendFormat("#CPU: {0}\r\n", Environment.ProcessorCount);
        if (machine.Memory > 0)
            builder.AppendFormat("#Memory: {0:n0}M/{1:n0}M\r\n", machine.AvailableMemory / 1024 / 1024, machine.Memory / 1024 / 1024);
        if (!String.IsNullOrWhiteSpace(machine.Processor)) builder.AppendFormat("#Processor: {0}\r\n", machine.Processor);
        if (!String.IsNullOrWhiteSpace(machine.Product)) builder.AppendFormat("#Product: {0} / {1}\r\n", machine.Product, machine.Vendor);
        if (machine.Temperature > 0) builder.AppendFormat("#Temperature: {0}\r\n", machine.Temperature);
        builder.AppendFormat("#GC: IsServerGC={0}, LatencyMode={1}\r\n", GCSettings.IsServerGC, GCSettings.LatencyMode);

        ThreadPool.GetMinThreads(out var minWorker, out var minIo);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);
        ThreadPool.GetAvailableThreads(out var availableWorker, out var availableIo);
        builder.AppendFormat("#ThreadPool: Min={0}/{1}, Max={2}/{3}, Available={4}/{5}\r\n", minWorker, minIo, maxWorker, maxIo, availableWorker, availableIo);
        builder.AppendFormat("#SystemStarted: {0}\r\n", TimeSpan.FromMilliseconds(Runtime.TickCount64));
        builder.AppendFormat("#Date: {0:yyyy-MM-dd}\r\n", DateTime.Now.AddHours(setting.UtcIntervalHours));
        builder.AppendFormat("#详解：{0}\r\n", "https://newlifex.com/core/log");
        builder.AppendFormat("#字段: {0}\r\n", "时间 线程ID 线程池Y/网页W/普通N 线程名/任务ID/定时T/线程池P/长任务L 消息内容");
        builder.AppendFormat("#Fields: {0}\r\n", setting.LogLineFormat.Replace('|', ' '));
        return builder.ToString();
    }
}
