using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NewLife;

/// <summary>运行时辅助</summary>
public static class Runtime
{
    private static Int32 _isConsole = -1;
    private static Int32 _isWeb = -1;
    private static String _clientId = String.Empty;
    private static Int32 _processId;

    static Runtime()
    {
        try
        {
            Mono = Type.GetType("Mono.Runtime") != null;
        }
        catch
        {
        }

        try
        {
            Unity = Type.GetType("UnityEngine.Application, UnityEngine") != null;
        }
        catch
        {
        }
    }

    /// <summary>是否控制台环境</summary>
    public static Boolean IsConsole
    {
        get
        {
            if (_isConsole >= 0) return _isConsole == 1;

            try
            {
                _ = Console.ForegroundColor;
                _isConsole = Process.GetCurrentProcess().MainWindowHandle == IntPtr.Zero ? 1 : 0;
            }
            catch
            {
                _isConsole = 0;
            }

            return _isConsole == 1;
        }
        set => _isConsole = value ? 1 : 0;
    }

    /// <summary>是否容器环境</summary>
    public static Boolean Container
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }
    }

    /// <summary>是否 Mono 环境</summary>
    public static Boolean Mono { get; }

    /// <summary>是否 Unity 环境</summary>
    public static Boolean Unity { get; }

    /// <summary>是否 Web 环境</summary>
    public static Boolean IsWeb
    {
        get
        {
            if (_isWeb >= 0) return _isWeb == 1;

            try
            {
                _isWeb = AppDomain.CurrentDomain.GetAssemblies().Any(e => e.GetName().Name == "Microsoft.AspNetCore") ? 1 : 0;
            }
            catch
            {
                _isWeb = 0;
            }

            return _isWeb == 1;
        }
    }

    /// <summary>是否 Windows</summary>
    public static Boolean Windows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>是否 Linux</summary>
    public static Boolean Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>是否 macOS</summary>
    public static Boolean OSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>系统启动以来的毫秒数</summary>
    public static Int64 TickCount64 => Environment.TickCount64;

    /// <summary>UTC 当前时间</summary>
    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <summary>当前进程标识</summary>
    public static Int32 ProcessId => _processId > 0 ? _processId : _processId = Environment.ProcessId;

    /// <summary>客户端标识</summary>
    public static String ClientId
    {
        get
        {
            if (!String.IsNullOrWhiteSpace(_clientId)) return _clientId;

            try
            {
                var host = Environment.MachineName;
                _clientId = $"{host}@{ProcessId}";
            }
            catch
            {
                _clientId = ProcessId.ToString();
            }

            return _clientId;
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
}
