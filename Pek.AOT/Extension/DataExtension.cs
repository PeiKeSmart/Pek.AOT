namespace Pek;

/// <summary>
/// 数据扩展方法（上游 Pek.Common Datas/DataExtension 迁移）
/// </summary>
/// <remarks>
/// 整个文件未迁移，原因如下：
/// 
/// 1. 核心依赖 System.Data（DataTable、IDataReader、DbCommand、DbConnection、DbParameter 等），
///    DataTable 在 NativeAOT 下不可用（需要动态列生成和序列化支持）。
/// 
/// 2. NewFuncHelper&lt;T&gt; 使用 Expression.Lambda(...).Compile() 创建实例工厂，
///    表达式树编译在 NativeAOT 下被禁止。
/// 
/// 3. GetValueGetter / GetValueSetter 等属性访问器委托同样依赖 Expression.Compile()。
/// 
/// 4. AttachDbParameters 方法使用反射获取字段/属性值，在 AOT 裁剪后可能导致运行时失败。
/// 
/// 如需 DataTable/IDataReader 相关扩展，请在非 AOT 项目中使用 Pek.Common 原始版本。
/// </remarks>
public static class DataExtension
{
    // AOT: skipped - unsafe (System.Data + Expression.Compile + 动态反射)
}
