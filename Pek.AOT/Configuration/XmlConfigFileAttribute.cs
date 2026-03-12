namespace Pek.Configuration;

/// <summary>Xml 配置文件特性</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class XmlConfigFileAttribute : Attribute
{
    /// <summary>配置文件名</summary>
    public String FileName { get; set; }

    /// <summary>指定配置文件名</summary>
    /// <param name="fileName">配置文件名</param>
    public XmlConfigFileAttribute(String fileName) => FileName = fileName;
}