using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Pek;

/// <summary>
/// 反射扩展方法（上游 Pek.Common Reflections/ReflectionExtension 迁移，AOT 安全子集）
/// </summary>
/// <remarks>
/// AOT 兼容说明：
/// - 以下方法使用 GetCustomAttribute / IsDefined / 静态类型检查，均为元数据级别操作，AOT 安全
/// - GetValueGetter / GetValueSetter 使用 Expression.Compile()，已跳过
/// - InvokeMethod / SetFieldValue / GetFieldValue / SetPropertyValue / GetPropertyValue 
///   使用动态反射调用，在 AOT 裁剪后可能导致运行时失败，已跳过
/// - GetMethodBySignature / GetBaseMethod 使用常规反射查询，AOT 安全（不涉及动态生成）
/// </remarks>
public static class ReflectionExtension
{
    #region 方法签名匹配

    /// <summary>
    /// 按签名获取方法
    /// </summary>
    /// <param name="type">类型</param>
    /// <param name="method">方法</param>
    /// <returns></returns>
    public static MethodInfo? GetMethodBySignature(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] this Type type,
        MethodInfo method)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        if (method == null)
            throw new ArgumentNullException(nameof(method));

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(x => x.Name.Equals(method.Name))
            .ToArray();

        var parameterTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
        if (method.ContainsGenericParameters)
        {
            foreach (var info in methods)
            {
                var innerParams = info.GetParameters();
                if (innerParams.Length != parameterTypes.Length)
                    continue;

                var idx = 0;
                foreach (var param in innerParams)
                {
                    if (!param.ParameterType.IsGenericParameter
                        && !parameterTypes[idx].IsGenericParameter
                        && param.ParameterType != parameterTypes[idx])
                    {
                        break;
                    }

                    idx++;
                }
                if (idx < parameterTypes.Length)
                    continue;

                return info;
            }

            return null;
        }

        var baseMethod = type.GetMethod(method.Name, parameterTypes);
        return baseMethod;
    }

    /// <summary>
    /// 获取基类中的方法定义
    /// </summary>
    /// <param name="currentMethod">当前方法</param>
    /// <returns></returns>
    public static MethodInfo? GetBaseMethod(this MethodInfo currentMethod)
    {
        if (null == currentMethod?.DeclaringType?.BaseType)
            return null;

        return currentMethod.DeclaringType.BaseType.GetMethodBySignature(currentMethod);
    }

    #endregion

    #region 可见性检查

    /// <summary>
    /// 属性是否可见且为虚方法
    /// </summary>
    /// <param name="property">属性</param>
    /// <returns></returns>
    public static Boolean IsVisibleAndVirtual(this PropertyInfo property)
    {
        return property == null
            ? throw new ArgumentNullException(nameof(property))
            : (property.CanRead && property.GetMethod?.IsVisibleAndVirtual() == true) ||
               (property.CanWrite && property.GetMethod?.IsVisibleAndVirtual() == true);
    }

    /// <summary>
    /// 方法是否可见且为虚方法
    /// </summary>
    /// <param name="method">方法</param>
    /// <returns></returns>
    public static Boolean IsVisibleAndVirtual(this MethodInfo method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        if (method.IsStatic || method.IsFinal)
            return false;
        return method.IsVirtual &&
               (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
    }

    /// <summary>
    /// 方法是否可见
    /// </summary>
    /// <param name="method">方法</param>
    /// <returns></returns>
    public static Boolean IsVisible(this MethodBase method)
    {
        if (method == null) throw new ArgumentNullException(nameof(method));

        return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    #endregion

    #region 自定义特性

    /// <summary>
    /// 获取显示名称（优先 DisplayNameAttribute，其次 DisplayAttribute，最后 MemberInfo.Name）
    /// </summary>
    /// <param name="this">成员信息</param>
    /// <returns></returns>
    public static String GetDisplayName([NotNull] this MemberInfo @this)
        => @this.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
        ?? @this.GetCustomAttribute<DisplayAttribute>()?.Name
        ?? @this.Name;

    /// <summary>
    /// 获取列名（优先 ColumnAttribute.Name，其次属性名）
    /// </summary>
    /// <param name="propertyInfo">属性信息</param>
    /// <returns></returns>
    public static String GetColumnName([NotNull] this PropertyInfo propertyInfo)
        => propertyInfo.GetCustomAttribute<ColumnAttribute>()?.Name ?? propertyInfo.Name;

    /// <summary>
    /// 获取描述
    /// </summary>
    /// <param name="this">成员信息</param>
    /// <returns></returns>
    public static String GetDescription([NotNull] this MemberInfo @this)
        => @this.GetCustomAttribute<DescriptionAttribute>()?.Description ?? String.Empty;

    #endregion

    #region 类型判断（AOT 安全，仅使用 typeof/is 检查）

    /// <summary>
    /// 是否有无参构造函数（委托给 Type 扩展方法）
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="this">实例</param>
    /// <returns></returns>
    // AOT: skipped - unsafe (依赖动态反射 HasEmptyConstructor)
    // public static Boolean HasEmptyConstructor<T>(this T @this) => typeof(T).HasEmptyConstructor();

    /// <summary>
    /// 是否是 ValueTuple
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    public static Boolean IsValueTuple<T>(this T t) => typeof(T).IsValueTuple();

    /// <summary>
    /// 是否是值类型
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    public static Boolean IsValueType<T>(this T t) => typeof(T).IsValueType;

    /// <summary>
    /// 是否是数组
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="this">实例</param>
    /// <returns></returns>
    public static Boolean IsArray<T>(this T @this) => @this!.GetType().IsArray;

    /// <summary>
    /// 是否是类
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="this">实例</param>
    /// <returns></returns>
    public static Boolean IsClass<T>(this T @this) => @this!.GetType().IsClass;

    /// <summary>
    /// 是否是枚举
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    public static Boolean IsEnum<T>(this T @this) => typeof(T).IsEnum;

    /// <summary>
    /// 是否是子类
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="this">实例</param>
    /// <param name="type">基类型</param>
    /// <returns></returns>
    public static Boolean IsSubclassOf<T>(this T @this, Type type) => typeof(T).IsSubclassOf(type);

    #endregion

    #region 特性检查

    /// <summary>
    /// 是否定义了指定特性
    /// </summary>
    /// <param name="this">实例</param>
    /// <param name="attributeType">特性类型</param>
    /// <param name="inherit">是否继承</param>
    /// <returns></returns>
    public static Boolean IsAttributeDefined([NotNull] this Object @this, Type attributeType, Boolean inherit = true)
        => @this.GetType().IsDefined(attributeType, inherit);

    /// <summary>
    /// 是否定义了指定特性
    /// </summary>
    /// <typeparam name="T">特性类型</typeparam>
    /// <param name="this">实例</param>
    /// <param name="inherit">是否继承</param>
    /// <returns></returns>
    public static Boolean IsAttributeDefined<T>([NotNull] this Object @this, Boolean inherit = true) where T : Attribute
        => @this.GetType().IsDefined(typeof(T), inherit);

    #endregion

    #region 程序集特性

    /// <summary>
    /// 获取程序集上的特性
    /// </summary>
    /// <typeparam name="T">特性类型</typeparam>
    /// <param name="this">程序集</param>
    /// <returns></returns>
    public static T? GetAttribute<T>(this Assembly @this) where T : Attribute
    {
        var configAttributes = Attribute.GetCustomAttributes(@this, typeof(T), false);

        if (configAttributes != null && configAttributes.Length > 0)
            return (T)configAttributes[0];

        return null;
    }

    /// <summary>
    /// 获取程序集上的自定义特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="attributeType">特性类型</param>
    /// <returns></returns>
    public static Attribute? GetCustomAttribute([NotNull] this Assembly element, Type attributeType)
        => Attribute.GetCustomAttribute(element, attributeType);

    /// <summary>
    /// 获取程序集上的自定义特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="attributeType">特性类型</param>
    /// <param name="inherit">是否继承</param>
    /// <returns></returns>
    public static Attribute? GetCustomAttribute([NotNull] this Assembly element, Type attributeType, Boolean inherit)
        => Attribute.GetCustomAttribute(element, attributeType, inherit);

    /// <summary>
    /// 获取程序集上的所有自定义特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="attributeType">特性类型</param>
    /// <returns></returns>
    public static Attribute[] GetCustomAttributes([NotNull] this Assembly element, Type attributeType)
        => Attribute.GetCustomAttributes(element, attributeType);

    /// <summary>
    /// 获取程序集上的所有自定义特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="attributeType">特性类型</param>
    /// <param name="inherit">是否继承</param>
    /// <returns></returns>
    public static Attribute[] GetCustomAttributes([NotNull] this Assembly element, Type attributeType, Boolean inherit)
        => Attribute.GetCustomAttributes(element, attributeType, inherit);

    /// <summary>
    /// 获取程序集上的所有自定义特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <returns></returns>
    public static Attribute[] GetCustomAttributes([NotNull] this Assembly element)
        => Attribute.GetCustomAttributes(element);

    /// <summary>
    /// 获取程序集上的所有自定义特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="inherit">是否继承</param>
    /// <returns></returns>
    public static Attribute[] GetCustomAttributes([NotNull] this Assembly element, Boolean inherit)
        => Attribute.GetCustomAttributes(element, inherit);

    /// <summary>
    /// 判断程序集上是否定义了指定特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="attributeType">特性类型</param>
    /// <returns></returns>
    public static Boolean IsDefined([NotNull] this Assembly element, Type attributeType)
        => Attribute.IsDefined(element, attributeType);

    /// <summary>
    /// 判断程序集上是否定义了指定特性
    /// </summary>
    /// <param name="element">程序集</param>
    /// <param name="attributeType">特性类型</param>
    /// <param name="inherit">是否继承</param>
    /// <returns></returns>
    public static Boolean IsDefined([NotNull] this Assembly element, Type attributeType, Boolean inherit)
        => Attribute.IsDefined(element, attributeType, inherit);

    #endregion

    // 以下方法未迁移（AOT 不安全）：
    //
    // GetField<T>(name) / GetField<T>(name, bindingAttr) - 依赖 Pek.Helpers.CacheUtil（AOT 中不可用）
    // GetFields / GetFields(bindingAttr) - 同上
    // GetFieldValue<T> - 依赖 FieldInfo.GetValue（动态反射，AOT 裁剪后可能失败）
    // GetMethod<T> / GetMethods<T> / GetProperty<T> / GetProperties - 依赖 CacheUtil
    // GetPropertyValue<T> - 依赖 Expression.Compile
    // SetFieldValue<T> - 依赖 FieldInfo.SetValue（动态反射）
    // SetPropertyValue<T> - 依赖 Expression.Compile
    // GetValueGetter / GetValueSetter - 依赖 Expression.Compile()（AOT 禁止）
    // InvokeMethod - 依赖 MethodInfo.Invoke（动态反射）
    // GetPropertyOrField - 组合使用上述方法
}
