namespace Pek.Helpers;

/// <summary>守卫辅助类</summary>
public static class Guard
{
    /// <summary>非空断言</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="t"></param>
    /// <param name="paramName"></param>
    /// <returns></returns>
    public static T NotNull<T>(T t, String paramName)
    {
        if (t is null) throw new ArgumentNullException(paramName);
        return t;
    }

    /// <summary>非空字符串断言</summary>
    /// <param name="str"></param>
    /// <param name="paramName"></param>
    /// <returns></returns>
    public static String NotNullOrEmpty(String str, String paramName)
    {
        NotNull(str, paramName);
        if (String.IsNullOrEmpty(str)) throw new ArgumentNullException(paramName);
        return str;
    }
}
