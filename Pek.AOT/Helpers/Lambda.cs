// AOT: skipped - Lambda requires Expression.Compile (System.Linq.Expressions)
// Source: Pek.Common/Helpers/Lambda.cs
// Reason: Uses Expression.Compile which performs runtime IL generation,
// completely unsupported in NativeAOT.
namespace Pek.Helpers;

/// <summary>AOT 占位：Lambda 依赖 Expression.Compile 动态 IL 生成，与 NativeAOT 不兼容</summary>
public static class LambdaPlaceholder
{
    // AOT: skipped - requires dynamic code
}
