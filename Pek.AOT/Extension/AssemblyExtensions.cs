using System.Reflection;

namespace Pek;

/// <summary>
/// 程序集(<see cref="Assembly"/>) 扩展
/// </summary>
public static class AssemblyExtensions
{
    #region GetFileVersion(获取程序集的文件版本)

    /// <summary>
    /// 获取程序集的文件版本
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns></returns>
    public static Version GetFileVersion(this Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        // AOT 安全：Assembly.Location 在单文件发布下为空（IL3000），改为读取程序集文件版本特性
        var attr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
        if (attr == null || !Version.TryParse(attr.Version, out var version))
            return new Version(0, 0);

        return version;
    }

    #endregion

    #region GetProductVersion(获取程序集的产品版本)

    /// <summary>
    /// 获取程序集的产品版本
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns></returns>
    public static Version GetProductVersion(this Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        // AOT 安全：Assembly.Location 在单文件发布下为空（IL3000），改为读取程序集信息版本特性
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var text = attr?.InformationalVersion;
        if (text == null)
            return new Version(0, 0);

        // InformationalVersion 可能带 +commit 或 - 后缀，仅取主版本段
        var index = text.IndexOfAny(['+', '-', ' ']);
        if (index > 0) text = text[..index];

        return Version.TryParse(text, out var version) ? version : new Version(0, 0);
    }

    #endregion
}
