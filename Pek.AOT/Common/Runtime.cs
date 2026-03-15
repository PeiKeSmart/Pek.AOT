using System.Collections;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using Pek.Log;
using Pek.Threading;

namespace Pek;

/// <summary>运行时</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/runtime
/// </remarks>
public static class Runtime
{
    private const String LogScope = "Pek.Common";

    #region 静态构造
    private static Boolean? _IsConsole;
    private static Boolean? _IsWeb;
    private static String _ClientId = String.Empty;
    private static Int32 _ProcessId;
    private static Boolean? _createConfigOnMissing;

    static Runtime()
    {
        try
        {
            Mono = Type.GetType("Mono.Runtime") != null;
        }
        catch { }

        try
        {
            Unity = Type.GetType("UnityEngine.Application, UnityEngine") != null;
        }
        catch { }

        if (!Unity)
            Unity = !String.IsNullOrWhiteSpace(GetEnvironmentVariable("UNITY_VERSION")) || !String.IsNullOrWhiteSpace(GetEnvironmentVariable("UNITY_PLAYER"));
    }
    #endregion

    #region 控制台
    /// <summary>是否控制台。用于判断是否可以执行一些控制台操作。</summary>
    /// <remarks>
    /// 通过访问 <see cref="Console.ForegroundColor"/> 触发控制台可用性检查；
    /// 若当前进程存在主窗口句柄（Windows GUI 应用），则视为非控制台；否则视为控制台。
    /// 任何异常（如无控制台缓冲区）将被视为非控制台环境。
    /// </remarks>
    public static Boolean IsConsole
    {
        get
        {
            if (_IsConsole != null) return _IsConsole.Value;

            _IsConsole = true;

            try
            {
                _ = Console.ForegroundColor; // 触发控制台可用性检查
                if (Process.GetCurrentProcess().MainWindowHandle != IntPtr.Zero)
                    _IsConsole = false;
                else
                    _IsConsole = true;
            }
            catch
            {
                _IsConsole = false;
            }

            return _IsConsole.Value;
        }
        set => _IsConsole = value;
    }

    /// <summary>是否在容器中运行</summary>
    public static Boolean Container
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }
    }
    #endregion

    #region 系统特性
    /// <summary>是否Mono环境</summary>
    public static Boolean Mono { get; }

    /// <summary>是否Unity环境</summary>
    public static Boolean Unity { get; }

    /// <summary>是否Web环境</summary>
    public static Boolean IsWeb
    {
        get
        {
            if (_IsWeb != null) return _IsWeb.Value;

            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(e => e.GetName().Name == "Microsoft.AspNetCore");
                _IsWeb = asm != null;
            }
            catch
            {
                _IsWeb = false;
            }

            return _IsWeb.Value;
        }
    }

    /// <summary>是否Windows环境</summary>
    public static Boolean Windows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>是否Linux环境</summary>
    public static Boolean Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>是否OSX环境</summary>
    public static Boolean OSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    #endregion

    #region 扩展
    /// <summary>系统启动以来的毫秒数</summary>
#if NETCOREAPP3_1_OR_GREATER
    public static Int64 TickCount64 => Environment.TickCount64;
