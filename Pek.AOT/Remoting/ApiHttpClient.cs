using System.Net.Http.Headers;

using Pek;
using Pek.Configuration;
using Pek.Data;
using Pek.Extension;
using Pek.Http;
using Pek.Log;
using Pek.Serialization;

namespace Pek.Remoting;

/// <summary>Http应用接口客户端</summary>
/// <remarks>ApiHttpClient 封装多个服务地址，统一提供负载均衡、故障转移和竞速调用能力。</remarks>
public partial class ApiHttpClient : DisposeBase, IApiClient, IConfigMapping, ILogFeature, ITracerFeature
{
    #region 属性
    /// <summary>令牌。每次请求携带</summary>
    public String? Token { get; set; }

    /// <summary>超时时间。默认15000ms</summary>
    public Int32 Timeout { get; set; } = 15_000;

    /// <summary>是否使用系统代理设置。默认false</summary>
    public Boolean UseProxy { get; set; }

    /// <summary>负载均衡器</summary>
    public ILoadBalancer LoadBalancer { get; private set; }

    /// <summary>负载均衡模式。默认Failover故障转移</summary>
    public LoadBalanceMode LoadBalanceMode
    {
        get => LoadBalancer.Mode;
        set
        {
            if (LoadBalancer.Mode != value) LoadBalancer = CreateLoadBalancer(value);
        }
    }

    /// <summary>加权轮询负载均衡。默认false只使用故障转移</summary>
    [Obsolete("请使用 LoadBalanceMode 属性")]
    public Boolean RoundRobin
    {
        get => LoadBalanceMode == LoadBalanceMode.RoundRobin;
        set => LoadBalanceMode = value ? LoadBalanceMode.RoundRobin : LoadBalanceMode.Failover;
    }

    /// <summary>不可用节点的屏蔽时间。默认60秒</summary>
    public Int32 ShieldingTime
    {
        get => LoadBalancer.ShieldingTime;
        set
        {
            LoadBalancer.ShieldingTime = value;
            _shieldingTime = value;
        }
    }
    private Int32 _shieldingTime = 60;

    /// <summary>身份验证</summary>
    public AuthenticationHeaderValue? Authentication { get; set; }

    /// <summary>证书验证。进行SSL通信时，是否验证证书有效性，默认false不验证</summary>
    public Boolean CertificateValidation { get; set; }

    /// <summary>默认用户浏览器UserAgent</summary>
    public String? DefaultUserAgent { get; set; }

    /// <summary>Json序列化主机</summary>
    public IJsonHost? JsonHost { get; set; }

    /// <summary>服务提供者</summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>创建请求时触发</summary>
    public event EventHandler<HttpRequestEventArgs>? OnRequest;

    /// <summary>创建客户端时触发</summary>
    public event EventHandler<HttpClientEventArgs>? OnCreateClient;

    /// <summary>Http过滤器</summary>
    public IHttpFilter? Filter { get; set; }

    /// <summary>状态码字段名。例如code/status等</summary>
    public String? CodeName { get; set; }

    /// <summary>数据体字段名。例如data/result等</summary>
    public String? DataName { get; set; }

    /// <summary>服务器源。正在使用的服务器</summary>
    public String? Source { get; private set; }

    /// <summary>调用统计</summary>
    public ICounter? StatInvoke { get; set; }

    /// <summary>慢追踪。远程调用或处理时间超过该值时，输出慢调用日志，默认5000ms</summary>
    public Int32 SlowTrace { get; set; } = 5_000;

    /// <summary>跟踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>服务列表。用于负载均衡和故障转移</summary>
    public IList<ServiceEndpoint> Services { get; set; } = [];

    /// <summary>当前服务</summary>
    protected ServiceEndpoint? _currentService;

