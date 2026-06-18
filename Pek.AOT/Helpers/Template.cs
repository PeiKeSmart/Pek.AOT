using System.Text.RegularExpressions;

namespace Pek.Helpers;

/// <summary>模版引擎</summary>
public class Template
{
    private String Content { get; set; }

    /// <summary>模版引擎</summary>
    /// <param name="content"></param>
    public Template(String content)
    {
        Content = content;
    }

    /// <summary>设置变量</summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public Template Set(String key, String value)
    {
        Content = Content.Replace("{{" + key + "}}", value);
        return this;
    }

    /// <summary>渲染模板</summary>
    /// <returns></returns>
    public String Render()
    {
        var mc = Regex.Matches(Content, @"\{\{.+?\}\}");
        foreach (Match m in mc)
        {
            throw new ArgumentException($"模版变量{m.Value}未被使用");
        }

        return Content;
    }
}
