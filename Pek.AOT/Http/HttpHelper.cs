using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.IO;
using Pek.Log;
using Pek.Net;
using Pek.Serialization;
using Pek.Xml;

namespace Pek.Http;

/// <summary>Http帮助类</summary>
/// <remarks>提供常用 HttpClient 扩展，包括表单、Json、Xml、上传下载、原始封包以及过滤器扩展。</remarks>
public static class HttpHelper
{
    /// <summary>性能跟踪器</summary>
    public static ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    /// <summary>Http过滤器</summary>
    public static IHttpFilter? Filter { get; set; }

    /// <summary>默认用户浏览器UserAgent</summary>
    public static String? DefaultUserAgent { get; set; }

    static HttpHelper()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        if (assembly != null)
        {
            var name = assembly.GetName();
            var os = Environment.OSVersion?.ToString();
            if (!os.IsNullOrEmpty() && os.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)) os = os[10..];
            if (!os.IsNullOrEmpty() && Encoding.UTF8.GetByteCount(os) == os.Length)
                DefaultUserAgent = $"{name.Name}/{name.Version} ({os})";
            else
                DefaultUserAgent = $"{name.Name}/{name.Version}";
        }
    }

    /// <summary>设置浏览器UserAgent</summary>
    /// <param name="client">客户端</param>
    /// <returns>客户端</returns>
    public static HttpClient SetUserAgent(this HttpClient client)
    {
        var userAgent = DefaultUserAgent;
        if (!userAgent.IsNullOrEmpty()) client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    /// <summary>为 HttpClient 创建处理器</summary>
    /// <param name="useProxy">是否使用代理</param>
    /// <param name="useCookie">是否使用 Cookie</param>
    /// <returns>消息处理器</returns>
    public static HttpMessageHandler CreateHandler(Boolean useProxy, Boolean useCookie) => CreateHandler(useProxy, useCookie, false);

    /// <summary>为 HttpClient 创建处理器</summary>
    /// <param name="useProxy">是否使用代理</param>
    /// <param name="useCookie">是否使用 Cookie</param>
    /// <param name="ignoreSSL">是否忽略证书校验</param>
    /// <returns>消息处理器</returns>
    public static HttpMessageHandler CreateHandler(Boolean useProxy, Boolean useCookie, Boolean ignoreSSL)
    {
#if NET5_0_OR_GREATER
        var handler = new SocketsHttpHandler
        {
            UseProxy = useProxy,
            UseCookies = useCookie,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = ConnectCallback,
        };

        if (ignoreSSL)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
        }

        return handler;
#elif NETCOREAPP3_0_OR_GREATER
        var handler = new SocketsHttpHandler
        {
            UseProxy = useProxy,
            UseCookies = useCookie,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (ignoreSSL)
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            };
        }

        return handler;
#else
        var handler = new HttpClientHandler
        {
            UseProxy = useProxy,
            UseCookies = useCookie,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        if (ignoreSSL) ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        return handler;
#endif
    }

#if NET5_0_OR_GREATER
    private static async ValueTask<Stream> ConnectCallback(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var dep = context.DnsEndPoint;
        var method = context.InitialRequestMessage.Method?.ToString() ?? "Connect";
        using var span = Tracer?.NewSpan($"net:{dep.Host}:{dep.Port}:{method}");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            var addrs = NetUri.ParseAddress(dep.Host);
            span?.AppendTag($"addrs={JoinAddresses(addrs)}");
            if (addrs != null && addrs.Length > 0)
                await socket.ConnectAsync(addrs, dep.Port, cancellationToken).ConfigureAwait(false);
            else
                await socket.ConnectAsync(dep, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            if (ex is SocketException se) Tracer?.NewError($"socket:SocketError-{se.SocketErrorCode}", se);
            socket.Dispose();
            throw;
        }

        return new NetworkStream(socket, true);
    }
