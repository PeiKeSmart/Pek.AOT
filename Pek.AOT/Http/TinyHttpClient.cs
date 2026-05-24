using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

using Pek;
using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Net;
using Pek.Remoting;
using Pek.Serialization;

namespace Pek.Http;

/// <summary>迷你Http客户端。支持https和302跳转</summary>
/// <remarks>基于Tcp连接设计，用于高吞吐的HTTP通信场景，功能较少，但一切均在掌控之中。</remarks>
public class TinyHttpClient : DisposeBase
{
    #region 属性
    /// <summary>客户端</summary>
    public System.Net.Sockets.TcpClient? Client { get; set; }

    /// <summary>基础地址</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>保持连接</summary>
    public Boolean KeepAlive { get; set; }

    /// <summary>超时时间。默认15s</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>缓冲区大小。接收缓冲区默认64*1024</summary>
    public Int32 BufferSize { get; set; } = 64 * 1024;

    /// <summary>Json序列化</summary>
    public IJsonHost JsonHost { get; set; } = JsonHelper.Default;

    /// <summary>JSON序列化选项，影响复杂对象的编码和解码行为</summary>
    public JsonOptions? JsonOptions { get; set; }

    /// <summary>性能追踪</summary>
    public ITracer? Tracer { get; set; } = HttpHelper.Tracer;

    private Stream? _stream;
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public TinyHttpClient() { }

    /// <summary>实例化</summary>
    /// <param name="server">服务地址</param>
    public TinyHttpClient(String server) => BaseAddress = new Uri(server);

