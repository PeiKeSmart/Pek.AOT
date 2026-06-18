// AOT: skipped - CacheUtil requires heavy reflection (Type.GetProperties, FieldInfo, ConstructorInfo, Func<Object[], Object>)
// The type metadata caching pattern with PropertyInfo/FieldInfo/ConstructorInfo delegates
// is fundamentally incompatible with NativeAOT trimming.
// Source: Pek.Common/Helpers/CacheUtil.cs
namespace Pek.Helpers;

/// <summary>AOT 占位：原始 CacheUtil 依赖大量反射 API，与 NativeAOT 不兼容</summary>
public static class CacheUtil
{
    // AOT: skipped - requires dynamic code
}