#endif

    /// <summary>创建请求包</summary>
    /// <param name="method">请求方法</param>
    /// <param name="uri">目标地址</param>
    /// <param name="headers">请求头</param>
    /// <param name="pk">请求体</param>
    /// <returns>请求包</returns>
    public static IPacket MakeRequest(String method, Uri uri, IDictionary<String, Object?>? headers, IPacket? pk)
    {
        var count = pk?.Total ?? 0;
        if (method.IsNullOrEmpty()) method = count > 0 ? "POST" : "GET";

        var host = GetHost(uri);
        var sb = Pool.StringBuilder.Get();
        sb.AppendFormat("{0} {1} HTTP/1.1\r\n", method, uri.PathAndQuery);
        sb.AppendFormat("Host: {0}\r\n", host);

        if (count > 0) sb.AppendFormat("Content-Length: {0}\r\n", count);
        if (headers != null)
        {
            foreach (var item in headers)
            {
                sb.AppendFormat("{0}: {1}\r\n", item.Key, item.Value);
            }
        }

        sb.Append("\r\n");
        return new ArrayPacket(sb.Return(true).GetBytes()) { Next = pk };
    }

    /// <summary>创建响应包</summary>
    /// <param name="code">状态码</param>
    /// <param name="headers">响应头</param>
    /// <param name="pk">响应体</param>
    /// <returns>响应包</returns>
    public static IPacket MakeResponse(HttpStatusCode code, IDictionary<String, Object?>? headers, IPacket? pk)
    {
        var sb = Pool.StringBuilder.Get();
        sb.AppendFormat("HTTP/1.1 {0} {1}\r\n", (Int32)code, code);

        var count = pk?.Total ?? 0;
        if (count > 0) sb.AppendFormat("Content-Length: {0}\r\n", count);
        if (headers != null)
        {
            foreach (var item in headers)
            {
                sb.AppendFormat("{0}: {1}\r\n", item.Key, item.Value);
            }
        }

        sb.Append("\r\n");
        return new ArrayPacket(sb.Return(true).GetBytes()) { Next = pk };
    }

    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n'];

    /// <summary>分析头部</summary>
    /// <param name="pk">数据包</param>
    /// <returns>头部字典</returns>
#pragma warning disable CS0618 // 类型或成员已过时
    public static IDictionary<String, Object> ParseHeader(Packet pk)
    {
        var headers = new Dictionary<String, Object>(StringComparer.OrdinalIgnoreCase);
        var p = pk.IndexOf(NewLine);
        if (p < 0) return headers;

        var lines = pk.ReadBytes(0, p).ToStr().Split("\r\n");
        p += 4;
        pk.Set(pk.Data, pk.Offset + p, pk.Count - p);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var k = line.IndexOf(':');
            if (k > 0) headers[line[..k]] = line[(k + 1)..].Trim();
        }

        if (lines.Length <= 0) return headers;

        var first = lines[0];
        var ss = first.Split(' ');
        if (ss.Length >= 3 && ss[2].StartsWithIgnoreCase("HTTP/"))
        {
            headers["Method"] = ss[0];
            var host = headers.TryGetValue("Host", out var target) ? target + String.Empty : String.Empty;
            headers["Url"] = new Uri($"http://{host}{ss[1]}");
        }
        else if (ss.Length >= 2)
        {
            var code = ss[1].ToInt();
            if (code > 0) headers["StatusCode"] = (HttpStatusCode)code;
        }

        return headers;
    }
#pragma warning restore CS0618

