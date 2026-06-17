using System.Reflection;

namespace Pek;

/// <summary>
/// 系统扩展 - 反射（上游 Pek.Common DHExtensions.Reflection 迁移，AOT 安全子集）
/// </summary>
/// <remarks>
/// AOT 兼容说明：
/// - GetPropertyValue 使用 GetProperty().GetValue() 反射调用，在 AOT 下可能因成员被裁剪而失败
/// - 但该方法为 MemberInfo 扩展，用途为从已知类型实例读取属性值，使用场景可控
/// - 若目标类型属性在 AOT 中被裁剪，该方法将返回 null 而非抛异常
/// </remarks>
public static partial class DHExtensions
{
    /// <summary>
    /// 获取实例上的属性值
    /// </summary>
    /// <param name="member">成员信息</param>
    /// <param name="instance">成员所在的类实例</param>
    public static Object? GetPropertyValue(this MemberInfo member, Object instance)
    {
        if (member == null)
            throw new ArgumentNullException(nameof(member));
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        return instance.GetType().GetProperty(member.Name)?.GetValue(instance);
    }
}
