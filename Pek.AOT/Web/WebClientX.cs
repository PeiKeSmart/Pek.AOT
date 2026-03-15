using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

using Pek.Buffers;
using Pek.Extension;
using Pek.Http;
using Pek.Log;
using Pek.Security;

namespace Pek.Web;

/// <summary>扩展的Web客户端</summary>
public class WebClientX : DisposeBase
{
    private HttpClient? _client;
    private String? _lastAddress;
    private Dictionary<String, String>? _cookies;

    /// <summary>超时，默认30000毫秒</summary>
    public Int32 Timeout { get; set; } = 30_000;

    /// <summary>要求的最低版本</summary>
    public Version? MinVersion { get; set; }

    /// <summary>最后使用的链接</summary>
    public Link? LastLink { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    static WebClientX()
    {
#if !NET9_0_OR_GREATER
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }
        catch { }
#endif
    }

    /// <summary>销毁</summary>
    /// <param name="disposing">是否显式销毁</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        _client.TryDispose();
    }

    /// <summary>创建客户端会话</summary>
    /// <returns>客户端</returns>
    public virtual HttpClient EnsureCreate()
    {
        var client = _client;
        if (client == null)
        {
            client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Timeout) };
            client.SetUserAgent();
            _client = client;
        }

        return client;
    }

    /// <summary>发送请求，获取响应</summary>
    /// <param name="address">地址</param>
    /// <param name="content">内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应内容</returns>
    public virtual async Task<HttpContent> SendAsync(String address, HttpContent? content = null, CancellationToken cancellationToken = default)
    {
        var client = EnsureCreate();
        Log.Info("{2}.{1} {0}", address, content != null ? "Post" : "Get", GetType().Name);

        var request = new HttpRequestMessage
        {
            Method = content != null ? HttpMethod.Post : HttpMethod.Get,
            RequestUri = new Uri(address),
            Content = content,
        };

        if (!_lastAddress.IsNullOrEmpty()) request.Headers.Referrer = new Uri(_lastAddress);
        if (_cookies != null && _cookies.Count > 0) request.Headers.Add("Cookie", _cookies.Select(item => $"{item.Key}={item.Value}"));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource(Timeout).Token, cancellationToken);
        var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        _lastAddress = address;
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            _cookies ??= [];
            foreach (var cookie in setCookies)
            {
                var p1 = cookie.IndexOf('=');
                var p2 = cookie.IndexOf(';');
                if (p1 > 0)
                {
                    var key = cookie[..p1];
                    var value = p2 > p1 ? cookie[(p1 + 1)..p2] : cookie[(p1 + 1)..];
                    _cookies[key] = value;
                }
            }
        }

        return response.Content;
    }

    /// <summary>获取字符串</summary>
    /// <param name="address">地址</param>
    /// <returns>字符串</returns>
    public virtual async Task<String> DownloadStringAsync(String address)
    {
        using var content = await SendAsync(address).ConfigureAwait(false);
        var bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);

        var charset = content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try
        {
            encoding = !charset.IsNullOrEmpty() ? Encoding.GetEncoding(charset) : Encoding.UTF8;
        }
        catch
        {
            encoding = Encoding.UTF8;
        }

        return encoding.GetString(bytes);
    }

    /// <summary>下载文件</summary>
    /// <param name="address">地址</param>
    /// <param name="file">目标文件</param>
    /// <returns>任务</returns>
    public virtual async Task DownloadFileAsync(String address, String file)
    {
        file.EnsureDirectory();
        using var content = await SendAsync(address).ConfigureAwait(false);
        using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using var fileStream = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read);
        await stream.CopyToAsync(fileStream).ConfigureAwait(false);
        await fileStream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>获取指定地址的Html</summary>
    /// <param name="url">地址</param>
    /// <returns>Html文本</returns>
    public String GetHtml(String url) => DownloadStringAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>获取指定地址的Html，分析所有超链接</summary>
    /// <param name="url">地址</param>
    /// <returns>链接数组</returns>
    public Link[] GetLinks(String url)
    {
        var html = GetHtml(url);
        if (html.IsNullOrWhiteSpace()) return [];
        return Link.Parse(html, url);
    }

    /// <summary>获取指定目录的文件信息</summary>
    /// <param name="dir">目录</param>
    /// <returns>链接数组</returns>
    public Link[] GetLinksInDirectory(String dir)
    {
        if (dir.IsNullOrEmpty()) return [];

        var directory = dir.AsDirectory();
        if (!directory.Exists) return [];

        var list = new List<Link>();
        foreach (var file in directory.GetAllFiles("*.zip;*.gz;*.tar.gz;*.7z;*.exe"))
        {
            var link = new Link();
            link.Parse(file.FullName);
            list.Add(link);
        }

        return [.. list];
    }

    /// <summary>分析指定页面指定名称的链接，并下载到目标目录</summary>
    /// <param name="urls">多个页面地址</param>
    /// <param name="name">页面上指定名称的链接</param>
    /// <param name="destdir">目标目录</param>
    /// <returns>下载后的文件</returns>
    public String DownloadLink(String urls, String name, String destdir)
    {
        Log.Info("下载链接：{0}，目标：{1}", urls, name);

        var names = name.Split(",", ";");
        var file = String.Empty;
        Link? link = null;
        Exception? lastError = null;
        foreach (var url in urls.Split(",", ";"))
        {
            try
            {
                var links = GetLinks(url);
                if (links.Length == 0) continue;

                var total = links.Length;
                links = links.Where(item => item.Name.EqualIgnoreCase(names) || item.FullName.EqualIgnoreCase(names)).ToArray();
                Log.Info("在页面[{0}]个链接中找到[{1}]个：{2}", total, links.Length, links.Join(",", item => item.FullName));

                if (MinVersion != null)
                    links = links.Where(item => item.Version >= MinVersion).ToArray();
                link = links.OrderByDescending(item => item.Version).ThenByDescending(item => item.Time).FirstOrDefault();

                if (link != null) Log.Info("选择：{0}", link);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (link != null) break;
        }

        if (link == null)
        {
            var links = GetLinksInDirectory(destdir);
            links = links.Where(item => item.Name.EqualIgnoreCase(names) || item.FullName.EqualIgnoreCase(names)).ToArray();
            link = links.OrderByDescending(item => item.Version).ThenByDescending(item => item.Time).FirstOrDefault();
        }

        if (link == null)
        {
            if (lastError != null) throw lastError;
            return file;
        }

        if (link.Url.IsNullOrEmpty() || link.FullName.IsNullOrEmpty()) throw new InvalidDataException();

        LastLink = link;
        var linkName = link.FullName;
        var file2 = destdir.CombinePath(linkName).EnsureDirectory();
        if (File.Exists(file2) && ValidLocal(linkName, link, file2)) return file2;

        Log.Info("分析得到文件 {0}，准备下载 {1}，保存到 {2}", linkName, link.Url, file2);
        file2 = file2.EnsureDirectory();

        var stopwatch = Stopwatch.StartNew();
        Task.Run(() => DownloadFileAsync(link.Url, file2)).Wait(Timeout);
        stopwatch.Stop();

        if (File.Exists(file2))
        {
            Log.Info("下载完成，共{0:n0}字节，耗时{1:n0}毫秒", file2.AsFile().Length, stopwatch.ElapsedMilliseconds);
            file = file2;
        }

        return file;
    }

    private Boolean ValidLocal(String linkName, Link link, String file)
    {
        var position = linkName.LastIndexOf("_");
        if (position > 0 && (position + 8 + 1 == linkName.Length || position + 14 + 1 == linkName.Length))
        {
            Log.Info("分析得到文件：{0}，目标文件已存在，无需下载：{1}", linkName, link.Url);
            return true;
        }

        if (!link.Hash.IsNullOrEmpty() && link.Hash.Length == 32)
        {
            var hash = file.AsFile().MD5().AsSpan().ToHex();
            if (link.Hash.EqualIgnoreCase(hash))
            {
                Log.Info("分析得到文件：{0}，目标文件已存在，且MD5哈希一致", linkName, link.Url);
                return true;
            }
        }

        if (!link.Hash.IsNullOrEmpty() && link.Hash.Length == 128)
        {
            using var stream = file.AsFile().OpenRead();
            var hash = SHA512.Create().ComputeHash(stream).AsSpan().ToHex();
            if (link.Hash.EqualIgnoreCase(hash))
            {
                Log.Info("分析得到文件：{0}，目标文件已存在，且SHA512哈希一致", linkName, link.Url);
                return true;
            }
        }

        if (link.Hash.IsNullOrEmpty() && File.Exists(file))
        {
            Log.Info("分析得到文件：{0}，下载失败，使用已存在的目标文件", linkName, link.Url);
            return true;
        }

        return false;
    }

    /// <summary>分析指定页面指定名称的链接，并下载到目标目录，解压后返回目标文件</summary>
    /// <param name="urls">多个页面地址</param>
    /// <param name="name">链接名称</param>
    /// <param name="destdir">目标目录</param>
    /// <param name="overwrite">是否覆盖</param>
    /// <returns>下载后的文件</returns>
    public String? DownloadLinkAndExtract(String urls, String name, String destdir, Boolean overwrite = false)
    {
        var file = String.Empty;
        try
        {
            file = DownloadLink(urls, name, destdir);
        }
        catch (Exception ex)
        {
            var error = ex.GetTrue().ToString();
            if (!error.IsNullOrEmpty()) Log.Error(error);
            if (!file.IsNullOrEmpty() && File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }

        if (file.IsNullOrEmpty()) return null;

        try
        {
            Log.Info("解压缩到 {0}", destdir);
            file.AsFile().Extract(destdir, overwrite);
            return file;
        }
        catch (Exception ex)
        {
            Log.Error(ex.ToString());
        }

        return null;
    }
}