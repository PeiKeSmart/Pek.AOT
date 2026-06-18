using System.ComponentModel;
using System.Text.Json.Serialization;
using Pek.Configuration;
using Pek.Extension;
using Pek.Log;
using Pek.Net;
using Pek.Security;

namespace Pek.Common;

/// <summary>系统设置。提供系统名称、版本等基本设置</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/sysconfig
/// AOT 适配：移除 AssemblyX 反射依赖，改用配置初始化 + 静态构造函数。
/// </remarks>
[DisplayName("系统设置")]
[Config("Sys")]
public class SysConfig : Config<SysConfig, SysConfigJsonContext>
{
    #region 属性
    /// <summary>系统名称</summary>
    [DisplayName("系统名称")]
    [Description("用于标识系统的英文名，不能有空格")]
    public String Name { get; set; } = "";

    /// <summary>系统版本</summary>
    [DisplayName("系统版本")]
    public String Version { get; set; } = "";

    /// <summary>显示名称</summary>
    [DisplayName("显示名称")]
    [Description("用户可见的名称")]
    public String DisplayName { get; set; } = "";

    /// <summary>公司</summary>
    [DisplayName("公司")]
    public String Company { get; set; } = "";

    /// <summary>应用实例。单应用多实例部署时用于唯一标识实例节点</summary>
    [DisplayName("应用实例。单应用多实例部署时用于唯一标识实例节点")]
    public Int32 Instance { get; set; }

    /// <summary>开发者模式</summary>
    [DisplayName("开发者模式")]
    public Boolean Develop { get; set; } = true;

    /// <summary>启用</summary>
    [DisplayName("启用")]
    public Boolean Enable { get; set; } = true;

    /// <summary>安装时间</summary>
    [DisplayName("安装时间")]
    public DateTime InstallTime { get; set; } = DateTime.Now;
    #endregion

    #region 方法
    /// <summary>加载后触发</summary>
    protected override void OnLoaded()
    {
        if (IsNew)
        {
            Name = "Pek.System";
            Version = "1.0";
            DisplayName = "系统设置";
            Company = "湖北登灏科技有限公司";
        }

        // 强制设置
        var name = GetSysName();
        if (!name.IsNullOrEmpty()) Name = name;

        // 本地实例，取IPv4地址后两段
        if (Instance <= 0)
        {
            try
            {
                var ip = NetHelper.MyIP();
                if (ip != null)
                {
                    var buf = ip.GetAddressBytes();
                    Instance = (buf[2] << 8) | buf[3];
                }
                else
                {
                    Instance = Rand.Next(1, 1024);
                }
            }
            catch
            {
                // 异常时随机
                Instance = Rand.Next(1, 1024);
            }
        }

        base.OnLoaded();
    }

    /// <summary>获取系统名</summary>
    /// <returns>系统名称；未设置时返回 null</returns>
    public static String? GetSysName()
    {
        // 从命令参数或环境变量获取系统名称，强制覆盖SysConfig，方便星尘发布根据命令行控制系统名称
        var name = "";
        // 命令参数
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].EqualIgnoreCase("-Name") && i + 1 < args.Length)
            {
                name = args[i + 1];
                break;
            }
        }

        // 环境变量
        if (name.IsNullOrEmpty()) name = Runtime.GetEnvironmentVariable("Name");

        return name;
    }
    #endregion
}

/// <summary>SysConfig 的 AOT 序列化上下文</summary>
[JsonSerializable(typeof(SysConfig))]
public partial class SysConfigJsonContext : JsonSerializerContext
{
}