    /// <summary>正在使用的服务点</summary>
    public ServiceEndpoint? Current { get; private set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ApiHttpClient() => LoadBalancer = CreateLoadBalancer(LoadBalanceMode.Failover);

    /// <summary>实例化</summary>
    /// <param name="urls">地址集合。多地址逗号分隔，支持权重</param>
    public ApiHttpClient(String urls) : this() => SetServer(urls);

    /// <summary>按照配置服务实例化，用于DI</summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="name">缓存名称，也是配置中心key</param>
    public ApiHttpClient(IServiceProvider serviceProvider, String name) : this()
    {
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        ServiceProvider = serviceProvider;
        Tracer = serviceProvider.GetService(typeof(ITracer)) as ITracer;

        if (serviceProvider.GetService(typeof(IConfigProvider)) is IConfigProvider configProvider)
        {
            var section = configProvider.GetSection(name);
            if (section != null)
                ((IConfigMapping)this).MapConfig(configProvider, section);
            else if (configProvider[name] is String value)
                SetServer(value);
        }
    }

    /// <summary>创建负载均衡器</summary>
    /// <param name="mode">负载均衡模式</param>
    /// <returns>负载均衡器</returns>
    protected virtual ILoadBalancer CreateLoadBalancer(LoadBalanceMode mode)
    {
        var lb = mode switch
        {
            LoadBalanceMode.RoundRobin => (ILoadBalancer)new WeightedRoundRobinLoadBalancer(),
            LoadBalanceMode.Race => new RaceLoadBalancer(),
            _ => new FailoverLoadBalancer(),
        };

        if (lb is LoadBalancerBase baseBalancer)
        {
            baseBalancer.ShieldingTime = _shieldingTime;
            baseBalancer.Log = Log;
        }

        if (lb is ITracerFeature tracerFeature) tracerFeature.Tracer = Tracer;
        if (lb is ILogFeature logFeature) logFeature.Log = Log;

        return lb;
    }
    #endregion

    #region 方法
    /// <summary>添加服务地址</summary>
    /// <param name="name">名称</param>
    /// <param name="address">地址，支持名称和权重</param>
    /// <returns>服务节点</returns>
    public ServiceEndpoint Add(String name, String address) => ParseAndAdd(Services, name, address);

    /// <summary>添加服务地址</summary>
    /// <param name="name">名称</param>
    /// <param name="uri">地址</param>
    /// <returns>服务节点</returns>
    public ServiceEndpoint Add(String name, Uri uri)
    {
        var service = new ServiceEndpoint { Name = name };
        service.SetAddress(uri);

        Services.Add(service);

        return service;
    }

    private static ServiceEndpoint ParseAndAdd(IList<ServiceEndpoint> services, String name, String address, Int32 weight = 0)
    {
        var url = address;
        var service = new ServiceEndpoint { Name = name };

        var p = url.IndexOf("://", StringComparison.Ordinal);
        if (p > 0)
        {
            var p2 = url.IndexOf('=');
            if (p2 > 0 && p2 < p)
            {
                service.Name = url[..p2];
                url = url[(p2 + 1)..];
            }

            p = url.IndexOf("://", StringComparison.Ordinal);
            p2 = url.IndexOf("*http", StringComparison.OrdinalIgnoreCase);
            if (p2 > 0 && p2 < p)
            {
                service.Weight = url[..p2].ToInt();
                url = url[(p2 + 1)..];
            }
        }

        p = url.IndexOf("#token=", StringComparison.OrdinalIgnoreCase);
        if (p > 0)
        {
            service.Token = url[(p + 7)..];
            url = url[..p];
        }

        service.SetAddress(new Uri(url));
        if (service.Weight <= 1 && weight > 0) service.Weight = weight;

        services.Add(service);

        return service;
    }

    private String? _lastUrls;

    /// <summary>设置服务端地址。如果新地址跟旧地址不同，将会替换旧地址构造的Services</summary>
    /// <param name="urls">地址集。多个地址逗号隔开</param>
    public void SetServer(String urls)
    {
        if (!urls.IsNullOrEmpty() && urls != _lastUrls)
        {
            var services = new List<ServiceEndpoint>();
            var items = urls.Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < items.Length; i++)
            {
                if (!items[i].IsNullOrEmpty()) ParseAndAdd(services, "service" + (i + 1), items[i]);
            }

            Services = services;
            _lastUrls = urls;
        }
    }

    /// <summary>添加服务端地址</summary>
    /// <param name="prefix">名称前缀</param>
    /// <param name="urls">地址集</param>
    /// <param name="weight">权重</param>
    /// <returns>添加的服务节点</returns>
    public IList<ServiceEndpoint> AddServer(String prefix, String urls, Int32 weight = 0)
    {
        if (prefix.IsNullOrEmpty()) prefix = "service";

        var idx = 1;
        var result = new List<ServiceEndpoint>();
        var items = urls.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var services = Services;
        foreach (var address in items)
        {
            if (address.IsNullOrEmpty()) continue;

            var name = prefix;
            while (name.IsNullOrEmpty() || services.Any(item => item.Name == name)) name = prefix + ++idx;

            var service = ParseAndAdd(services, name, address, weight);
            result.Add(service);
        }

        return result;
    }

    void IConfigMapping.MapConfig(IConfigProvider provider, IConfigSection section)
    {
        if (section != null && section.Value != null) SetServer(section.Value);
    }
    #endregion

    #region 核心方法
    /// <summary>异步获取，参数构造在Url</summary>
    public Task<TResult?> GetAsync<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(HttpMethod.Get, action, args);

