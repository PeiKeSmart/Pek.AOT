using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

using Pek.Data;
using Pek.Serialization;

namespace Pek;

/// <summary>机器信息接口</summary>
public interface IMachineInfo
{
    /// <summary>初始化静态数据</summary>
    /// <param name="info">机器信息实例</param>
    void Init(MachineInfo info);

    /// <summary>刷新动态数据</summary>
    /// <param name="info">机器信息实例</param>
    void Refresh(MachineInfo info);
}

/// <summary>机器信息</summary>
public class MachineInfo : IExtend
{
    private static readonly Lazy<MachineInfo> _current = new(CreateCurrent);
    private readonly Dictionary<String, Object?> _items = [];

    static MachineInfo() => JsonHelper.Register(MachineInfoJsonContext.Default.MachineInfo);

    /// <summary>系统名称</summary>
    public String OSName { get; set; } = String.Empty;

    /// <summary>系统版本</summary>
    public String OSVersion { get; set; } = String.Empty;

    /// <summary>产品名称</summary>
    public String Product { get; set; } = String.Empty;

    /// <summary>制造商</summary>
    public String Vendor { get; set; } = String.Empty;

    /// <summary>处理器型号</summary>
    public String Processor { get; set; } = String.Empty;

    /// <summary>内存总量</summary>
    public UInt64 Memory { get; set; }

    /// <summary>可用内存</summary>
    public UInt64 AvailableMemory { get; set; }

    /// <summary>温度</summary>
    public Double Temperature { get; set; }

    /// <summary>当前机器信息</summary>
    public static MachineInfo Current => _current.Value;

    /// <summary>机器信息提供者</summary>
    public static IMachineInfo? Provider { get; set; }

    /// <summary>扩展数据项字典</summary>
    public IDictionary<String, Object?> Items => _items;

    /// <summary>获取或设置扩展数据项</summary>
    /// <param name="key">扩展数据键</param>
    /// <returns>扩展值</returns>
    public Object? this[String key]
    {
        get => _items.TryGetValue(key, out var value) ? value : null;
        set => _items[key] = value;
    }

    /// <summary>异步注册机器信息</summary>
    /// <returns>机器信息</returns>
    public static Task<MachineInfo> RegisterAsync() => Task.FromResult(Current);

    /// <summary>获取当前机器信息</summary>
    /// <returns>机器信息</returns>
    public static MachineInfo GetCurrent() => Current;

    /// <summary>获取 Linux 发行版名称</summary>
    /// <returns>发行版描述</returns>
    public static String GetLinuxName() => RuntimeInformation.OSDescription;

    private static MachineInfo CreateCurrent()
    {
        var total = 0UL;
        var available = 0UL;

        TryGetMemory(out total, out available);

        var info = new MachineInfo
        {
            OSName = RuntimeInformation.OSDescription,
            OSVersion = Environment.OSVersion.VersionString,
            Processor = GetProcessorName(),
            Memory = total,
            AvailableMemory = available,
            Product = Environment.MachineName,
            Vendor = Environment.UserDomainName,
        };

        Provider?.Init(info);
        Provider?.Refresh(info);

        return info;
    }

    private static void TryGetMemory(out UInt64 total, out UInt64 available)
    {
        total = 0;
        available = 0;

        if (Runtime.Windows && TryGetWindowsMemory(out total, out available)) return;
        if (Runtime.Linux && TryGetLinuxMemory(out total, out available)) return;

        var gcInfo = GC.GetGCMemoryInfo();
        total = gcInfo.TotalAvailableMemoryBytes > 0 ? (UInt64)gcInfo.TotalAvailableMemoryBytes : 0;
        var used = GC.GetTotalMemory(false);
        available = total > (UInt64)used ? total - (UInt64)used : 0;
    }

    private static Boolean TryGetWindowsMemory(out UInt64 total, out UInt64 available)
    {
        total = 0;
        available = 0;

        var memory = new MEMORYSTATUSEX();
        memory.Init();
        if (!GlobalMemoryStatusEx(ref memory)) return false;

        total = memory.ullTotalPhys;
        available = memory.ullAvailPhys;
        return total > 0;
    }

    private static Boolean TryGetLinuxMemory(out UInt64 total, out UInt64 available)
    {
        total = 0;
        available = 0;

        const String fileName = "/proc/meminfo";
        if (!File.Exists(fileName)) return false;

        foreach (var line in File.ReadLines(fileName))
        {
            if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                total = ParseLinuxMemory(line);
            else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                available = ParseLinuxMemory(line);
        }

        return total > 0;
    }

    private static UInt64 ParseLinuxMemory(String line)
    {
        var value = line[(line.IndexOf(':') + 1)..].Trim();
        if (value.EndsWith("kB", StringComparison.OrdinalIgnoreCase))
            value = value[..^2].Trim();

        return UInt64.TryParse(value, out var size) ? size * 1024 : 0;
    }

    private static String GetProcessorName()
    {
        if (Runtime.Windows)
        {
            var value = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (!String.IsNullOrWhiteSpace(value)) return value;
        }

        return RuntimeInformation.ProcessArchitecture.ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public UInt32 dwLength;
        public UInt32 dwMemoryLoad;
        public UInt64 ullTotalPhys;
        public UInt64 ullAvailPhys;
        public UInt64 ullTotalPageFile;
        public UInt64 ullAvailPageFile;
        public UInt64 ullTotalVirtual;
        public UInt64 ullAvailVirtual;
        public UInt64 ullAvailExtendedVirtual;

        public void Init() => dwLength = (UInt32)Marshal.SizeOf<MEMORYSTATUSEX>();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern Boolean GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}

/// <summary>MachineInfo 的 AOT 序列化上下文</summary>
[JsonSerializable(typeof(MachineInfo))]
public partial class MachineInfoJsonContext : JsonSerializerContext
{
}
