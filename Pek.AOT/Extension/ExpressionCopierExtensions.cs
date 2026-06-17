namespace Pek;

/// <summary>
/// 对象(<see cref="Object"/>) 扩展 - 表达式复制
/// </summary>
/// <remarks>
/// AOT: skipped - requires Expression.Compile() which is forbidden in NativeAOT.
/// The upstream implementation uses System.Linq.Expressions to compile a memberwise
/// copy lambda at runtime, which is incompatible with AOT trimming.
/// </remarks>
public static class ExpressionCopierExtensions
{
    // AOT: skipped - requires Expression.Compile()
    // 上游 Pek.Common 的 Extensions.Object.ExpressionCopier 使用 Expression.Compile()
    // 这在 NativeAOT 中不可用。若需要对象复制，可使用 source-gen 或手写映射。
}
