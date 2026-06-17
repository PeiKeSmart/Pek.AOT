using System.Diagnostics.CodeAnalysis;

using System.Collections.Concurrent;

namespace Pek;

/// <summary>
/// 枚举扩展属性
/// </summary>
public static class EnumExtension
{
    private static ConcurrentDictionary<String, Dictionary<String, String>> _enumCache;

    private static ConcurrentDictionary<String, Dictionary<String, String>> EnumCache
    {
        get
        {
            if (_enumCache == null)
            {
                _enumCache = new ConcurrentDictionary<String, Dictionary<String, String>>();
            }
            return _enumCache;
        }
        set { _enumCache = value; }
    }

    /// <summary>
    /// 获得枚举提示文本
    /// </summary>
    /// <param name="en"></param>
    /// <returns></returns>
    public static String GetEnumText(this Enum en)
    {
        var enString = String.Empty;
        if (null == en) return enString;
        var type = en.GetType();
        enString = en.ToString();
        if (!EnumCache.ContainsKey(type.FullName!))
        {
            var fields = type.GetFields();
            var temp = new Dictionary<String, String>();
            foreach (var item in fields)
            {
                var attrs = item.GetCustomAttributes(typeof(TextAttribute), false);
                if (attrs.Length == 1)
                {
                    var v = ((TextAttribute)attrs[0]).Value;
                    temp.Add(item.Name, v);
                }
            }
            EnumCache.TryAdd(type.FullName!, temp);
        }
        if (EnumCache[type.FullName!].ContainsKey(enString))
        {
            return EnumCache[type.FullName!][enString];
        }
        return enString;
    }
}

/// <summary>
/// 文本特性
/// </summary>
public class TextAttribute : Attribute
{
    /// <summary>
    /// 实例化
    /// </summary>
    /// <param name="value"></param>
    public TextAttribute(String value)
    {
        Value = value;
    }

    /// <summary>
    /// 值
    /// </summary>
    public String Value { get; set; }
}
