// AOT: skipped - BinaryTree uses Expression.Compile(), Type.GetMethod() reflection,
// and Parallel.ForEach with dynamic expression trees.
// This file is for 24-point game style mathematical expression tree generation
// which fundamentally requires runtime expression compilation.
// Original source: DH.NCore/DH.NCore/Data/BinaryTree.cs
namespace Pek.Data;

/// <summary>二叉树 - AOT模式不支持，原实现依赖表达式树动态编译</summary>
public class BinaryTree
{
    /// <summary>高级操作符。如 Sqrt/Cbrt</summary>
    public IList<String> Operations { get; set; } = new List<String>();

    /// <summary>数学运算 - AOT模式不支持，需要 Expression.Compile()</summary>
    /// <param name="numbers"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public String[] Execute(Double[] numbers, Double result)
        => throw new NotSupportedException("BinaryTree requires Expression.Compile() which is not available in AOT mode.");
}
