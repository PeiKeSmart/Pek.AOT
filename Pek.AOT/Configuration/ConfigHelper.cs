using System.Globalization;

using Pek.Extension;

namespace Pek.Configuration;

/// <summary>配置助手</summary>
public static class ConfigHelper
{
    /// <summary>查找配置项</summary>
    /// <param name="section">起始配置节</param>
    /// <param name="key">键路径，冒号分隔</param>
    /// <param name="createOnMiss">当不存在时是否自动创建</param>
    /// <returns>匹配配置节；未找到且不创建时返回 null</returns>
    public static IConfigSection? Find(this IConfigSection section, String key, Boolean createOnMiss = false)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        if (key.IsNullOrEmpty()) return section;

        var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var current = section;

        foreach (var part in parts)
        {
            var child = current.Childs?.FirstOrDefault(e => e.Key.EqualIgnoreCase(part));
            if (child == null)
            {
                if (!createOnMiss) return null;
                child = current.AddChild(part);
            }

            current = child;
        }

        return current;
    }

    /// <summary>添加子节点</summary>
    /// <param name="section">父配置节</param>
    /// <param name="key">子节点键名</param>
    /// <returns>创建的子配置节</returns>
    public static IConfigSection AddChild(this IConfigSection section, String key)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        if (key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(key));

        var child = new ConfigSection { Key = key };
        section.Childs ??= [];
        section.Childs.Add(child);

        return child;
    }

    /// <summary>查找或添加子节点</summary>
    /// <param name="section">父配置节</param>
    /// <param name="key">子节点键名</param>
    /// <returns>已存在或新建的子配置节</returns>
    public static IConfigSection GetOrAddChild(this IConfigSection section, String key)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        if (key.IsNullOrEmpty()) throw new ArgumentNullException(nameof(key));

        var child = section.Childs?.FirstOrDefault(e => e.Key.EqualIgnoreCase(key));
        return child ?? section.AddChild(key);
    }

    /// <summary>设置节点值</summary>
    /// <param name="section">目标配置节</param>
    /// <param name="value">待设置的值</param>
    public static void SetValue(this IConfigSection section, Object? value)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));

        if (value is DateTime dateTime)
            section.Value = dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        else if (value is Boolean boolean)
            section.Value = boolean ? "true" : "false";
        else if (value is Enum)
            section.Value = value.ToString();
        else if (value is IFormattable formattable)
            section.Value = formattable.ToString(null, CultureInfo.InvariantCulture);
        else
            section.Value = value?.ToString();
    }
}