    /// <summary>同步获取，参数构造在Url</summary>
    public TResult? Get<TResult>(String action, Object? args = null) => GetAsync<TResult>(action, args).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步提交，参数Json打包在Body</summary>
    public Task<TResult?> PostAsync<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(HttpMethod.Post, action, args);

    /// <summary>同步提交，参数Json打包在Body</summary>
    public TResult? Post<TResult>(String action, Object? args = null) => PostAsync<TResult>(action, args).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>异步上传，参数Json打包在Body</summary>
    public Task<TResult?> PutAsync<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(HttpMethod.Put, action, args);

    /// <summary>异步修改，参数Json打包在Body</summary>
    public Task<TResult?> PatchAsync<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(HttpMethod.Patch, action, args);

    /// <summary>异步删除，参数Json打包在Body</summary>
    public Task<TResult?> DeleteAsync<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(HttpMethod.Delete, action, args);

    /// <summary>异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="method">请求方法</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="onRequest">请求头回调</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>调用结果</returns>
    public virtual async Task<TResult?> InvokeAsync<TResult>(HttpMethod method, String action, Object? args = null, Action<HttpRequestMessage>? onRequest = null, CancellationToken cancellationToken = default)
    {
        var returnType = typeof(TResult);
        var services = Services;

        using var span = Tracer?.NewSpan(action, args);

        for (var i = 0; i < services.Count; i++)
        {
            using var request = BuildRequest(method, action, args, returnType);
            onRequest?.Invoke(request);

            var filter = Filter;
            try
            {
                using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

                var jsonHost = JsonHost ?? ServiceProvider?.GetService(typeof(IJsonHost)) as IJsonHost ?? JsonHelper.Default;
                return await ApiHelper.ProcessResponse<TResult>(response, CodeName, DataName, jsonHost).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                span?.AppendTag(ex.Message);

                while (ex is AggregateException aggregateException && aggregateException.InnerException != null) ex = aggregateException.InnerException;
                ex.Source = _currentService?.Address + "/" + action;

                var client = _currentService?.Client;
                if (client != null && filter != null) await filter.OnError(client, ex, this, cancellationToken).ConfigureAwait(false);

                if (ex is HttpRequestException or TaskCanceledException && i + 1 < services.Count) continue;

                span?.SetError(ex, null);
                throw;
            }
        }

        throw new InvalidOperationException();
    }

    /// <summary>异步调用，等待返回结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>调用结果</returns>
    public Task<TResult?> InvokeAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        var method = HttpMethod.Post;
        if (args == null || IsBaseType(args.GetType()) || action.StartsWithIgnoreCase("Get") || action.Contains("/get", StringComparison.OrdinalIgnoreCase))
            method = HttpMethod.Get;

