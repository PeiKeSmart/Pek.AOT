using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Http;
using Pek.IO;
using Pek.Log;
using Pek.Serialization;
using Pek.Xml;

namespace Pek.Remoting;

/// <summary>Api助手</summary>
public static class ApiHelper
{
    #region 远程调用
    /// <summary>性能跟踪器</summary>
    public static ITracer? Tracer { get; set; } = DefaultTracer.Instance;

    /// <summary>Http过滤器</summary>
    public static IHttpFilter? Filter { get; set; }

    /// <summary>异步调用，等待返回结果</summary>
    public static Task<TResult?> GetAsync<TResult>(this HttpClient client, String action, Object? args = null, CancellationToken cancellationToken = default) =>
        client.InvokeAsync<TResult>(HttpMethod.Get, action, args, null, "data", cancellationToken);

    /// <summary>同步获取，参数构造在Url</summary>
    public static TResult? Get<TResult>(this HttpClient client, String action, Object? args = null) =>
        GetAsync<TResult>(client, action, args).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步调用，等待返回结果</summary>
    public static Task<TResult?> PostAsync<TResult>(this HttpClient client, String action, Object? args = null, CancellationToken cancellationToken = default) =>
        client.InvokeAsync<TResult>(HttpMethod.Post, action, args, null, "data", cancellationToken);

    /// <summary>同步提交，参数Json打包在Body</summary>
    public static TResult? Post<TResult>(this HttpClient client, String action, Object? args = null) =>
        PostAsync<TResult>(client, action, args).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步上传，等待返回结果</summary>
    public static Task<TResult?> PutAsync<TResult>(this HttpClient client, String action, Object? args = null, CancellationToken cancellationToken = default) =>
        client.InvokeAsync<TResult>(HttpMethod.Put, action, args, null, "data", cancellationToken);

    /// <summary>异步删除，等待返回结果</summary>
    public static Task<TResult?> DeleteAsync<TResult>(this HttpClient client, String action, Object? args = null, CancellationToken cancellationToken = default) =>
        client.InvokeAsync<TResult>(HttpMethod.Delete, action, args, null, "data", cancellationToken);

