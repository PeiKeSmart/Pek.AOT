using System.Text.RegularExpressions;

namespace Pek.FastToken;

/// <summary>模版引擎</summary>
/// <remarks>模版引擎</remarks>
/// <param name="content">模板内容</param>
public partial class Template(String content)
{
    private String Content { get; set; } = content;

    /// <summary>创建模板</summary>
    /// <param name="content">模板内容</param>
    public static Template Create(String content) => new(content);

    /// <summary>设置变量</summary>
    /// <param name="key">变量名</param>
    /// <param name="value">变量值</param>
    public Template Set(String key, String value)
    {
        Content = Content.Replace("{{" + key + "}}", value);
        return this;
    }

    /// <summary>渲染模板</summary>
    /// <param name="check">是否检查未使用的模板变量</param>
    public String Render(Boolean check = false)
    {
        if (check)
        {
            var mc = MyTemplateRegex.TemplateVarRegex().Matches(Content);
            foreach (Match m in mc)
            {
                throw new ArgumentException($"模版变量{m.Value}未被使用");
            }
        }

        return Content;
    }
}

/// <summary>模板源生成正则表达式（AOT安全）</summary>
internal static partial class MyTemplateRegex
{
    [GeneratedRegex(@"\{\{.+?\}\}")]
    internal static partial Regex TemplateVarRegex();
}