#else
    /// <summary>系统启动以来的毫秒数</summary>
    public static Int64 TickCount64
    {
        get
        {
            if (Windows)
            {
                try
                {
                    return unchecked((Int64)GetTickCount64());
                }
                catch { }
            }

            if (Stopwatch.IsHighResolution) return Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;

            return Environment.TickCount;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern UInt64 GetTickCount64();
#endif

    /// <summary>UTC 当前时间</summary>
    public static DateTimeOffset UtcNow => TimerScheduler.GlobalTimeProvider.GetUtcNow();

    /// <summary>当前进程标识</summary>
    public static Int32 ProcessId => _ProcessId > 0 ? _ProcessId : _ProcessId = Environment.ProcessId;

    /// <summary>客户端标识</summary>
    public static String ClientId
    {
        get
        {
            if (!String.IsNullOrWhiteSpace(_ClientId)) return _ClientId;

            try
            {
                var host = Environment.MachineName;
                _ClientId = $"{host}@{ProcessId}";
            }
            catch
            {
                _ClientId = ProcessId.ToString();
            }

            return _ClientId;
        }
    }

    /// <summary>不区分大小写读取环境变量</summary>
    /// <param name="variable">环境变量名</param>
    /// <returns>环境变量值</returns>
    public static String GetEnvironmentVariable(String variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (!String.IsNullOrWhiteSpace(value)) return value;

        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            if (item.Key is String key && String.Equals(key, variable, StringComparison.OrdinalIgnoreCase))
                return item.Value?.ToString() ?? String.Empty;
        }

        return String.Empty;
    }

    /// <summary>获取环境变量集合。不区分大小写</summary>
    /// <returns>环境变量集合</returns>
    public static IDictionary<String, String?> GetEnvironmentVariables()
    {
        var dic = new Dictionary<String, String?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            var key = item.Key as String;
            if (!String.IsNullOrWhiteSpace(key)) dic[key] = item.Value as String;
        }

        return dic;
    }
    #endregion

    #region 设置
    /// <summary>默认配置。配置文件不存在时，是否生成默认配置文件</summary>
    public static Boolean CreateConfigOnMissing
    {
        get
        {
            if (_createConfigOnMissing == null)
            {
                var value = Environment.GetEnvironmentVariable("CreateConfigOnMissing");
                _createConfigOnMissing = !String.IsNullOrWhiteSpace(value) ? value.ToBoolean(true) : true;
            }

            return _createConfigOnMissing.Value;
        }
        set => _createConfigOnMissing = value;
    }
    #endregion

    #region 内存
    /// <summary>释放内存。GC回收后再释放虚拟内存</summary>
    /// <param name="processId">进程Id。默认0表示当前进程</param>
    /// <param name="gc">是否GC回收</param>
    /// <param name="workingSet">是否释放工作集</param>
    public static Boolean FreeMemory(Int32 processId = 0, Boolean gc = true, Boolean workingSet = true)
    {
        if (processId <= 0) processId = ProcessId;

        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error(XXTrace.FormatScope(LogScope, nameof(Runtime), "获取进程失败 ProcessId={0} Error={1}"), processId, ex.Message);
            return false;
        }

        if (process == null || process.HasExited) return false;

        if (processId != ProcessId) gc = false;

        var log = XTrace.Log;
        if (log.Enable && log.Level <= LogLevel.Debug)
        {
            var gcMemory = GC.GetTotalMemory(false) / 1024;
            var workingSetSize = process.WorkingSet64 / 1024;
            var privateMemory = process.PrivateMemorySize64 / 1024;
            if (gc)
                log.Debug(XXTrace.FormatScope(LogScope, nameof(Runtime), "{3}/{4} 开始释放内存：GC={0:n0}K，WorkingSet={1:n0}K，PrivateMemory={2:n0}K"), gcMemory, workingSetSize, privateMemory, process.ProcessName, process.Id);
            else
                log.Debug(XXTrace.FormatScope(LogScope, nameof(Runtime), "{3}/{4} 开始释放内存：WorkingSet={1:n0}K，PrivateMemory={2:n0}K"), gcMemory, workingSetSize, privateMemory, process.ProcessName, process.Id);
        }

        if (gc)
        {
            var max = GC.MaxGeneration;
            var mode = GCCollectionMode.Forced;
#if NET8_0_OR_GREATER
            mode = GCCollectionMode.Aggressive;
#endif
#if NET451_OR_GREATER || NETSTANDARD || NETCOREAPP
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
#endif
            GC.Collect(max, mode);
            GC.WaitForPendingFinalizers();
            GC.Collect(max, mode);
        }

        if (workingSet && Windows)
        {
            try
            {
                EmptyWorkingSet(process.Handle);
            }
            catch (Exception ex)
            {
                log.Error(XXTrace.FormatScope(LogScope, nameof(Runtime), "EmptyWorkingSet失败：{0}"), ex.Message);
                return false;
            }
        }

        if (log.Enable && log.Level <= LogLevel.Debug)
        {
            process.Refresh();
            var gcMemory = GC.GetTotalMemory(false) / 1024;
            var workingSetSize = process.WorkingSet64 / 1024;
            var privateMemory = process.PrivateMemorySize64 / 1024;
            if (gc)
                log.Debug(XXTrace.FormatScope(LogScope, nameof(Runtime), "{3}/{4} 释放内存完成：GC={0:n0}K，WorkingSet={1:n0}K，PrivateMemory={2:n0}K"), gcMemory, workingSetSize, privateMemory, process.ProcessName, process.Id);
            else
                log.Debug(XXTrace.FormatScope(LogScope, nameof(Runtime), "{3}/{4} 释放内存完成：WorkingSet={1:n0}K，PrivateMemory={2:n0}K"), gcMemory, workingSetSize, privateMemory, process.ProcessName, process.Id);
        }

        return true;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern Boolean EmptyWorkingSet(IntPtr hProcess);
    #endregion
}
