using System.Collections.Specialized;
using System.Text;

namespace Pek;

/// <summary>
/// 键值对集合(<see cref="NameValueCollection"/>) 扩展
/// </summary>
public static class NameValueCollectionExtensions
{
    #region ToQueryString(将键值对集合转换成查询字符串)

    /// <summary>
    /// 将键值对集合转换成查询字符串
    /// </summary>
    /// <param name="collection">键值对集合</param>
    public static String ToQueryString(this NameValueCollection collection)
    {
        if (collection == null || !collection.HasKeys())
            return String.Empty;
        var sb = new StringBuilder();
        foreach (String key in collection.Keys)
            sb.Append($"{key}={collection[key]}&");
        // 上游依赖 StringBuilder.TrimEnd 扩展，用内联等价替代
        while (sb.Length > 0 && sb[sb.Length - 1] == '&')
            sb.Length--;
        return sb.ToString();
    }

    #endregion
}
