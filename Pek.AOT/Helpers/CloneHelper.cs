using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pek.Helpers;

/// <summary>深度克隆辅助类。AOT 注意：JsonSerializer 无源生成上下文时可能受裁剪影响，复杂类型需配合 JsonSerializerContext</summary>
public static class CloneHelper
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>深度克隆</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    public static T? DeepCloneWithJson<T>(this T source)
    {
        if (source == null) return default;

        var jsonString = JsonSerializer.Serialize(source, _jsonSerializerOptions);
        return JsonSerializer.Deserialize<T>(jsonString, _jsonSerializerOptions);
    }
}
