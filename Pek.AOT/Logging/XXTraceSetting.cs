using System.ComponentModel;
using System.Text.Json.Serialization;

using Pek.Configuration;

namespace Pek.Logging;

/// <summary>XXTrace 日志配置</summary>
[DisplayName("核心设置")]
[Config("Core")]
public class XXTraceSetting : Config<XXTraceSetting, XXTraceSettingJsonContext>
{
    /// <summary>是否启用全局调试</summary>
    [Description("全局调试。XXTrace.Debug")]
    public Boolean Debug { get; set; } = true;

    /// <summary>日志等级</summary>
    [Description("日志等级。只输出大于等于该级别的日志，All/Debug/Info/Warn/Error/Fatal，默认Info")]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>文件日志目录</summary>
    [Description("文件日志目录。默认Log子目录")]
    public String LogPath { get; set; } = "Log";

    /// <summary>日志文件上限，单位 MB，0 表示不限制</summary>
    [Description("日志文件上限。超过上限后拆分新日志文件，默认10MB，0表示不限制大小")]
    public Int32 LogFileMaxBytes { get; set; } = 10;

    /// <summary>日志文件备份数量，0 表示不限制</summary>
    [Description("日志文件备份。超过备份数后，最旧的文件将被删除，默认100，0表示不限制个数")]
    public Int32 LogFileBackups { get; set; } = 100;

    /// <summary>日志文件格式，支持 {0} 日期和 {1} 日志等级</summary>
    [Description("日志文件格式。默认{0:yyyy_MM_dd}.log，支持日志等级如 {1}_{0:yyyy_MM_dd}.log")]
    public String LogFileFormat { get; set; } = "{0:yyyy_MM_dd}.log";

    /// <summary>日志行格式</summary>
    [Description("日志行格式。默认Time|ThreadId|Kind|Name|Message，还支持Level")]
    public String LogLineFormat { get; set; } = "Time|ThreadId|Kind|Name|Message";

    /// <summary>网络日志地址，支持 udp、tcp、http、https</summary>
    [Description("网络日志。本地子网日志广播udp://255.255.255.255:514，或者http://xxx:80/log")]
    public String NetworkLog { get; set; } = String.Empty;

    /// <summary>日志时间 UTC 校正小时数</summary>
    [Description("日志记录时间UTC校正，小时")]
    public Int32 UtcIntervalHours { get; set; }

    /// <summary>数据目录</summary>
    [Description("数据目录。本地数据库目录，默认Data子目录")]
    public String DataPath { get; set; } = "Data";

    /// <summary>备份目录</summary>
    [Description("备份目录。备份数据库时存放的目录，默认Backup子目录")]
    public String BackupPath { get; set; } = "Backup";

    /// <summary>插件目录</summary>
    [Description("插件目录")]
    public String PluginPath { get; set; } = "Plugins";

    /// <summary>插件服务器地址</summary>
    [Description("插件服务器。将从该网页上根据关键字分析链接并下载插件")]
    public String PluginServer { get; set; } = "http://x.newlifex.com/";

    /// <summary>是否辅助解析程序集</summary>
    [Description("辅助解析程序集。程序集加载过程中，被依赖程序集未能解析时，是否协助解析，默认false")]
    public Boolean AssemblyResolve { get; set; }

    /// <summary>服务地址</summary>
    [Description("服务地址。用于内部构造其它Url或向注册中心登记，多地址逗号隔开")]
    public String ServiceAddress { get; set; } = String.Empty;

}

/// <summary>XXTraceSetting 的 AOT 序列化上下文</summary>
[JsonSerializable(typeof(LogLevel))]
[JsonSerializable(typeof(XXTraceSetting))]
public partial class XXTraceSettingJsonContext : JsonSerializerContext
{
}