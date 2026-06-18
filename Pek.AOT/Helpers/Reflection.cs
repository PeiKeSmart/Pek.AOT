using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Pek.Helpers;

/// <summary>反射操作。AOT 安全版</summary>
public static class Reflection
{
    #region GetDescription(获取类型描述)

    /// <summary>获取类型描述，使用<see cref="DescriptionAttribute"/>设置描述</summary>
    /// <typeparam name="T">类型</typeparam>
    public static String GetDescription<T>() => GetDescription(Common.GetType<T>());

    /// <summary>获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述</summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="memberName">成员名称</param>
    public static String GetDescription<T>(String memberName) => GetDescription(Common.GetType<T>(), memberName);

    /// <summary>获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述</summary>
    /// <param name="type">类型</param>
    /// <param name="memberName">成员名称</param>
    public static String GetDescription(Type type, String memberName)
    {
        if (type == null)
            return String.Empty;
        return memberName.IsEmpty()
            ? String.Empty
            : GetDescription(type.GetTypeInfo().GetMember(memberName).FirstOrDefault());
    }

    /// <summary>获取类型成员描述，使用<see cref="DescriptionAttribute"/>设置描述</summary>
    /// <param name="member">成员</param>
    public static String GetDescription(MemberInfo? member)
    {
        if (member == null)
            return String.Empty;
        return member.GetCustomAttribute<DescriptionAttribute>() is DescriptionAttribute attribute
            ? attribute.Description
            : member.Name;
    }

    #endregion

    #region IsDeriveClassFrom(判断当前类型是否可由指定类型派生)

    /// <summary>判断当前类型是否可由指定类型派生</summary>
    /// <typeparam name="TBaseType">基类型</typeparam>
    /// <param name="type">当前类型</param>
    /// <param name="canAbstract">能否是抽象类</param>
    public static Boolean IsDeriveClassFrom<TBaseType>(Type type, Boolean canAbstract = false) => IsDeriveClassFrom(type, typeof(TBaseType), canAbstract);

    /// <summary>判断当前类型是否可由指定类型派生</summary>
    /// <param name="type">当前类型</param>
    /// <param name="baseType">基类型</param>
    /// <param name="canAbstract">能否是抽象类</param>
    public static Boolean IsDeriveClassFrom(Type type, Type baseType, Boolean canAbstract = false)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (baseType == null) throw new ArgumentNullException(nameof(baseType));

