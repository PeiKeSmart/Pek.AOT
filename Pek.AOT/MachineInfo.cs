using System.Runtime.InteropServices;

namespace NewLife;

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
        var gcInfo = GC.GetGCMemoryInfo();
        var total = gcInfo.TotalAvailableMemoryBytes > 0 ? (UInt64)gcInfo.TotalAvailableMemoryBytes : 0;
        var used = GC.GetTotalMemory(false);
        var available = total > (UInt64)used ? total - (UInt64)used : 0;

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

    private static String GetProcessorName()
    {
        if (Runtime.Windows)
        {
            var value = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (!String.IsNullOrWhiteSpace(value)) return value;
        }

        return RuntimeInformation.ProcessArchitecture.ToString();
    }
}
