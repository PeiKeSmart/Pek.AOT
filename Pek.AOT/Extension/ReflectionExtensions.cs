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

        // AOT: reflection access - may return null if property metadata is trimmed
        return instance.GetType().GetProperty(member.Name)?.GetValue(instance);
    }
}