        return type.IsClass && (!canAbstract && !type.IsAbstract) && type.IsBaseOn(baseType);
    }

    #endregion

    #region IsBaseOn(返回当前类型是否是指定基类的派生类)

    /// <summary>返回当前类型是否是指定基类的派生类</summary>
    /// <typeparam name="TBaseType">基类型</typeparam>
    /// <param name="type">类型</param>
    public static Boolean IsBaseOn<TBaseType>(Type type) => IsBaseOn(type, typeof(TBaseType));

    /// <summary>返回当前类型是否是指定基类的派生类</summary>
    /// <param name="type">类型</param>
    /// <param name="baseType">基类类型</param>
    public static Boolean IsBaseOn(Type type, Type baseType) => baseType.IsGenericTypeDefinition
        ? IsGenericAssignableFrom(baseType, type)
        : baseType.IsAssignableFrom(type);

    #endregion

    #region IsGenericAssignableFrom(判断当前泛型类型是否可由指定类型的实例填充)

    /// <summary>判断当前泛型类型是否可由指定类型的实例填充</summary>
    /// <param name="genericType">泛型类型</param>
    /// <param name="type">指定类型</param>
    public static Boolean IsGenericAssignableFrom(Type genericType, Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (genericType == null) throw new ArgumentNullException(nameof(genericType));

        if (!genericType.IsGenericType)
            throw new ArgumentException("该功能只支持泛型类型的调用，非泛型类型可使用 IsAssignableFrom 方法。");
        var allOthers = new List<Type>() { type };
        if (genericType.IsInterface) allOthers.AddRange(type.GetInterfaces());

        foreach (var other in allOthers)
        {
            var cur = other;
            while (cur != null)
            {
                if (cur.IsGenericType)
                    cur = cur.GetGenericTypeDefinition();
                if (cur.IsSubclassOf(genericType) || cur == genericType)
                    return true;
                cur = cur.BaseType;
            }
        }
        return false;
    }

    #endregion

    #region GetDisplayName(获取类型显示名称)

    /// <summary>获取类型显示名称，使用<see cref="DisplayNameAttribute"/>设置显示名称</summary>
    /// <typeparam name="T">类型</typeparam>
    public static String GetDisplayName<T>() => GetDisplayName(Common.GetType<T>());

    /// <summary>获取类型成员显示名称，使用<see cref="DisplayNameAttribute"/>或<see cref="DisplayAttribute"/>设置显示名称</summary>
    /// <param name="member">成员</param>
    private static String GetDisplayName(MemberInfo member)
    {
        if (member == null)
            return String.Empty;
        if (member.GetCustomAttribute<DisplayAttribute>() is DisplayAttribute displayAttribute)
            return displayAttribute.Name;
        if (member.GetCustomAttribute<DisplayNameAttribute>() is DisplayNameAttribute displayNameAttribute)
            return displayNameAttribute.DisplayName;
        return String.Empty;
    }

    #endregion

    #region GetDisplayNameOrDescription(获取显示名称或类型描述)

    /// <summary>获取类型显示名称或描述，使用<see cref="DescriptionAttribute"/>设置描述，使用<see cref="DisplayNameAttribute"/>设置显示名称</summary>
    /// <typeparam name="T">类型</typeparam>
    public static String GetDisplayNameOrDescription<T>() => GetDisplayNameOrDescription(Common.GetType<T>());

    /// <summary>获取类型显示名称或成员描述，使用<see cref="DescriptionAttribute"/>设置描述，使用<see cref="DisplayNameAttribute"/>或<see cref="DisplayAttribute"/>设置显示名称</summary>
    /// <param name="member">成员</param>
    public static String GetDisplayNameOrDescription(MemberInfo member)
    {
        var result = GetDisplayName(member);
        return String.IsNullOrWhiteSpace(result) ? Pek.Helpers.Reflection.GetDescription(member) : result;
    }

    #endregion

    // FindTypes / GetInstancesByInterface / GetAssembly / GetAssemblies 已删除
    // 原因：依赖运行时程序集枚举（Assembly.GetTypes / Assembly.Load），NativeAOT 不可用

    #region CreateInstance(动态创建实例)

    /// <summary>动态创建实例。AOT 安全版——委托到 ActivatorHelper</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="type">类型（必须与 T 一致）</param>
    /// <param name="parameters">传递给构造函数的参数</param>
    public static T? CreateInstance<T>(Type type, params Object[] parameters)
    {
        if (type != typeof(T))
            throw new ArgumentException($"Type mismatch: {type.FullName} cannot be cast to {typeof(T).FullName}", nameof(type));
        return ActivatorHelper.CreateInstance<T>(parameters);
    }

    /// <summary>动态创建实例。AOT 安全版——委托到 ActivatorHelper</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="className">类名，包括命名空间。AOT 下要求类型已注册</param>
    /// <param name="parameters">传递给构造函数的参数</param>
    public static T? CreateInstance<T>(String className, params Object[] parameters)
    {
        var type = Type.GetType(className) ?? Assembly.GetCallingAssembly().GetType(className);
        if (type == null)
            throw new InvalidOperationException($"Type '{className}' not found. Ensure it is preserved for AOT.");
        return CreateInstance<T>(type, parameters);
    }

    #endregion

    #region GetCurrentAssemblyName(获取当前程序集名称)

    /// <summary>获取当前程序集名称</summary>
    public static String GetCurrentAssemblyName() => Assembly.GetCallingAssembly().GetName().Name!;

    #endregion

    #region GetAttribute(获取特性信息)

    /// <summary>获取特性信息</summary>
    /// <typeparam name="TAttribute">泛型特性</typeparam>
    /// <param name="memberInfo">元数据</param>
    public static TAttribute? GetAttribute<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute => (TAttribute?)memberInfo.GetCustomAttributes(typeof(TAttribute), false).FirstOrDefault();

    #endregion

    #region GetAttributes(获取特性信息数据)

    /// <summary>获取特性信息数组</summary>
    /// <typeparam name="TAttribute">泛型特性</typeparam>
    /// <param name="memberInfo">元数据</param>
    public static TAttribute[] GetAttributes<TAttribute>(MemberInfo memberInfo) where TAttribute : Attribute => Array.ConvertAll(memberInfo.GetCustomAttributes(typeof(TAttribute), false), x => (TAttribute)x);

    #endregion

    #region GetPropertyInfo(获取属性信息)

    /// <summary>获取属性信息</summary>
    /// <param name="type">类型</param>
    /// <param name="propertyName">属性名</param>
    public static PropertyInfo? GetPropertyInfo(Type type, String propertyName) => type.GetProperties().FirstOrDefault(p => p.Name.Equals(propertyName));

    #endregion

    #region IsBool(是否布尔类型)

    /// <summary>是否布尔类型</summary>
    /// <param name="member">成员</param>
    public static Boolean IsBool(MemberInfo member)
    {
        if (member == null)
            return false;
        switch (member.MemberType)
        {
            case MemberTypes.TypeInfo:
                return member.ToString() == "System.Boolean";

            case MemberTypes.Property:
                return IsBool((PropertyInfo)member);
        }
        return false;
    }

    /// <summary>是否布尔类型</summary>
    /// <param name="property">属性</param>
    public static Boolean IsBool(PropertyInfo property) => property.PropertyType == typeof(Boolean) || property.PropertyType == typeof(Boolean?);

    #endregion

    #region IsEnum(是否枚举类型)

    /// <summary>是否枚举类型</summary>
    /// <param name="member">成员</param>
    public static Boolean IsEnum(MemberInfo member)
    {
        if (member == null)
            return false;
        switch (member.MemberType)
        {
            case MemberTypes.TypeInfo:
                return ((TypeInfo)member).IsEnum;

            case MemberTypes.Property:
                return IsEnum((PropertyInfo)member);
        }
        return false;
    }

    /// <summary>是否枚举类型</summary>
    /// <param name="property">属性</param>
    public static Boolean IsEnum(PropertyInfo property)
    {
        if (property.PropertyType.GetTypeInfo().IsEnum)
            return true;
        var value = Nullable.GetUnderlyingType(property.PropertyType);
        if (value == null)
            return false;
        return value.GetTypeInfo().IsEnum;
    }

    #endregion

    #region IsDate(是否日期类型)

    /// <summary>是否日期类型</summary>
    /// <param name="member">成员</param>
    public static Boolean IsDate(MemberInfo member)
    {
        if (member == null)
            return false;
        switch (member.MemberType)
        {
            case MemberTypes.TypeInfo:
                return member.ToString() == "System.DateTime";

            case MemberTypes.Property:
                return IsDate((PropertyInfo)member);
        }
        return false;
    }

    /// <summary>是否日期类型</summary>
    /// <param name="property">属性</param>
    public static Boolean IsDate(PropertyInfo property)
    {
        if (property.PropertyType == typeof(DateTime))
            return true;
        if (property.PropertyType == typeof(DateTime?))
            return true;
        return false;
    }

    #endregion

    #region IsInt(是否整型)

    /// <summary>是否整型</summary>
    /// <param name="member">成员</param>
    public static Boolean IsInt(MemberInfo member)
    {
        if (member == null)
            return false;
        switch (member.MemberType)
        {
            case MemberTypes.TypeInfo:
                return member.ToString() == "System.Int32" || member.ToString() == "System.Int16" ||
                       member.ToString() == "System.Int64";

            case MemberTypes.Property:
                return IsInt((PropertyInfo)member);
        }
        return false;
    }

    /// <summary>是否整型</summary>
    /// <param name="property">成员</param>
    public static Boolean IsInt(PropertyInfo property)
    {
        if (property.PropertyType == typeof(Int32))
            return true;
        if (property.PropertyType == typeof(Int32?))
            return true;
        if (property.PropertyType == typeof(Int16))
            return true;
        if (property.PropertyType == typeof(Int16?))
            return true;
        if (property.PropertyType == typeof(Int64))
            return true;
        if (property.PropertyType == typeof(Int64?))
            return true;
        return false;
    }

    #endregion

    #region IsNumber(是否数值类型)

    /// <summary>是否数值类型</summary>
    /// <param name="member">成员</param>
    public static Boolean IsNumber(MemberInfo member)
    {
        if (member == null)
            return false;
        if (IsInt(member))
            return true;
        switch (member.MemberType)
        {
            case MemberTypes.TypeInfo:
                return member.ToString() == "System.Double" || member.ToString() == "System.Decimal" ||
                       member.ToString() == "System.Single";

            case MemberTypes.Property:
                return IsNumber((PropertyInfo)member);
        }
        return false;
    }

    /// <summary>是否数值类型</summary>
    /// <param name="property">属性</param>
    public static Boolean IsNumber(PropertyInfo property)
    {
        if (property.PropertyType == typeof(Double))
            return true;
        if (property.PropertyType == typeof(Double?))
            return true;
        if (property.PropertyType == typeof(Decimal))
            return true;
        if (property.PropertyType == typeof(Decimal?))
            return true;
        if (property.PropertyType == typeof(Single))
            return true;
        if (property.PropertyType == typeof(Single?))
            return true;
        return false;
    }

    #endregion

    #region IsCollection(是否集合)

    /// <summary>是否集合</summary>
    /// <param name="type">类型</param>
    public static Boolean IsCollection(Type type) => type.IsArray || IsGenericCollection(type);

    #endregion

    #region IsGenericCollection(是否泛型集合)

    /// <summary>是否泛型集合</summary>
    /// <param name="type">类型</param>
    public static Boolean IsGenericCollection(Type type)
    {
        if (!type.IsGenericType)
            return false;
        var typeDefinition = type.GetGenericTypeDefinition();
        return typeDefinition == typeof(IEnumerable<>)
               || typeDefinition == typeof(IReadOnlyCollection<>)
               || typeDefinition == typeof(IReadOnlyList<>)
               || typeDefinition == typeof(ICollection<>)
               || typeDefinition == typeof(IList<>)
               || typeDefinition == typeof(List<>);
    }

    #endregion

    #region GetPublicProperties(获取公共属性列表)

    /// <summary>获取公共属性列表。AOT 下需确保实例类型的 PublicProperties 已保留</summary>
    /// <param name="instance">实例</param>
    public static List<Item> GetPublicProperties(Object instance)
    {
        var properties = instance.GetType().GetProperties();
        return properties.ToList().Select(t => new Item(t.Name, t.GetValue(instance))).ToList();
    }

    #endregion

    #region GetTopBaseType(获取顶级基类)

    /// <summary>获取顶级基类</summary>
    /// <typeparam name="T">类型</typeparam>
    public static Type? GetTopBaseType<T>() => GetTopBaseType(typeof(T));

    /// <summary>获取顶级基类</summary>
    /// <param name="type">类型</param>
    public static Type? GetTopBaseType(Type type)
    {
        if (type == null)
            return null;
        if (type.IsInterface)
            return type;
        if (type.BaseType == typeof(Object))
            return type;
        return GetTopBaseType(type.BaseType!);
    }

    #endregion

    #region GetElementType(获取元素类型)

    /// <summary>获取元素类型。如果是集合，返回集合的元素类型</summary>
    /// <param name="type">类型</param>
    public static Type GetElementType(Type type)
    {
        if (IsCollection(type) == false)
            return type;
        if (type.IsArray)
            return type.GetElementType()!;
        var genericArgumentsTypes = type.GetTypeInfo().GetGenericArguments();
        if (genericArgumentsTypes == null || genericArgumentsTypes.Length == 0)
            throw new ArgumentException("泛型类型参数不能为空");
        return genericArgumentsTypes[0];
    }

    #endregion

    #region GetImplementedGenericTypes(获取实现泛型类型)

    /// <summary>获取实现泛型类型</summary>
    /// <param name="givenType">给定类型</param>
    /// <param name="genericType">泛型类型</param>
    public static List<Type> GetImplementedGenericTypes(Type givenType, Type genericType)
    {
        var result = new List<Type>();
        AddImplementedGenericTypes(result, givenType, genericType);
        return result;
    }

    /// <summary>添加实现泛型类型</summary>
    /// <param name="result">结果</param>
    /// <param name="givenType">给定类型</param>
    /// <param name="genericType">泛型类型</param>
    private static void AddImplementedGenericTypes(List<Type> result, Type givenType, Type genericType)
    {
        var givenTypeInfo = givenType.GetTypeInfo();
        if (givenTypeInfo.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
            result.AddIfNotContains(givenType);
        foreach (var interfaceType in givenTypeInfo.GetInterfaces())
        {
            if (interfaceType.GetTypeInfo().IsGenericType && interfaceType.GetGenericTypeDefinition() == genericType)
                result.AddIfNotContains(interfaceType);
        }
        if (givenTypeInfo.BaseType == null)
            return;
        AddImplementedGenericTypes(result, givenTypeInfo.BaseType, genericType);
    }

    #endregion

    #region IsFunc(是否Func)

    /// <summary>是否Func</summary>
    /// <param name="obj">对象</param>
    public static Boolean IsFunc(Object obj)
    {
        if (obj == null)
            return false;
        var type = obj.GetType();
        if (!type.GetTypeInfo().IsGenericType)
            return false;
        return type.GetGenericTypeDefinition() == typeof(Func<>);
    }

    /// <summary>是否Func</summary>
    /// <typeparam name="TReturn">返回类型</typeparam>
    /// <param name="obj">对象</param>
    public static Boolean IsFunc<TReturn>(Object obj) => obj != null && obj.GetType() == typeof(Func<TReturn>);

    #endregion

    #region IsPrimitiveExtended(是否元数据扩展)

    /// <summary>是否元数据扩展</summary>
    /// <param name="type">类型</param>
    /// <param name="includeNullables">是否包含可空</param>
    /// <param name="includeEnums">是否包含枚举</param>
    public static Boolean IsPrimitiveExtended(Type type, Boolean includeNullables = true, Boolean includeEnums = false)
    {
        if (IsPrimitiveExtendedInternal(type, includeEnums))
            return true;
        if (includeNullables && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return IsPrimitiveExtendedInternal(type.GenericTypeArguments[0], includeEnums);
        return false;
    }

    /// <summary>是否内部元数据扩展</summary>
    /// <param name="type">类型</param>
    /// <param name="includeEnums">是否包含枚举</param>
    private static Boolean IsPrimitiveExtendedInternal(Type type, Boolean includeEnums)
    {
        if (type.IsPrimitive)
            return true;
        if (includeEnums && type.IsEnum)
            return true;
        return type == typeof(String) ||
               type == typeof(Decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid);
    }

    #endregion

    #region IsEnumerable(是否迭代集合)

    /// <summary>是否迭代集合</summary>
    /// <param name="type">类型</param>
    /// <param name="itemType">项类型</param>
    /// <param name="includePrimitives">是否包含元数据</param>
    public static Boolean IsEnumerable(Type type, out Type? itemType, Boolean includePrimitives = true)
    {
        if (!includePrimitives && IsPrimitiveExtended(type))
        {
            itemType = null;
            return false;
        }

        var enumerableTypes = GetImplementedGenericTypes(type, typeof(IEnumerable<>));
        if (enumerableTypes.Count == 1)
        {
            itemType = enumerableTypes[0].GenericTypeArguments[0];
            return true;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            itemType = typeof(Object);
            return true;
        }

        itemType = null;
        return false;
    }

    #endregion
}

/// <summary>名值对。用于 GetPublicProperties 返回值</summary>
public class Item
{
    /// <summary>名称</summary>
    public String Name { get; set; }

    /// <summary>值</summary>
    public Object? Value { get; set; }

    /// <summary>实例化</summary>
    public Item() { }

    /// <summary>实例化</summary>
    /// <param name="name">名称</param>
    /// <param name="value">值</param>
    public Item(String name, Object? value)
    {
        Name = name;
        Value = value;
    }
}
