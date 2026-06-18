// AOT: skipped - ActivatorHelper requires dynamic code (Activator.CreateInstance, Expression, ParameterInfo)
// Source: Pek.Common/Helpers/ActivatorHelper.cs
// Reason: Uses System.Linq.Expressions and ParameterInfo.HasDefaultValue for runtime type activation,
// which is fundamentally incompatible with NativeAOT trimming.
namespace Pek.Helpers;

/// <summary>AOT 占位：ActivatorHelper 依赖动态反射和表达式树，与 NativeAOT 不兼容</summary>
public static class ActivatorHelperPlaceholder
{
    // AOT: skipped - requires dynamic code
}
