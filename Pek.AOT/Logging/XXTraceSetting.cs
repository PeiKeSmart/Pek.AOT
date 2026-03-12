using System.Text.Json.Serialization;

using Pek.Configuration;

namespace Pek.Logging;

/// <summary>XXTrace 日志配置</summary>
public class XXTraceSetting : Config<XXTraceSetting>
{
    /// <summary>是否启用全局调试</summary>
    public Boolean Debug { get; set; } = true;

    /// <summary>日志等级</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>文件日志目录</summary>
    public String LogPath { get; set; } = "Log";

    /// <summary>日志文件上限，单位 MB，0 表示不限制</summary>
    public Int32 LogFileMaxBytes { get; set; } = 10;

    /// <summary>日志文件备份数量，0 表示不限制</summary>
    public Int32 LogFileBackups { get; set; } = 100;

    /// <summary>日志文件格式，支持 {0} 日期和 {1} 日志等级</summary>
    public String LogFileFormat { get; set; } = "{0:yyyy_MM_dd}.log";

    /// <summary>日志行格式</summary>
    public String LogLineFormat { get; set; } = "Time|ThreadId|Kind|Name|Message";

    /// <summary>网络日志地址，支持 udp、tcp、http、https</summary>
    public String NetworkLog { get; set; } = String.Empty;

    /// <summary>日志时间 UTC 校正小时数</summary>
    public Int32 UtcIntervalHours { get; set; }

    /// <summary>数据目录</summary>
    public String DataPath { get; set; } = "Data";

    /// <summary>备份目录</summary>
    public String BackupPath { get; set; } = "Backup";

    /// <summary>插件目录</summary>
    public String PluginPath { get; set; } = "Plugins";

    /// <summary>插件服务器地址</summary>
    public String PluginServer { get; set; } = "http://x.newlifex.com/";

    /// <summary>是否辅助解析程序集</summary>
    public Boolean AssemblyResolve { get; set; }

    /// <summary>服务地址</summary>
    public String ServiceAddress { get; set; } = String.Empty;

    /// <summary>静态构造函数，注册 AOT 配置</summary>
    static XXTraceSetting() => RegisterForAot<XXTraceSettingJsonContext>("Core");

    /// <summary>归一化当前配置中的空值和非法值</summary>
    public void Normalize()
    {
        if (String.IsNullOrWhiteSpace(LogPath)) LogPath = "Log";
        if (String.IsNullOrWhiteSpace(LogFileFormat)) LogFileFormat = "{0:yyyy_MM_dd}.log";
        if (String.IsNullOrWhiteSpace(LogLineFormat)) LogLineFormat = "Time|ThreadId|Kind|Name|Message";
        if (String.IsNullOrWhiteSpace(DataPath)) DataPath = "Data";
        if (String.IsNullOrWhiteSpace(BackupPath)) BackupPath = "Backup";
        if (String.IsNullOrWhiteSpace(PluginPath)) PluginPath = "Plugins";
        if (String.IsNullOrWhiteSpace(PluginServer)) PluginServer = "http://x.newlifex.com/";
        if (LogFileMaxBytes < 0) LogFileMaxBytes = 0;
        if (LogFileBackups < 0) LogFileBackups = 0;
    }

    /// <summary>保存前先归一化配置</summary>
    public override void Save()
    {
        Normalize();
        base.Save();
    }

    /// <summary>创建默认日志配置</summary>
    /// <returns>默认配置</returns>
    public static XXTraceSetting CreateDefault()
    {
        var setting = new XXTraceSetting();
        setting.Normalize();
        return setting;
    }
}

/// <summary>XXTraceSetting 的 AOT 序列化上下文</summary>
[JsonSerializable(typeof(LogLevel))]
[JsonSerializable(typeof(XXTraceSetting))]
public partial class XXTraceSettingJsonContext : JsonSerializerContext
{
}