    /// <summary>异步调用，等待返回结果</summary>
    public static async Task<TResult?> InvokeAsync<TResult>(this HttpClient client, HttpMethod method, String action, Object? args = null, Action<HttpRequestMessage>? onRequest = null, String dataName = "data", CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(method, action, args);
        var returnType = typeof(TResult);

#pragma warning disable CS0618
        if (returnType == typeof(Byte[]) || returnType == typeof(IPacket) || returnType == typeof(Packet))
#pragma warning restore CS0618
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        else
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        onRequest?.Invoke(request);

        using var span = Tracer?.NewSpan(request);
        var filter = Filter;
        try
        {
            if (filter != null) await filter.OnRequest(client, request, null, cancellationToken).ConfigureAwait(false);

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (filter != null) await filter.OnResponse(client, response, request, cancellationToken).ConfigureAwait(false);

            return await ProcessResponse<TResult>(response, null, dataName).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, args);

            if (filter != null) await filter.OnError(client, ex, request, cancellationToken).ConfigureAwait(false);

            throw;
        }
    }
    #endregion

    #region 远程辅助
    /// <summary>建立请求，action写到url里面</summary>
    public static HttpRequestMessage BuildRequest(HttpMethod method, String action, Object? args, IJsonHost? jsonHost = null)
    {
        var request = new HttpRequestMessage(method, action);

        if (args is HttpContent content)
        {
            request.Content = content;
            return request;
        }

        if (method == HttpMethod.Get || method == HttpMethod.Delete)
        {
            if (args is IPacket packet)
            {
                var url = action + (action.Contains('?') ? "&" : "?") + packet.ToArray().ToUrlBase64();
                request.RequestUri = new global::System.Uri(url, UriKind.RelativeOrAbsolute);
            }
            else if (args is Byte[] buffer)
            {
                var url = action + (action.Contains('?') ? "&" : "?") + buffer.ToUrlBase64();
                request.RequestUri = new global::System.Uri(url, UriKind.RelativeOrAbsolute);
            }
            else if (args != null)
            {
                var values = GetValues(args);
                request.RequestUri = new global::System.Uri(GetUrl(action, values), UriKind.RelativeOrAbsolute);
            }

            return request;
        }

        if (method == HttpMethod.Post || method == HttpMethod.Put || method.Method == "PATCH")
        {
            if (args is IPacket packet)
            {
                request.Content = BuildContent(packet);
            }
            else if (args is Byte[] buffer)
            {
                request.Content = BuildContent(new ArrayPacket(buffer));
            }
            else if (args != null)
            {
                jsonHost ??= JsonHelper.Default;
                var body = jsonHost.Write(args, new JsonOptions { IgnoreNullValues = true });
                var byteContent = new ByteArrayContent(body.GetBytes());
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = byteContent;
            }
        }

        return request;
    }

    /// <summary>为二进制数据生成请求体内容。对超长内容进行压缩</summary>
    public static HttpContent BuildContent(IPacket data)
    {
        var gzip = Pek.Net.SocketSetting.Current.AutoGZip;
        if (gzip > 0 && data.Total >= gzip)
        {
            var buffer = data.ReadBytes().CompressGZip();
            var gzipContent = new ByteArrayContent(buffer);
            gzipContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-gzip");
            return gzipContent;
        }

        var content = data.Next == null && data.TryGetArray(out var segment)
            ? new ByteArrayContent(segment.Array!, segment.Offset, segment.Count)
            : new ByteArrayContent(data.ReadBytes());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    /// <summary>结果代码名称。默认 code/errcode/status</summary>
    public static IList<String> CodeNames { get; } = ["code", "errcode", "status"];

    /// <summary>结果消息名称。默认 message/msg/errmsg/error</summary>
    public static IList<String> MessageNames { get; } = ["message", "msg", "errmsg", "error"];

    /// <summary>处理响应。统一识别code/message</summary>
    public static Task<TResult?> ProcessResponse<TResult>(HttpResponseMessage response, String dataName = "data") => ProcessResponse<TResult>(response, null, dataName);

    /// <summary>处理响应。统一识别code/message</summary>
    public static Task<TResult?> ProcessResponse<TResult>(HttpResponseMessage response, String? codeName, String? dataName) => ProcessResponse<TResult>(response, codeName, dataName, JsonHelper.Default);

    /// <summary>处理响应。统一识别code/message</summary>
    public static async Task<TResult?> ProcessResponse<TResult>(HttpResponseMessage response, String? codeName, String? dataName, IJsonHost jsonHost)
    {
        var resultType = typeof(TResult);
        if (resultType == typeof(HttpResponseMessage)) return (TResult)(Object)response;

#if NET5_0_OR_GREATER
        var buffer = response.Content == null ? null : await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#else
        var buffer = response.Content == null ? null : await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif

        if (response.StatusCode >= HttpStatusCode.BadRequest)
        {
            var message = buffer == null ? null : Encoding.UTF8.GetString(buffer).Trim('"');
            if (!message.IsNullOrEmpty() && message.StartsWith("{") && message.EndsWith("}"))
            {
                var dictionary = jsonHost.Decode(message);
                if (dictionary != null)
                {
                    var detail = String.Empty;
                    if (dictionary.TryGetValue("title", out var value)) detail = value + String.Empty;
                    if (dictionary.TryGetValue("errors", out value) && value != null) detail += jsonHost.Write(value, new JsonOptions { IgnoreNullValues = true });
                    if (!detail.IsNullOrEmpty()) message = detail.Trim();
                }
            }

            if (message.IsNullOrEmpty()) message = response.ReasonPhrase;
            if (message.IsNullOrEmpty()) message = response.StatusCode + String.Empty;

#if NET5_0_OR_GREATER
            throw new HttpRequestException(message);
#else
            throw new HttpRequestException(message);
#endif
        }

        if (buffer == null || buffer.Length == 0) return default;

        if (resultType == typeof(Byte[])) return (TResult)(Object)buffer;
        if (resultType == typeof(IPacket)) return (TResult)(Object)new ArrayPacket(buffer);
#pragma warning disable CS0618
        if (resultType == typeof(Packet)) return (TResult)(Object)new Packet(buffer);
#pragma warning restore CS0618

        var text = Encoding.UTF8.GetString(buffer).Trim();
        return ProcessResponse<TResult>(text, codeName, dataName ?? "data", jsonHost);
    }

    /// <summary>处理响应。</summary>
    public static TResult? ProcessResponse<TResult>(String? response, String? codeName, String dataName, IJsonHost? jsonHost = null)
    {
        if (response.IsNullOrEmpty()) return default;

        var resultType = typeof(TResult);
        jsonHost ??= JsonHelper.Default;

        IDictionary<String, Object?>? dictionary;
        if (response.StartsWith("<") && response.EndsWith(">"))
        {
            var xmlDictionary = XmlHelper.ToXmlDictionary(response) ?? new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            dictionary = xmlDictionary.ToDictionary(item => item.Key, item => (Object?)item.Value, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var value = jsonHost.Parse(response);
            if (dataName.IsNullOrEmpty() && value is TResult typedValue) return typedValue;

            dictionary = value as IDictionary<String, Object?>;
        }

        if (dictionary == null) throw new InvalidCastException($"Unable to convert to type [{resultType}]! {response.Cut(64)}");

        var hasData = !dataName.IsNullOrEmpty() && dictionary.ContainsKey(dataName);
        if (!hasData && dictionary is TResult typedDictionary) return typedDictionary;

        var data = hasData ? dictionary[dataName] : dictionary;
        var code = ReadCode(dictionary, codeName);
        if (code is not ApiCode.Ok and not ApiCode.Ok200)
        {
            var message = ReadMessage(dictionary);
            if (message.IsNullOrEmpty()) message = data + String.Empty;
            throw new ApiException(code, message);
        }

        if (data is TResult result) return result;
        if (resultType == typeof(Object)) return (TResult?)data;
        if (data == null && IsNullableType(resultType)) return default;
        if (IsSimpleType(resultType)) return ChangeValue<TResult>(data);
        if (data == null) return default;

        if (data is not IDictionary<String, Object?> and not IList<Object?> and not IDictionary<String, Object> and not IList<Object>)
            throw new InvalidDataException($"Unrecognized response data [{(data as String)?.Cut(64)}] for [{resultType.Name}]");

        return jsonHost.Convert<TResult>(data);
    }

    /// <summary>根据动作和参数构造Url</summary>
    public static String GetUrl(String action, IDictionary<String, Object?>? parameters)
    {
        if (parameters == null || parameters.Count == 0) return action;

        var builder = Pool.StringBuilder.Get();
        builder.Append(action);
        builder.Append(action.Contains('?') ? '&' : '?');

        var first = true;
        foreach (var item in parameters)
        {
            if (!first) builder.Append('&');
            first = false;

            var value = item.Value is DateTime dateTime ? dateTime.ToFullString() : item.Value + String.Empty;
            builder.AppendFormat("{0}={1}", Encode(item.Key), Encode(value));
        }

        return builder.Return(true);
    }

    private static IDictionary<String, Object?>? GetValues(Object args)
    {
        if (args is IDictionarySource source) return source.ToDictionary();
        if (args is IDictionary<String, Object?> dictionary) return dictionary;
        if (args is IDictionary<String, String> stringDictionary) return stringDictionary.ToDictionary(item => item.Key, item => (Object?)item.Value, StringComparer.OrdinalIgnoreCase);
        if (args is IEnumerable<KeyValuePair<String, Object?>> pairs) return pairs.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        throw new NotSupportedException($"ApiHelper 仅支持 IDictionary、IDictionarySource 或 IEnumerable<KeyValuePair<String, Object?>> 作为查询参数类型。当前类型：{args.GetType().FullName}");
    }

    private static Int32 ReadCode(IDictionary<String, Object?> dictionary, String? codeName)
    {
        if (!codeName.IsNullOrEmpty())
        {
            if (dictionary.TryGetValue(codeName, out var value)) return value is Boolean flag ? (flag ? 0 : -1) : value.ToInt();
            return 0;
        }

        foreach (var item in CodeNames)
        {
            if (!dictionary.TryGetValue(item, out var value)) continue;
            return value is Boolean flag ? (flag ? 0 : -1) : value.ToInt();
        }

        return 0;
    }

    private static String ReadMessage(IDictionary<String, Object?> dictionary)
    {
        foreach (var item in MessageNames)
        {
            if (dictionary.TryGetValue(item, out var value) && value != null) return value + String.Empty;
        }

        return String.Empty;
    }

    private static Boolean IsNullableType(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    private static Boolean IsSimpleType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType.IsEnum) return true;
        if (actualType == typeof(Guid) || actualType == typeof(TimeSpan) || actualType == typeof(DateTimeOffset)) return true;

        return Type.GetTypeCode(actualType) != TypeCode.Object;
    }

    private static TResult? ChangeValue<TResult>(Object? value)
    {
        if (value == null) return default;
        if (value is TResult result) return result;

        return (TResult?)JsonHelper.Default.Convert(value, typeof(TResult));
    }

    private static String Encode(String? data)
    {
        if (String.IsNullOrEmpty(data)) return String.Empty;

        return Uri.EscapeDataString(data).Replace("%20", "+");
    }

    private static String ToUrlBase64(this Byte[] data)
    {
        var text = Convert.ToBase64String(data);
        return text.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    #endregion
}