    /// <summary>销毁</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        Client.TryDispose();
        Client = null;
        _stream.TryDispose();
        _stream = null;
    }
    #endregion

    #region 核心方法
    /// <summary>获取网络数据流</summary>
    /// <param name="uri">目标地址</param>
    /// <returns>网络数据流</returns>
    protected virtual async Task<Stream> GetStreamAsync(Uri? uri)
    {
        var client = Client;
        var stream = _stream;

        var active = false;
        try
        {
            active = stream != null && client != null && client.Connected && stream.CanWrite && stream.CanRead;
            if (active) return stream!;

            stream = client?.GetStream();
            active = stream != null && client != null && client.Connected && stream.CanWrite && stream.CanRead;
        }
        catch { }

        if (!active)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            var remote = new NetUri(NetType.Tcp, uri.Host, uri.Port);

            client.TryDispose();
            stream.TryDispose();

            client = new System.Net.Sockets.TcpClient { ReceiveTimeout = (Int32)Timeout.TotalMilliseconds };
            await client.ConnectAsync(remote.GetAddresses(), remote.Port).ConfigureAwait(false);

            Client = client;
            stream = client.GetStream();

            if (BaseAddress == null) BaseAddress = new Uri(uri, "/");

            active = true;
        }

        if (active)
        {
            if (uri != null && uri.Scheme.EqualIgnoreCase("https"))
            {
                if (stream == null) throw new InvalidOperationException(nameof(NetworkStream));

                var sslStream = new SslStream(stream, false, static (_, _, _, _) => true);
                await sslStream.AuthenticateAsClientAsync(uri.Host, [], SslProtocols.Tls12, false).ConfigureAwait(false);
                stream = sslStream;
            }

            _stream = stream;
        }

        return stream!;
    }

    /// <summary>异步请求网络数据</summary>
    /// <param name="uri">目标地址</param>
    /// <param name="request">请求数据包</param>
    /// <returns>响应数据包</returns>
    protected virtual async Task<IOwnerPacket> SendDataAsync(Uri? uri, IPacket? request)
    {
        var stream = await GetStreamAsync(uri).ConfigureAwait(false);

        if (request != null) await request.CopyToAsync(stream).ConfigureAwait(false);

        var packet = new OwnerPacket(BufferSize);
        using var source = new CancellationTokenSource(Timeout);
        var count = await stream.ReadAsync(packet.GetMemory(), source.Token).ConfigureAwait(false);

        return packet.Resize(count);
    }

    /// <summary>异步发出请求，并接收响应</summary>
    /// <param name="request">Http请求</param>
    /// <returns>Http响应</returns>
    public virtual async Task<HttpResponse?> SendAsync(HttpRequest request)
    {
        var uri = request.RequestUri ?? throw new ArgumentNullException(nameof(request.RequestUri));
        var requestPacket = request.Build();

        var response = new HttpResponse();
        IPacket? body = null;
        var retry = 5;
        while (retry-- > 0)
        {
            var responsePacket = await SendDataAsync(uri, requestPacket).ConfigureAwait(false);
            if (responsePacket == null || responsePacket.Length == 0) return null;

            if (!response.Parse(responsePacket)) return response;
            body = response.Body;

            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect)
            {
                if (response.Headers.TryGetValue("Location", out var location) && !location.IsNullOrEmpty())
                {
                    var uri2 = new Uri(location, UriKind.RelativeOrAbsolute);
                    if (!uri2.IsAbsoluteUri) uri2 = new Uri(uri, uri2);

                    if (uri.Host != uri2.Host || uri.Scheme != uri2.Scheme)
                    {
                        Client.TryDispose();
                        Client = null;
                        _stream.TryDispose();
                        _stream = null;
                    }

                    uri = uri2;
                    request.RequestUri = uri;

                    requestPacket.Dispose();
                    requestPacket = request.Build();

                    continue;
                }
            }

            break;
        }

        requestPacket.Dispose();

        if (response.StatusCode != HttpStatusCode.OK) throw new Exception($"{(Int32)response.StatusCode} {response.StatusDescription}");

        if (body != null && response.ContentLength > 0 && body.Length < response.ContentLength)
        {
            using var memoryStream = new MemoryStream(response.ContentLength);
            await body.CopyToAsync(memoryStream).ConfigureAwait(false);

            var total = body.Length;
            while (total < response.ContentLength)
            {
                var packet = await SendDataAsync(null, null).ConfigureAwait(false);
                if (packet == null || packet.Length == 0) break;

                packet.CopyTo(memoryStream);
                total += packet.Length;
            }

            memoryStream.Position = 0;
            body = new ArrayPacket(memoryStream);
            response.Body = body;
        }

        if (body != null && response.Headers.TryGetValue("Transfer-Encoding", out var transferEncoding) && transferEncoding.EqualIgnoreCase("chunked"))
        {
            if (body.Length == 0)
            {
                body.TryDispose();
                body = await SendDataAsync(null, null).ConfigureAwait(false);
            }

            response.Body = await ReadChunkAsync(body).ConfigureAwait(false);
        }

        if (!KeepAlive)
        {
            Client.TryDispose();
            Client = null;
            _stream.TryDispose();
            _stream = null;
        }

        return response;
    }

    /// <summary>读取分片，返回链式Packet</summary>
    /// <param name="body">响应主体</param>
    /// <returns>完整数据包</returns>
    protected virtual async Task<IPacket> ReadChunkAsync(IPacket body)
    {
        using var memoryStream = new MemoryStream(BufferSize);

        var packet = body;
        while (true)
        {
            var data = packet.GetSpan();
            if (!ParseChunk(data, out var offset, out var len)) break;
            if (len <= 0) break;

            var memory = packet.GetMemory();
            if (offset + len <= memory.Length)
            {
                memory = memory.Slice(offset, len);
                memoryStream.Write(memory.Span);

                var next = offset + len + 2;
                if (next < packet.Length)
                    packet = packet.Slice(next, -1, true);
                else
                {
                    packet.TryDispose();
                    packet = null!;
                }
            }
            else
            {
                memory = memory[offset..];
                memoryStream.Write(memory.Span);

                packet.TryDispose();
                packet = null!;

                var remain = len - memory.Length;
                while (remain > 0)
                {
                    var packet2 = await SendDataAsync(null, null).ConfigureAwait(false);
                    memory = packet2.GetMemory();

                    if (remain <= memory.Length)
                    {
                        memoryStream.Write(memory[..remain].Span);

                        if (remain + 2 < memory.Length)
                            packet = packet2.Slice(remain + 2, -1, true);
                        else
                            packet2.Dispose();

                        remain = 0;
                    }
                    else
                    {
                        memoryStream.Write(memory.Span);
                        remain -= memory.Length;

                        packet2.Dispose();
                    }
                }
            }

            if (packet != null && packet.Length > 0) continue;

            packet = await SendDataAsync(null, null).ConfigureAwait(false);
            if (packet == null || packet.Length == 0) break;
        }

        memoryStream.Position = 0;
        return new ArrayPacket(memoryStream);
    }
    #endregion

    #region 辅助
    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n'];

    private static Boolean ParseChunk(Span<Byte> data, out Int32 offset, out Int32 octets)
    {
        offset = 0;
        octets = 0;
        var position = data.IndexOf(NewLine);
        if (position <= 0) return false;

        octets = Int32.Parse(data[..position], NumberStyles.HexNumber);
        offset = position + 2;

        return true;
    }

    private static IDictionary<String, Object?> GetValues(Object args)
    {
        if (args is IDictionarySource source) return source.ToDictionary();
        if (args is IDictionary<String, Object?> dictionary) return dictionary;
        if (args is IDictionary<String, String> stringDictionary) return stringDictionary.ToDictionary(static item => item.Key, static item => (Object?)item.Value, StringComparer.OrdinalIgnoreCase);
        if (args is IEnumerable<KeyValuePair<String, Object?>> pairs) return pairs.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.OrdinalIgnoreCase);

        throw new NotSupportedException($"TinyHttpClient 仅支持 IDictionary、IDictionarySource 或 IEnumerable<KeyValuePair<String, Object?>> 作为参数类型。当前类型：{args.GetType().FullName}");
    }

    private static String Encode(String? data)
    {
        if (String.IsNullOrEmpty(data)) return String.Empty;

        return Uri.EscapeDataString(data).Replace("%20", "+");
    }

    private static Boolean IsBaseType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType.IsEnum) return true;
        if (actualType == typeof(Guid) || actualType == typeof(DateTimeOffset) || actualType == typeof(TimeSpan)) return true;

        return Type.GetTypeCode(actualType) != TypeCode.Object;
    }
    #endregion

    #region 主要方法
    /// <summary>异步获取字符串</summary>
    /// <param name="url">地址</param>
    /// <returns>响应字符串</returns>
    public async Task<String?> GetStringAsync(String url)
    {
        var request = new HttpRequest
        {
            RequestUri = new Uri(url),
        };

        using var response = await SendAsync(request).ConfigureAwait(false);
        return response?.Body?.ToStr();
    }

    /// <summary>异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="method">Get/Post</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <returns>调用结果</returns>
    public async Task<TResult?> InvokeAsync<TResult>(String method, String action, Object? args = null)
    {
        var baseAddress = BaseAddress ?? throw new ArgumentNullException(nameof(BaseAddress));
        var request = BuildRequest(baseAddress, method, action, args);

        using var response = await SendAsync(request).ConfigureAwait(false);
        if (response == null || response.Body == null || response.Body.Length == 0) return default;

        return ProcessResponse<TResult>(response.Body);
    }

    private HttpRequest BuildRequest(Uri baseAddress, String method, String action, Object? args)
    {
        var request = new HttpRequest
        {
            Method = method.ToUpperInvariant(),
            RequestUri = new Uri(baseAddress, action),
            KeepAlive = KeepAlive,
        };

        if (args == null) return request;

        var parameters = GetValues(args);
        if (method.EqualIgnoreCase("Post"))
            request.Body = (ArrayPacket)JsonHost.Write(parameters, JsonOptions ?? new JsonOptions()).GetBytes();
        else
        {
            var builder = Pool.StringBuilder.Get();
            builder.Append(action);
            builder.Append('?');

            var first = true;
            foreach (var item in parameters)
            {
                if (!first) builder.Append('&');
                first = false;

                var value = item.Value is DateTime dateTime ? dateTime.ToFullString() : item.Value + String.Empty;
                builder.AppendFormat("{0}={1}", item.Key, Encode(value));
            }

            request.RequestUri = new Uri(baseAddress, builder.Return(true));
        }

        return request;
    }

    private TResult? ProcessResponse<TResult>(IPacket packet)
    {
        var text = packet.ToStr();
        if (IsBaseType(typeof(TResult))) return (TResult?)System.Convert.ChangeType(text, Nullable.GetUnderlyingType(typeof(TResult)) ?? typeof(TResult), CultureInfo.InvariantCulture);

        var obj = JsonHost.Parse(text);
        if (obj is TResult result) return result;

        var dictionary = obj as IDictionary<String, Object?>;
        if (dictionary == null || !dictionary.TryGetValue("data", out var data)) throw new InvalidDataException("Unrecognized response data");

        if (dictionary.TryGetValue("result", out var result2))
        {
            if (result2 is Boolean flag && !flag) throw new InvalidOperationException($"remote error: {data}");
        }
        else if (dictionary.TryGetValue("code", out var code))
        {
            if (code is Int32 intCode && intCode != 0) throw new ApiException(intCode, data + String.Empty);
        }
        else
        {
            throw new InvalidDataException("Unrecognized response data");
        }

        if (data == null) return default;

        return JsonHost.Convert<TResult>(data);
    }
    #endregion

    #region 日志
    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>写日志</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">参数</param>
    public void WriteLog(String format, params Object?[] args) => Log?.Info(format, args);
    #endregion
}