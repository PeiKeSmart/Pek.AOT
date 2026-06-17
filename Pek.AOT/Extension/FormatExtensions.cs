using System.Globalization;
using System.Text;

namespace Pek;

/// <summary>格式化扩展。AOT 安全版</summary>
public static class FormatExtensions
{
    /// <summary>获取布尔值描述</summary>
    public static String Description(this Boolean value) => value ? "是" : "否";

    /// <summary>获取布尔值描述</summary>
    public static String Description(this Boolean? value) => value == null ? "" : Description(value.Value);

    /// <summary>格式化字符串，不依赖区域性</summary>
    public static String FormatInvariant(this String format, params Object[] args) => String.Format(CultureInfo.InvariantCulture, format, args);

    /// <summary>格式化字符串，依赖当前区域性</summary>
    public static String FormatCurrent(this String format, params Object[] args) => String.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>格式化字符串，依赖当前 UI 区域性</summary>
    public static String FormatCurrentUI(this String format, params Object[] args) => String.Format(CultureInfo.CurrentUICulture, format, args);

    /// <summary>格式化异常消息</summary>
    /// <param name="e">异常对象</param>
    /// <param name="isHideStackTrace">是否隐藏异常堆栈信息</param>
    public static String FormatMessage(this Exception e, Boolean isHideStackTrace = false)
    {
        var sb = new StringBuilder();
        var count = 0;
        var appString = String.Empty;
        while (e != null)
        {
            if (count > 0) appString += "  ";
            sb.AppendLine($"{appString}异常消息：{e.Message}");
            sb.AppendLine($"{appString}异常类型：{e.GetType().FullName}");
            sb.AppendLine($"{appString}异常方法：{(e.TargetSite == null ? null : e.TargetSite.Name)}");
            sb.AppendLine($"{appString}异常源：{e.Source}");
            if (!isHideStackTrace && e.StackTrace != null)
                sb.AppendLine($"{appString}异常堆栈：{e.StackTrace}");
            if (e.InnerException != null)
            {
                sb.AppendLine($"{appString}内部异常：");
                count++;
            }
            e = e.InnerException;
        }
        return sb.ToString();
    }
}
