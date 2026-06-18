using System.Text.Json;

namespace Pek.Helpers;

/// <summary>常用公共操作</summary>
public static partial class Common
{
    #region GetType(获取类型)

    /// <summary>获取类型</summary>
    /// <typeparam name="T">类型</typeparam>
    public static Type GetType<T>() => GetType(typeof(T));

    /// <summary>获取类型</summary>
    /// <param name="type">类型</param>
    public static Type GetType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    #endregion

    /// <summary>Json数组字符串对象转为名值字典。使用 System.Text.Json（AOT 安全）</summary>
    /// <param name="listJson">Json数组字符串对象</param>
    /// <returns></returns>
    public static IDictionary<String, Object?> ToDictionary(this String listJson)
    {
        if (String.IsNullOrWhiteSpace(listJson)) return new Dictionary<String, Object?>();

        var dic = new Dictionary<String, Object?>();

        try
        {
            using var doc = JsonDocument.Parse(listJson);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            dic[prop.Name] = ConvertJsonElement(prop.Value);
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    dic[prop.Name] = ConvertJsonElement(prop.Value);
                }
            }
        }
        catch (JsonException)
        {
            // 非合法JSON，返回空字典
        }

        return dic;
    }

    private static Object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
