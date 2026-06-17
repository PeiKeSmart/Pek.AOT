namespace Pek;

/// <summary>
/// 对象(<see cref="Object"/>) 扩展 —— 类型转换系列（上游 Pek.Common Extensions.Object.As 迁移）
/// </summary>
/// <remarks>
/// ClonePropertyFrom 系列方法未迁移：
/// 上游 Pek.Common Bases/ObjectExtensions.cs 中的 ClonePropertyFrom 方法依赖
/// Type.GetFields()、Type.GetProperties()、FieldInfo.SetValue()、PropertyInfo.GetValue()/SetValue()
/// 等动态反射 API，在 NativeAOT 下不受支持，无法等价替换。
/// 如需对象克隆，请在非 AOT 项目中使用 Pek.Common 的原始版本。
/// </remarks>
public static class ObjectExtensions
{
    /// <summary>
    /// 转换为指定对象
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="this">object</param>
    public static T AsTo<T>(this Object @this) => (T)@this;

    /// <summary>
    /// 转换为指定对象
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
    /// 转换为指定对象
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
    /// 转换为指定对象
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
    /// 转换为指定对象
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
