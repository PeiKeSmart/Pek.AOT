using System.Security.Claims;
using System.Security.Principal;

namespace Pek;

/// <summary>
/// 标识(<see cref="IIdentity"/>) 扩展
/// </summary>
public static class IdentityExtensions
{
    #region GetValue(获取指定类型的Claim值)

    /// <summary>
    /// 获取指定类型的Claim值
    /// </summary>
    /// <param name="identity">标识</param>
    /// <param name="type">类型</param>
    public static String GetValue(this IIdentity identity, String type)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));

        if (!(identity is ClaimsIdentity claimsIdentity))
            return null;
        return claimsIdentity.FindFirst(type)?.Value ?? String.Empty;
    }

    /// <summary>
    /// 获取指定类型的Claim值
    /// </summary>
    /// <typeparam name="T">泛型</typeparam>
    /// <param name="identity">标识</param>
    /// <param name="type">类型</param>
    public static T? GetValue<T>(this IIdentity identity, String type)
    {
        var result = identity.GetValue(type);
        // 上游依赖 Conv.CTo<T>，用 BCL Convert.ChangeType 等价替换
        if (result.IsEmpty()) return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    #endregion

    #region GetValues(获取指定类型的所有Claim值)

    /// <summary>
    /// 获取指定类型的所有Claim值
    /// </summary>
    /// <param name="identity">标识</param>
    /// <param name="type">类型</param>
    public static String[] GetValues(this IIdentity identity, String type)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));

        if (!(identity is ClaimsIdentity claimsIdentity))
            return null;
        return claimsIdentity.Claims.Where(x => x.Type == type).Select(x => x.Value).ToArray();
    }

    #endregion

    #region RemoveClaim(移除指定类型的声明)

    /// <summary>
    /// 移除指定类型的声明
    /// </summary>
    /// <param name="identity">标识</param>
    /// <param name="claimType">声明类型</param>
    public static void RemoveClaim(this IIdentity identity, String claimType)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));

        if (!(identity is ClaimsIdentity claimsIdentity))
            return;
        var claim = claimsIdentity.FindFirst(claimType);
        if (claim == null)
            return;
        claimsIdentity.RemoveClaim(claim);
    }

    #endregion
}