        return InvokeAsync<TResult>(method, action, args, null, cancellationToken);
    }

    /// <summary>同步调用，阻塞等待</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <returns>调用结果</returns>
    public TResult? Invoke<TResult>(String action, Object? args = null) => InvokeAsync<TResult>(action, args, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>下载文件到本地并校验哈希</summary>
    /// <param name="requestUri">请求资源地址</param>
    /// <param name="fileName">目标文件名</param>
    /// <param name="expectedHash">预期哈希</param>
    /// <param name="cancellationToken">取消通知</param>
    public virtual async Task DownloadFileAsync(String requestUri, String fileName, String? expectedHash, CancellationToken cancellationToken = default)
    {
        var services = Services;

        var action = requestUri;
        if (requestUri.StartsWithIgnoreCase("http://", "https://")) action = new Uri(requestUri).AbsolutePath.TrimStart('/');
        using var span = Tracer?.NewSpan(action, expectedHash);

        for (var i = 0; i < services.Count; i++)
        {
            using var request = BuildRequest(HttpMethod.Get, requestUri, null, null);

            var filter = Filter;
            try
            {
                using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await HttpHelper.SaveFileAsync(stream, fileName, expectedHash, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                span?.AppendTag(ex.Message);

                while (ex is AggregateException aggregateException && aggregateException.InnerException != null) ex = aggregateException.InnerException;
                ex.Source = _currentService?.Address + "/" + action;

                var client = _currentService?.Client;
                if (client != null && filter != null) await filter.OnError(client, ex, this, cancellationToken).ConfigureAwait(false);

                if (ex is HttpRequestException or TaskCanceledException && i + 1 < services.Count) continue;

                span?.SetError(ex, null);
                throw;
            }
        }
    }
    #endregion

    #region 构造请求
    /// <summary>建立请求</summary>
    /// <param name="method">请求方法</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="returnType">返回类型</param>
    /// <returns>请求消息</returns>
    protected virtual HttpRequestMessage BuildRequest(HttpMethod method, String action, Object? args, Type? returnType)
    {
        HttpRequestMessage request;
        if (args == null)
            request = new HttpRequestMessage(method, action);
        else
        {
            var jsonHost = JsonHost ?? ServiceProvider?.GetService(typeof(IJsonHost)) as IJsonHost ?? JsonHelper.Default;
            request = ApiHelper.BuildRequest(method, action, args, jsonHost);
        }

        if (returnType != null)
        {
            if (returnType == typeof(Byte[]) || returnType == typeof(IPacket))
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            else
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        var auth = Authentication;
        if (auth == null && !Token.IsNullOrEmpty()) auth = new AuthenticationHeaderValue("Bearer", Token);
        if (auth != null) request.Headers.Authorization = auth;

        OnRequest?.Invoke(this, new HttpRequestEventArgs { Request = request });

        return request;
    }
    #endregion

    #region 调度池
    /// <summary>异步发送</summary>
    /// <param name="request">请求</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>响应消息</returns>
    protected virtual async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Services.Count == 0) throw new InvalidOperationException("Service address not added!");

        var service = LoadBalancer.GetService(Services) ?? throw new InvalidOperationException("No available service nodes!");
        Source = service.Name;
        _currentService = service;

        DefaultSpan.Current?.AppendTag($"[{service.Name}]={service.Address}");

        var statInvoke = StatInvoke;
        var startTicks = statInvoke?.StartCount();
        Exception? error = null;
        try
        {
            var client = EnsureClient(service);
            var response = await SendOnServiceAsync(request, service, client, false, cancellationToken).ConfigureAwait(false);
            Current = service;
            return response;
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            if (statInvoke != null)
            {
                var msCost = statInvoke.StopCount(startTicks) / 1000;
                if (SlowTrace > 0 && msCost >= SlowTrace) this.WriteLog("慢调用[{0}]，耗时{1:n0}ms", request.RequestUri?.AbsoluteUri, msCost);
            }

            LoadBalancer.PutService(Services, service, error);
        }
    }

    /// <summary>在指定服务地址上发生请求</summary>
    /// <param name="request">请求消息</param>
    /// <param name="service">服务节点</param>
    /// <param name="client">客户端</param>
    /// <param name="onlyHeader">仅头部响应</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>响应消息</returns>
    protected virtual async Task<HttpResponseMessage> SendOnServiceAsync(HttpRequestMessage request, ServiceEndpoint service, HttpClient client, Boolean onlyHeader, CancellationToken cancellationToken)
    {
        var filter = Filter;
        if (filter != null) await filter.OnRequest(client, request, this, cancellationToken).ConfigureAwait(false);

        var completionOption = onlyHeader ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
        var response = await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);

        if (filter != null) await filter.OnResponse(client, response, this, cancellationToken).ConfigureAwait(false);

        return response;
    }

    /// <summary>确保服务有可用的 HttpClient</summary>
    /// <param name="service">服务</param>
    /// <returns>Http客户端</returns>
    internal HttpClient EnsureClient(ServiceEndpoint service)
    {
        var client = service.Client;
        if (client == null)
        {
            if (service.CreateTime.Year < 2000) Log?.Debug("使用[{0}]：{1}", service.Name, service.Address);

            client = CreateClient();
            client.BaseAddress = service.Address;
            if (!service.Token.IsNullOrEmpty()) Token = service.Token;

            service.Client = client;
            service.CreateTime = DateTime.Now;
        }

        if (client.BaseAddress == null) client.BaseAddress = service.Address;

        return client;
    }

    /// <summary>创建客户端</summary>
    /// <returns>Http客户端</returns>
    protected virtual HttpClient CreateClient()
    {
        var handler = HttpHelper.CreateHandler(UseProxy, false, !CertificateValidation);
        if (Tracer != null) handler = new HttpTraceHandler(handler) { Tracer = Tracer };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(Timeout)
        };

        var userAgent = DefaultUserAgent;
        if (!userAgent.IsNullOrEmpty()) client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        OnCreateClient?.Invoke(this, new HttpClientEventArgs { Client = client });

        return client;
    }
    #endregion

    #region 辅助
    private static Boolean IsBaseType(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        if (actualType.IsEnum) return true;
        if (actualType == typeof(Guid) || actualType == typeof(DateTimeOffset) || actualType == typeof(TimeSpan)) return true;

        return Type.GetTypeCode(actualType) != TypeCode.Object;
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

    #region 销毁
    /// <summary>释放资源</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        if (disposing)
        {
            foreach (var item in Services)
            {
                item.Client.TryDispose();
                item.Client = null;
            }
        }

        base.Dispose(disposing);
    }
    #endregion
}