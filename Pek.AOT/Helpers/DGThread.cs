// AOT: skipped - DGThread marked as dynamic code by user instruction
// Source: Pek.Common/Helpers/DGThread.cs
// Note: The original file contains some AOT-safe threading utilities (Sleep, ParallelExecute, ThreadPool)
// but is classified as "dynamic code" per migration categorization. If threading helpers are needed,
// review and migrate specific AOT-safe methods individually.
namespace Pek.Helpers;

/// <summary>AOT 占位：DGThread 按分类归为动态代码，与 NativeAOT 不兼容</summary>
public static class DGThreadPlaceholder
{
    // AOT: skipped - requires dynamic code
}
