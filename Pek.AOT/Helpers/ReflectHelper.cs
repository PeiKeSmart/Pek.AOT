// AOT: skipped - ReflectHelper requires Assembly.GetAssemblies(), PropertyInfo, MethodInfo
// Source: Pek.Common/Helpers/ReflectHelper.cs
// Reason: Uses runtime assembly enumeration and type metadata inspection that is
// incompatible with trimmed NativeAOT applications.
namespace Pek.Helpers;

/// <summary>AOT 占位：ReflectHelper 依赖运行时程序集枚举和反射元数据，与 NativeAOT 不兼容</summary>
public static class ReflectHelperPlaceholder
{
    // AOT: skipped - requires dynamic code
}
