namespace Pek.Webs;

/// <summary>
/// UserAgent操作辅助类
/// </summary>
public static class DHUserAgentHelper
{
    /// <summary>
    /// 操作系统字典
    /// </summary>
    public static IDictionary<String, String> OperationSystemDict { get; set; } = new Dictionary<String, String>()
        {
            {"NT 10.0","Windows 10" },
            {"NT 6.2","Windows 8" },
            {"NT 6.1","Windows 7" },
            {"NT 6.0","Windows Vista/Server 2008" },
            {"NT 5.2","Windows Server 2003" },
            {"NT 5.1","Windows XP" },
            {"NT 5.0","Windows 2000" },
            {"ME","Windows ME" },
            {"Mac","Mac" },
            {"Unix","UNIX" },
            {"Linux","Linux" },
            {"SunOs","Solaris" },
            {"FreeBSD","FreeBSD" },
        };

    /// <summary>
    /// 浏览器字典
    /// </summary>
    public static IDictionary<String, String> BrowserDict { get; set; } = new Dictionary<String, String>()
        {
            {"Maxthon","遨游浏览器" },
            {"MetaSr","搜狗高速浏览器" },
            {"BIDUBrowser","百度浏览器" },
            {"QQBrowser","QQ浏览器" },
            {"GreenBrowser","Green浏览器" },
            {"360se","360安全浏览器" },
            {"MSIE 6.0","Internet Explorer 6.0" },
            {"MSIE 7.0","Internet Explorer 7.0" },
            {"MSIE 8.0","Internet Explorer 8.0" },
            {"MSIE 9.0","Internet Explorer 9.0" },
            {"MSIE 10.0","Internet Explorer 10.0" },
            {"Firefox","Firefox" },
            {"Opera","Opera" },
            {"Chrome","Chrome" },
            {"Safari","Safari" },
        };

    #region GetOperatingSystemName(根据 UserAgent 获取操作系统名称)

    /// <summary>
    /// 根据 UserAgent 获取操作系统名称
    /// </summary>
    /// <param name="userAgent">UA</param>
    public static String GetOperatingSystemName(String userAgent)
    {
        foreach (var keyValue in OperationSystemDict)
        {
            if (userAgent.Contains(keyValue.Key))
                return keyValue.Value;
        }
        return "Other OperationSystem";
    }

    #endregion

    #region GetBrowserName(根据 UserAgent 获取浏览器名称)

    /// <summary>
    /// 根据 UserAgent 获取浏览器名称
    /// </summary>
    /// <param name="userAgent">UA</param>
    public static String GetBrowserName(String userAgent)
    {
        foreach (var keyValue in BrowserDict)
        {
            if (userAgent.Contains(keyValue.Key))
                return keyValue.Value;
        }
        return "Other Browser";
    }

    #endregion

    #region IsWechatBrowser(是否微信浏览器)

    /// <summary>
    /// 是否微信浏览器
    /// </summary>
    /// <param name="userAgent">UA</param>
    public static Boolean IsWechatBrowser(String userAgent) => userAgent.Contains("MicroMessenger");

    #endregion
}

/// <summary>
/// 用户代理信息
/// 参考地址：https://github.com/mumuy/browser/blob/master/Browser.js
/// </summary>
public class UserAgentInfo
{
    /// <summary>
    /// 浏览器
    /// </summary>
    public String Browser { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public String Version { get; set; }

    /// <summary>
    /// 内核
    /// </summary>
    public String Engine { get; set; }

    /// <summary>
    /// 操作系统
    /// </summary>
    public String Os { get; set; }

    /// <summary>
    /// 操作系统版本号
    /// </summary>
    public String OsVersion { get; set; }

    /// <summary>
    /// 设备
    /// </summary>
    public String Device { get; set; }

    /// <summary>
    /// 语言
    /// </summary>
    public String Language { get; set; }
}
