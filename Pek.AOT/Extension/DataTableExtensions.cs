namespace Pek;

/// <summary>
/// 数据表扩展方法（上游 Pek.Common Datas/DataTableExtensions 迁移）
/// </summary>
/// <remarks>
/// 整个文件未迁移。
/// 上游使用 DataTable、ConstructorInfo.Invoke() 等 API，在 NativeAOT 下均不可用：
/// 
/// 1. DataTable 在 NativeAOT 下不可用（需要动态列生成和序列化支持）。
/// 2. type.GetConstructors(...).Single(...).Invoke(null) 使用反射创建实例，
///    在 AOT 裁剪后可能找不到构造函数。
/// 3. property.GetSetMethod(true).Invoke(...) 使用反射设置属性值，
///    在 AOT 裁剪后可能被裁剪掉。
/// 
/// 如需 DataTable 与实体互转，请在非 AOT 项目中使用 Pek.Common 原始版本。
/// </remarks>
public static class DataTableExtensions
{
    // AOT: skipped - unsafe (DataTable + ConstructorInfo.Invoke + 动态反射)
}
