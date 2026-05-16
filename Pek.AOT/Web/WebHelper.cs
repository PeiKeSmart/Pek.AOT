using System.Text;

using Pek.Extension;

namespace Pek.Web;

/// <summary>Web辅助类</summary>
public static class WebHelper
{
    #region Url扩展
    /// <summary>追加Url参数，不为空时加与符号</summary>
    /// <param name="sb"></param>
    /// <param name="str"></param>
    /// <returns></returns>
    public static StringBuilder UrlParam(this StringBuilder sb, String str)
    {
        if (str.IsNullOrWhiteSpace()) return sb;

        if (sb.Length > 0) sb.Append("&");

        sb.Append(str);

        return sb;
    }

    /// <summary>追加Url参数，不为空时加与符号</summary>
    /// <param name="sb">字符串构建</param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static StringBuilder UrlParam(this StringBuilder sb, String name, Object value)
    {
        if (name.IsNullOrWhiteSpace()) return sb;

        return UrlParam(sb, $"{name}={value}");
    }

    /// <summary>把一个参数字典追加Url参数，指定包含的参数</summary>
    /// <param name="sb">字符串构建</param>
    /// <param name="pms">参数字典</param>
    /// <param name="includes">包含的参数</param>
    /// <returns></returns>
    public static StringBuilder UrlParams(this StringBuilder sb, IDictionary<String, String> pms, params String[] includes)
    {
        foreach (var item in pms)
        {
            if (item.Key.EqualIgnoreCase(includes)) sb.UrlParam(item.Key, item.Value);
        }

        return sb;
    }

    /// <summary>把一个参数字典追加Url参数，排除一些参数</summary>
    /// <param name="sb">字符串构建</param>
    /// <param name="pms">参数字典</param>
    /// <param name="excludes">要排除的参数</param>
    /// <returns></returns>
    public static StringBuilder UrlParamsExcept(this StringBuilder sb, IDictionary<String, String> pms, params String[] excludes)
    {
        foreach (var item in pms)
        {
            if (!item.Key.EqualIgnoreCase(excludes)) sb.UrlParam(item.Key, item.Value);
        }

        return sb;
    }
    #endregion
}