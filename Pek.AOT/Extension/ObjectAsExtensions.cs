namespace Pek;

/// <summary>
/// 对象(<see cref="Object"/>) 扩展 - 类型转换
/// </summary>
public static class ObjectAsExtensions
{
    /// <summary>
    /// 转换为指定对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="this">object</param>
    public static T AsTo<T>(this Object @this) => (T)@this;

    /// <summary>
    /// 转换为指定对象（安全转换，失败返回默认值）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="this">当前对象</param>
    public static T? AsToOrDefault<T>(this Object @this)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <summary>
    /// 转换为指定对象（安全转换，失败返回指定默认值）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="this">object</param>
    /// <param name="defaultValue">默认值</param>
    public static T AsToOrDefault<T>(this Object @this, T defaultValue)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 转换为指定对象（安全转换，失败调用默认值工厂）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="this">当前对象</param>
    /// <param name="defaultValueFactory">默认值工厂</param>
    public static T AsToOrDefault<T>(this Object @this, Func<T> defaultValueFactory)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return defaultValueFactory();
        }
    }

    /// <summary>
    /// 转换为指定对象（安全转换，失败调用带参默认值工厂）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="this">object</param>
    /// <param name="defaultValueFactory">默认值工厂</param>
    public static T AsToOrDefault<T>(this Object @this, Func<Object, T> defaultValueFactory)
    {
        try
        {
            return (T)@this;
        }
        catch (Exception)
        {
            return defaultValueFactory(@this);
        }
    }
}
