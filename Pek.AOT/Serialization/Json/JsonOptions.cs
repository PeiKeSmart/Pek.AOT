namespace Pek.Serialization;

/// <summary>Json序列化选项</summary>
public class JsonOptions
{
    /// <summary>使用驼峰命名。默认false</summary>
    public Boolean CamelCase { get; set; }

    /// <summary>忽略空值。默认false</summary>
    public Boolean IgnoreNullValues { get; set; }

    /// <summary>忽略循环引用。默认false</summary>
    public Boolean IgnoreCycles { get; set; }

    /// <summary>缩进。默认false</summary>
    public Boolean WriteIndented { get; set; }

    /// <summary>使用完整的时间格式。默认false</summary>
    public Boolean FullTime { get; set; }

    /// <summary>枚举使用字符串。默认false</summary>
    public Boolean EnumString { get; set; }

    /// <summary>长整型作为字符串序列化。默认false</summary>
    public Boolean Int64AsString { get; set; }

    /// <summary>默认构造函数</summary>
    public JsonOptions() { }

    /// <summary>复制构造函数</summary>
    /// <param name="jsonOptions">源选项</param>
    public JsonOptions(JsonOptions jsonOptions)
    {
        if (jsonOptions == null) throw new ArgumentNullException(nameof(jsonOptions));

        CamelCase = jsonOptions.CamelCase;
        IgnoreNullValues = jsonOptions.IgnoreNullValues;
        IgnoreCycles = jsonOptions.IgnoreCycles;
        WriteIndented = jsonOptions.WriteIndented;
        FullTime = jsonOptions.FullTime;
        EnumString = jsonOptions.EnumString;
        Int64AsString = jsonOptions.Int64AsString;
    }
}