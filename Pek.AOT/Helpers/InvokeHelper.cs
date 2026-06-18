// AOT: skipped - InvokeHelper requires dynamic reflection (MethodInfo.Invoke patterns)
// Source: Pek.Common/Helpers/InvokeHelper.cs
// Reason: Uses runtime method invocation patterns that rely on reflection metadata
// which is trimmed in NativeAOT.
namespace Pek.Helpers;

/// <summary>AOT 占位：InvokeHelper 依赖反射调用模式，与 NativeAOT 不兼容</summary>
public static class InvokeHelperPlaceholder
{
    // AOT: skipped - requires dynamic code
}
