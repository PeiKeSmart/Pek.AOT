using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Pek.Helpers;

/// <summary>类型成员缓存。AOT 安全版（含修剪注解）</summary>
public static class CacheUtil
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypePropertyCache = new();

    /// <summary>获取类型的所有公共属性（带缓存）</summary>
    public static PropertyInfo[] GetTypeProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
        => null == type ? throw new ArgumentNullException(nameof(type)) : TypePropertyCache.GetOrAdd(type, t => t.GetProperties());

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> TypeFieldCache = new();

    /// <summary>获取类型的所有公共字段（带缓存）</summary>
    public static FieldInfo[] GetTypeFields([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type type)
        => null == type ? throw new ArgumentNullException(nameof(type)) : TypeFieldCache.GetOrAdd(type, t => t.GetFields());

    internal static readonly ConcurrentDictionary<Type, MethodInfo[]> TypeMethodCache = new();

    internal static readonly ConcurrentDictionary<Type, Func<Object?, Object?>> TypeNewFuncCache = new();

    internal static readonly ConcurrentDictionary<Type, ConstructorInfo> TypeConstructorCache = new();
}
