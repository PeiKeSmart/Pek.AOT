using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;

using Pek.Collections;
using Pek.IO;

namespace Pek.Log;

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

    /// <summary>格式化日志文本</summary>
    /// <param name="format">格式化模板</param>
    /// <param name="args">格式化参数</param>
    /// <returns>格式化结果</returns>
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

    /// <summary>空日志</summary>
    public static ILog Null { get; } = new NullLogger();

    private sealed class NullLogger : Logger
    {
        public override Boolean Enable { get => false; set { } }

        protected override void OnWrite(LogLevel level, String format, params Object?[] args) { }
    }

    /// <summary>输出日志头，包含所有环境信息</summary>
    protected static String GetHead()
    {
        var setting = XTrace.GetSetting();
        var process = Process.GetCurrentProcess();
        var name = AppDomain.CurrentDomain.FriendlyName;
        if (String.IsNullOrWhiteSpace(name)) name = process.ProcessName;

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;
        var fileName = String.Empty;
        var target = RuntimeInformation.FrameworkDescription;
        var targetFramework = AppContext.TargetFrameworkName;
        if (!String.IsNullOrWhiteSpace(targetFramework)) target = $"{target} ({targetFramework})";
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
        var os = machine != null && !String.IsNullOrWhiteSpace(machine.OSName)
            ? $"{machine.OSName} {machine.OSVersion}".Trim()
            : Environment.OSVersion.ToString();

        var builder = Pool.StringBuilder.Get();
        try
        {
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
            builder.AppendFormat("#CLR: {0}, {1}\r\n", Environment.Version, target);
            builder.AppendFormat("#OS: {0}, {1}/{2}\r\n", os, Environment.MachineName, Environment.UserName);
            builder.AppendFormat("#CPU: {0}\r\n", Environment.ProcessorCount);
            if (machine != null)
            {
                if (machine.Memory > 0)
                    builder.AppendFormat("#Memory: {0:n0}M/{1:n0}M\r\n", machine.AvailableMemory / 1024 / 1024, machine.Memory / 1024 / 1024);
                if (!String.IsNullOrWhiteSpace(machine.Processor)) builder.AppendFormat("#Processor: {0}\r\n", machine.Processor);
                if (!String.IsNullOrWhiteSpace(machine.Product)) builder.AppendFormat("#Product: {0} / {1}\r\n", machine.Product, machine.Vendor);
                if (machine.Temperature > 0) builder.AppendFormat("#Temperature: {0}\r\n", machine.Temperature);
            }
            builder.AppendFormat("#GC: IsServerGC={0}, LatencyMode={1}\r\n", GCSettings.IsServerGC, GCSettings.LatencyMode);

            ThreadPool.GetMinThreads(out var minWorker, out var minIo);
            ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);
            ThreadPool.GetAvailableThreads(out var availableWorker, out var availableIo);
            builder.AppendFormat("#ThreadPool: Min={0}/{1}, Max={2}/{3}, Available={4}/{5}\r\n", minWorker, minIo, maxWorker, maxIo, availableWorker, availableIo);
            builder.AppendFormat("#SystemStarted: {0}\r\n", FormatSystemStarted(Runtime.TickCount64));
            builder.AppendFormat("#Date: {0:yyyy-MM-dd}\r\n", DateTime.Now.AddHours(setting.UtcIntervalHours));
            builder.AppendFormat("#详解：{0}\r\n", "https://newlifex.com/core/log");
            builder.AppendFormat("#字段: {0}\r\n", "时间 线程ID 线程池Y/网页W/普通N 线程名/任务ID/定时T/线程池P/长任务L 消息内容");
            builder.AppendFormat("#Fields: {0}\r\n", setting.LogLineFormat.Replace('|', ' '));
            return builder.ToString();
        }
        finally
        {
            Pool.StringBuilder.Return(builder);
        }
    }

    private static String FormatSystemStarted(Int64 tickCount)
    {
        var uptime = TimeSpan.FromMilliseconds(tickCount);
        var hours = (Int64)uptime.TotalHours;

        return $"{hours:D2}:{uptime:mm\\:ss\\.fffffff}";
    }
}
