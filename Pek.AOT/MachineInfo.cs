using System.Runtime.InteropServices;

namespace Pek;

/// <summary>机器信息</summary>
public class MachineInfo
{
    private static readonly Lazy<MachineInfo> _current = new(CreateCurrent);

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

        return new MachineInfo
        {
            OSName = RuntimeInformation.OSDescription,
            OSVersion = Environment.OSVersion.VersionString,
            Processor = GetProcessorName(),
            Memory = total,
            AvailableMemory = available,
            Product = Environment.MachineName,
            Vendor = Environment.UserDomainName,
        };
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
