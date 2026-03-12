namespace Pek.Configuration;

/// <summary>Json 配置文件特性</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class JsonConfigFileAttribute : Attribute
{
    /// <summary>配置文件名</summary>
    public String FileName { get; set; }

    /// <summary>指定配置文件名</summary>
    /// <param name="fileName">配置文件名</param>
    public JsonConfigFileAttribute(String fileName) => FileName = fileName;
}