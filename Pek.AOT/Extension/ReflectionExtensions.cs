using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Pek;

/// <summary>系统扩展 - 反射（上游 Pek.Common DHExtensions.Reflection 迁移，AOT 安全子集）</summary>
public static partial class DHExtensions
{
    /// <summary>获取实例上的属性值</summary>
    /// <param name="member">成员信息</param>
    /// <param name="instance">成员所在的类实例</param>
    public static Object? GetPropertyValue(this MemberInfo member, Object instance)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        if (instance == null) throw new ArgumentNullException(nameof(instance));

        // AOT 安全：直接使用传入的 PropertyInfo 取值，避免按名反射查找（IL2075）；非 PropertyInfo 成员在裁剪下不保证元数据保留，返回 null
        if (member is PropertyInfo property)
            return property.GetValue(instance);

        return null;
    }
}
