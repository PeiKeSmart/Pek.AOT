// AOT: skipped - Reflection requires Type.GetMember, DescriptionAttribute, and reflection-based metadata
// Source: Pek.Common/Helpers/Reflection.cs
// Reason: Uses runtime type member discovery via reflection which relies on metadata
// that is trimmed by NativeAOT.
namespace Pek.Helpers;

/// <summary>AOT 占位：Reflection 依赖运行时类型成员发现，与 NativeAOT 裁剪不兼容</summary>
public static class ReflectionPlaceholder
{
    // AOT: skipped - requires dynamic code
}
