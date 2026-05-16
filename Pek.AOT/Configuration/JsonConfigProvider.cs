using Pek.IO;
using Pek.Serialization;
using Pek.Extension;

namespace Pek.Configuration;

/// <summary>Json文件配置提供者</summary>
/// <remarks>当前提供 appsettings.json 的根键/节读取能力，用于兼容旧调用链</remarks>
public class JsonConfigProvider : ConfigProvider
{
    /// <summary>配置文件名</summary>
    public String FileName { get; set; } = "appsettings.json";

    /// <summary>加载本地配置文件得到配置提供者</summary>
    /// <param name="fileName">配置文件名，默认appsettings.json</param>
    /// <returns>Json配置提供者实例</returns>
    public static JsonConfigProvider LoadAppSettings(String? fileName = null)
    {
        if (String.IsNullOrWhiteSpace(fileName)) fileName = "appsettings.json";

        return new JsonConfigProvider { FileName = fileName };
    }

    /// <summary>从数据源加载数据到配置树</summary>
    /// <returns>true 表示加载成功</returns>
    public override Boolean LoadAll()
    {
        var fileName = FileName;
        if (!String.IsNullOrWhiteSpace(fileName) && String.IsNullOrEmpty(Path.GetExtension(fileName))) fileName += ".json";

        fileName = fileName.GetBasePath();
        Root = new ConfigSection { Childs = [] };

        if (!File.Exists(fileName))
        {
            IsNew = true;
            return true;
        }

        var txt = File.ReadAllText(fileName);
        txt = TrimComment(txt);

        var src = txt.DecodeJson();
        if (src != null) Map(src, Root);

        IsNew = false;
        return true;
    }

    /// <summary>字典映射到配置树</summary>
    /// <param name="src">源字典</param>
    /// <param name="section">目标配置节</param>
    protected virtual void Map(IDictionary<String, Object?> src, IConfigSection section)
    {
        foreach (var item in src)
        {
            var name = item.Key;
            if (name.Length == 0 || name[0] == '#') continue;

            var cfg = section.GetOrAddChild(name);
            var cname = "#" + name;
            if (src.TryGetValue(cname, out var comment) && comment != null) cfg.Comment = comment + "";

            if (item.Value is IDictionary<String, Object?> dic)
            {
                cfg.Childs = [];
                Map(dic, cfg);
            }
            else if (item.Value is IList<Object> list)
            {
                MapList(list, cfg);
            }
            else
            {
                cfg.SetValue(item.Value);
            }
        }
    }

    private void MapList(IList<Object> list, IConfigSection cfg)
    {
        cfg.Childs = [];
        foreach (var elm in list)
        {
            if (elm is IDictionary<String, Object?> dic)
            {
                var cfg2 = new ConfigSection { Childs = [] };
                Map(dic, cfg2);
                cfg.Childs.Add(cfg2);
            }
            else
            {
                var key = elm?.GetType()?.Name;
                if (!String.IsNullOrEmpty(key))
                {
                    var cfg2 = new ConfigSection
                    {
                        Key = key,
                        Value = elm + "",
                    };
                    cfg.Childs.Add(cfg2);
                }
            }
        }
    }

    /// <summary>清理json字符串中的注释</summary>
    /// <param name="text">原始json文本</param>
    /// <returns>清理后的json文本</returns>
    public static String TrimComment(String text)
    {
        while (true)
        {
            var p = text.IndexOf("/*", StringComparison.Ordinal);
            if (p < 0) break;

            var p2 = text.IndexOf("*/", p + 2, StringComparison.Ordinal);
            if (p2 < 0) break;

            text = text[..p] + text[(p2 + 2)..];
        }

        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        text = lines
            .Where(e => !String.IsNullOrEmpty(e) && !e.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Join(Environment.NewLine);

        return text;
    }
}