#pragma warning restore CS0618

    /// <summary>异步提交 Json</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="headers">请求头</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    public static async Task<String> PostJsonAsync(this HttpClient client, String requestUri, Object data, IDictionary<String, String>? headers = null, CancellationToken cancellationToken = default)
    {
        var content = data is String str
            ? new StringContent(str, Encoding.UTF8, "application/json")
            : new StringContent(data.ToJson(), Encoding.UTF8, "application/json");
        return await PostAsync(client, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>同步提交 Json</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="headers">请求头</param>
    /// <returns>响应字符串</returns>
    public static String PostJson(this HttpClient client, String requestUri, Object data, IDictionary<String, String>? headers = null) => client.PostJsonAsync(requestUri, data, headers).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步提交 Xml</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="headers">请求头</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    public static async Task<String> PostXmlAsync(this HttpClient client, String requestUri, Object data, IDictionary<String, String>? headers = null, CancellationToken cancellationToken = default)
    {
        var content = data is String str
            ? new StringContent(str, Encoding.UTF8, "application/xml")
            : new StringContent(SerializeXml(data), Encoding.UTF8, "application/xml");
        return await PostAsync(client, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>同步提交 Xml</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="headers">请求头</param>
    /// <returns>响应字符串</returns>
    public static String PostXml(this HttpClient client, String requestUri, Object data, IDictionary<String, String>? headers = null) => client.PostXmlAsync(requestUri, data, headers).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步提交表单</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="headers">请求头</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    public static async Task<String> PostFormAsync(this HttpClient client, String requestUri, Object data, IDictionary<String, String>? headers = null, CancellationToken cancellationToken = default)
    {
        HttpContent content;
        if (data is String str)
        {
            content = new StringContent(str, Encoding.UTF8, "application/x-www-form-urlencoded");
        }
#if NET5_0_OR_GREATER
        else if (data is IDictionary<String?, String?> stringDictionary)
        {
            content = new FormUrlEncodedContent(stringDictionary);
        }
#endif
        else if (data is IDictionary<String, String> typedStringDictionary)
        {
            content = new FormUrlEncodedContent(typedStringDictionary);
        }
        else
        {
            var list = new List<KeyValuePair<String?, String?>>();
            foreach (var item in GetDictionaryValues(data))
            {
                list.Add(new KeyValuePair<String?, String?>(item.Key, item.Value + String.Empty));
            }

            content = new FormUrlEncodedContent(list);
        }

        return await PostAsync(client, requestUri, content, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>同步提交表单</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="headers">请求头</param>
    /// <returns>响应字符串</returns>
    public static String PostForm(this HttpClient client, String requestUri, Object data, IDictionary<String, String>? headers = null) => client.PostFormAsync(requestUri, data, headers).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步提交多段表单</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="data">数据对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    public static async Task<String> PostMultipartFormAsync(this HttpClient client, String requestUri, Object data, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent();
        foreach (var item in GetDictionaryValues(data))
        {
            if (item.Value is FileStream fs)
                content.Add(new StreamContent(fs), item.Key, Path.GetFileName(fs.Name));
            else if (item.Value is Stream stream)
                content.Add(new StreamContent(stream), item.Key);
            else if (item.Value is String str)
                content.Add(new StringContent(str), item.Key);
            else if (item.Value is Byte[] buf)
                content.Add(new ByteArrayContent(buf), item.Key);
            else if (item.Value == null || IsBaseType(item.Value.GetType()))
                content.Add(new StringContent(item.Value + String.Empty), item.Key);
            else
                content.Add(new StringContent(item.Value.ToJson()), item.Key);
        }

        return await PostAsync(client, requestUri, content, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>同步获取字符串</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="headers">请求头</param>
    /// <returns>响应字符串</returns>
    public static String GetString(this HttpClient client, String requestUri, IDictionary<String, String>? headers = null)
    {
        if (headers != null) client.AddHeaders(headers);
#if NET5_0_OR_GREATER
        using var source = new CancellationTokenSource(client.Timeout);
        return client.GetStringAsync(requestUri, source.Token).ConfigureAwait(false).GetAwaiter().GetResult();
#else
        return client.GetStringAsync(requestUri).ConfigureAwait(false).GetAwaiter().GetResult();
#endif
    }

    /// <summary>下载文件到本地</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="fileName">文件名</param>
    /// <returns>任务</returns>
    public static async Task DownloadFileAsync(this HttpClient client, String requestUri, String fileName)
    {
        fileName = fileName.GetFullPath();
        var stream = await client.GetStreamAsync(requestUri).ConfigureAwait(false);
        var tempFile = Path.GetTempFileName();

        try
        {
            using (var fs = new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                await stream.CopyToAsync(fs).ConfigureAwait(false);
                fs.SetLength(fs.Position);
                await fs.FlushAsync().ConfigureAwait(false);
            }

            fileName.EnsureDirectory(true);
            if (File.Exists(fileName)) File.Delete(fileName);
            File.Move(tempFile, fileName);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            catch { }
        }
    }

    /// <summary>下载文件到本地</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="fileName">文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public static async Task DownloadFileAsync(this HttpClient client, String requestUri, String fileName, CancellationToken cancellationToken)
    {
#if NET5_0_OR_GREATER
        var stream = await client.GetStreamAsync(requestUri, cancellationToken).ConfigureAwait(false);
#else
        var stream = await client.GetStreamAsync(requestUri).ConfigureAwait(false);
#endif
        await SaveFileAsync(stream, fileName, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>下载文件到本地并校验哈希</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="fileName">文件名</param>
    /// <param name="expectedHash">预期哈希</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public static async Task DownloadFileAsync(this HttpClient client, String requestUri, String fileName, String? expectedHash, CancellationToken cancellationToken = default)
    {
        if (expectedHash.IsNullOrEmpty())
        {
            await client.DownloadFileAsync(requestUri, fileName, cancellationToken).ConfigureAwait(false);
            return;
        }

#if NET5_0_OR_GREATER
        var stream = await client.GetStreamAsync(requestUri, cancellationToken).ConfigureAwait(false);
#else
        var stream = await client.GetStreamAsync(requestUri).ConfigureAwait(false);
#endif
        await SaveFileAsync(stream, fileName, expectedHash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>保存流到文件</summary>
    /// <param name="stream">数据流</param>
    /// <param name="fileName">文件名</param>
    /// <param name="expectedHash">预期哈希</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public static async Task SaveFileAsync(this Stream stream, String fileName, String? expectedHash, CancellationToken cancellationToken = default)
    {
        fileName = fileName.GetFullPath();
        var temp = fileName + ".tmp";

        try
        {
            temp.EnsureDirectory(true);
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bufferSize = SocketSetting.Current.BufferSize;
                if (bufferSize < 1024) bufferSize = 1024;

                await stream.CopyToAsync(fs, bufferSize, cancellationToken).ConfigureAwait(false);
                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!expectedHash.IsNullOrEmpty())
            {
                var fi = temp.AsFile();
                if (!fi.VerifyHash(expectedHash))
                {
                    try
                    {
                        if (File.Exists(temp)) File.Delete(temp);
                    }
                    catch { }

                    throw new IOException("Save file hash verification failed.");
                }
            }

            fileName.EnsureDirectory(true);
            if (File.Exists(fileName))
            {
                try
                {
                    File.Delete(fileName);
                }
                catch
                {
                    File.Move(fileName, $"{fileName}.tmp.{DateTime.Now:yyMMddHHmmss}");
                }
            }

            File.Move(temp, fileName);
        }
        finally
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch { }
        }
    }

    /// <summary>上传文件以及附带数据</summary>
    /// <param name="client">客户端</param>
    /// <param name="requestUri">请求地址</param>
    /// <param name="fileName">文件名</param>
    /// <param name="data">附带数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应字符串</returns>
    public static async Task<String> UploadFileAsync(this HttpClient client, String requestUri, String fileName, Object? data = null, CancellationToken cancellationToken = default)
    {
        var content = new MultipartFormDataContent();
        if (!fileName.IsNullOrEmpty())
            content.Add(new StreamContent(fileName.AsFile().OpenRead()), "file", Path.GetFileName(fileName));

        if (data != null)
        {
            foreach (var item in GetDictionaryValues(data))
            {
                if (item.Value is String str)
                    content.Add(new StringContent(str), item.Key);
                else if (item.Value is Byte[] buf)
                    content.Add(new ByteArrayContent(buf), item.Key);
                else if (item.Value == null || IsBaseType(item.Value.GetType()))
                    content.Add(new StringContent(item.Value + String.Empty), item.Key);
                else
                    content.Add(new StringContent(item.Value.ToJson()), item.Key);
            }
        }

        return await PostAsync(client, requestUri, content, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<String> PostAsync(HttpClient client, String requestUri, HttpContent content, IDictionary<String, String>? headers, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };
        if (headers != null)
        {
            foreach (var item in headers)
            {
                request.Headers.Add(item.Key, item.Value);
            }
        }

        if (content.Headers.TryGetValues("Content-Type", out var values))
        {
            var contentType = values.FirstOrDefault()?.Split(';').FirstOrDefault();
            if (!contentType.IsNullOrEmpty() && contentType.EqualIgnoreCase("application/json", "application/xml")) request.Headers.Accept.ParseAdd(contentType);
        }

        using var span = Tracer?.NewSpan(request);
        var filter = Filter;
        try
        {
            if (filter != null) await filter.OnRequest(client, request, null, cancellationToken).ConfigureAwait(false);
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (filter != null) await filter.OnResponse(client, response, request, cancellationToken).ConfigureAwait(false);

#if NET5_0_OR_GREATER
            var result = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            span?.AppendTag(result);
            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            if (filter != null) await filter.OnError(client, ex, request, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static HttpClient AddHeaders(this HttpClient client, IDictionary<String, String> headers)
    {
        foreach (var item in headers)
        {
            if (client.DefaultRequestHeaders.Contains(item.Key)) client.DefaultRequestHeaders.Remove(item.Key);
            client.DefaultRequestHeaders.Add(item.Key, item.Value);
        }

        return client;
    }

    private static String GetHost(Uri uri)
    {
        if (uri.IsDefaultPort) return uri.Host;
        return uri.Host.Contains(':') ? $"[{uri.Host}]:{uri.Port}" : $"{uri.Host}:{uri.Port}";
    }

    private static String JoinAddresses(IPAddress[]? addrs)
    {
        if (addrs == null || addrs.Length == 0) return String.Empty;

        var builder = Pool.StringBuilder.Get();
        foreach (var item in addrs)
        {
            builder.Separate(",").Append(item);
        }

        return builder.Return(true);
    }

    private static String SerializeXml(Object data)
    {
        if (data is IDictionary<String, String> dictionary) return dictionary.ToXml();

        var jsonOptions = JsonHelper.Default.Options;
        var options = new JsonSerializerOptions { WriteIndented = jsonOptions.WriteIndented };
        if (jsonOptions.IgnoreNullValues) options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
        return data.ToXml(data.GetType(), options);
    }

    private static IEnumerable<KeyValuePair<String, Object?>> GetDictionaryValues(Object data)
    {
        if (data is IDictionarySource source) return source.ToDictionary();
        if (data is IDictionary<String, Object?> dictionary) return dictionary;
        if (data is IDictionary<String, String> stringDictionary) return stringDictionary.Select(e => new KeyValuePair<String, Object?>(e.Key, e.Value));
        if (data is IEnumerable<KeyValuePair<String, Object?>> keyValuePairs) return keyValuePairs;

        throw new NotSupportedException($"HttpHelper 仅支持 IDictionary、IDictionarySource 或 IEnumerable<KeyValuePair<String, Object?>> 作为表单/上传数据类型。当前类型：{data.GetType().FullName}");
    }

    private static Boolean IsBaseType(Type type)
    {
        if (type.IsEnum) return true;

        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (Type.GetTypeCode(actualType) != TypeCode.Object) return true;

        return actualType == typeof(Guid)
            || actualType == typeof(DateTimeOffset)
            || actualType == typeof(TimeSpan)
            || actualType == typeof(Byte[]);
    }
}