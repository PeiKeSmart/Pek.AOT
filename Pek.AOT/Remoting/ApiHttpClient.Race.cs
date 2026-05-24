using System.Diagnostics;

using Pek;
using Pek.Extension;
using Pek.Http;
using Pek.Log;
using Pek.Serialization;

namespace Pek.Remoting;

public partial class ApiHttpClient
{
    /// <summary>竞速下载文件到本地并校验哈希</summary>
    /// <param name="requestUri">请求资源地址</param>
    /// <param name="fileName">目标文件名</param>
    /// <param name="expectedHash">预期哈希字符串</param>
    /// <param name="useHeadCheck">是否使用HEAD请求做先行检查</param>
    /// <param name="cancellationToken">取消通知</param>
    public virtual async Task DownloadFileRaceAsync(String requestUri, String fileName, String? expectedHash, Boolean useHeadCheck = false, CancellationToken cancellationToken = default)
    {
        var available = await GetRaceServicesAsync(cancellationToken).ConfigureAwait(false);
        if (available.Count == 0) throw new XException("No available service nodes!");

        if (available.Count == 1)
        {
            await DownloadFileAsync(requestUri, fileName, expectedHash, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (expectedHash.IsNullOrEmpty()) useHeadCheck = false;

        using var span = Tracer?.NewSpan($"race:{requestUri}", new { fileName, expectedHash, useHeadCheck });
        span?.AppendTag(available.Join(",", item => $"{item.Score}*{item.UriName}"));

        using var raceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var method = useHeadCheck ? HttpMethod.Head : HttpMethod.Get;
        var tasks = available.Select(item => SendRaceRequestAsync(item, item.Score, method, requestUri, null, null, raceCts.Token)).ToList();

        ServiceEndpoint? selectedService = null;
        HttpResponseMessage? selectedResponse = null;

        try
        {
            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(completed);

                var (service, response, _) = await completed.ConfigureAwait(false);
                if (response != null && response.IsSuccessStatusCode)
                {
                    if (expectedHash.IsNullOrEmpty())
                    {
                        selectedService = service;
                        selectedResponse = response;
                        break;
                    }

                    if (MatchHashFromHeaders(response, expectedHash))
                    {
                        selectedService = service;
                        selectedResponse = response;
                        break;
                    }
                }

                response?.Dispose();
            }

            if (selectedService == null || selectedResponse == null)
                throw new InvalidOperationException("No available service nodes!");

            raceCts.Cancel();

            var downloadResponse = selectedResponse;
            if (useHeadCheck)
            {
                selectedResponse.Dispose();

                var client = EnsureClient(selectedService);
                using var request = BuildRequest(HttpMethod.Get, requestUri, null, null);
                downloadResponse = await SendOnServiceAsync(request, selectedService, client, false, cancellationToken).ConfigureAwait(false);
                downloadResponse.EnsureSuccessStatusCode();
            }

            _currentService = selectedService;
            Source = selectedService.Name;

            var stream = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await HttpHelper.SaveFileAsync(stream, fileName, expectedHash, cancellationToken).ConfigureAwait(false);
            Current = selectedService;

            downloadResponse.Dispose();
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
        finally
        {
            _ = CleanupTasksAsync(tasks);
        }
    }

    /// <summary>竞速调用，并行请求所有可用服务地址，选取最快成功返回的结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="method">请求方法</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>调用结果</returns>
    public virtual async Task<TResult?> InvokeRaceAsync<TResult>(HttpMethod method, String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        var available = await GetRaceServicesAsync(cancellationToken).ConfigureAwait(false);
        if (available.Count == 0) throw new XException("No available service nodes!");

        if (available.Count == 1) return await InvokeAsync<TResult>(method, action, args, null, cancellationToken).ConfigureAwait(false);

        var returnType = typeof(TResult);

        using var span = Tracer?.NewSpan($"race:{action}", args);
        span?.AppendTag(available.Join(",", item => $"{item.Score}*{item.UriName}"));

        using var raceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = available.Select(item => SendRaceRequestAsync(item, item.Score, method, action, args, returnType, raceCts.Token)).ToList();

        ServiceEndpoint? selectedService = null;
        HttpResponseMessage? selectedResponse = null;

        try
        {
            while (tasks.Count > 0)
            {
                var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(completed);

                var (service, response, _) = await completed.ConfigureAwait(false);
                if (response == null || !response.IsSuccessStatusCode)
                {
                    response?.Dispose();
                    continue;
                }

                selectedService = service;
                selectedResponse = response;
                break;
            }

            if (selectedService == null || selectedResponse == null)
                throw new InvalidOperationException("No available service nodes!");

            raceCts.Cancel();

            _currentService = selectedService;
            Source = selectedService.Name;

            var jsonHost = JsonHost ?? ServiceProvider?.GetService(typeof(IJsonHost)) as IJsonHost ?? JsonHelper.Default;
            var result = await ApiHelper.ProcessResponse<TResult>(selectedResponse, CodeName, DataName, jsonHost).ConfigureAwait(false);

            Current = selectedService;
            return result;
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
        finally
        {
            _ = CleanupTasksAsync(tasks);
        }
    }

    /// <summary>竞速调用，并行请求所有可用服务地址，选取最快成功返回的结果</summary>
    /// <typeparam name="TResult">返回类型</typeparam>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>调用结果</returns>
    public Task<TResult?> InvokeRaceAsync<TResult>(String action, Object? args = null, CancellationToken cancellationToken = default)
    {
        var method = HttpMethod.Post;
        if (args == null || IsBaseType(args.GetType()) || action.StartsWithIgnoreCase("Get") || action.Contains("/get", StringComparison.OrdinalIgnoreCase))
            method = HttpMethod.Get;

        return InvokeRaceAsync<TResult>(method, action, args, cancellationToken);
    }

    /// <summary>发送竞速请求并返回响应</summary>
    /// <param name="service">服务节点</param>
    /// <param name="delay">启动延迟（毫秒）</param>
    /// <param name="method">请求方法</param>
    /// <param name="action">服务操作</param>
    /// <param name="args">参数</param>
    /// <param name="returnType">返回类型</param>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>服务、响应和异常</returns>
    private async Task<(ServiceEndpoint Service, HttpResponseMessage? Response, Exception? Error)> SendRaceRequestAsync(ServiceEndpoint service, Int32 delay, HttpMethod method, String action, Object? args, Type? returnType, CancellationToken cancellationToken)
    {
        if (delay > 0) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested) return (service, null, new OperationCanceledException());

        var watch = Stopwatch.StartNew();
        try
        {
            var client = EnsureClient(service);
            using var request = BuildRequest(method, action, args, returnType);

            var response = await SendOnServiceAsync(request, service, client, true, cancellationToken).ConfigureAwait(false);

            if (LoadBalancer is RaceLoadBalancer raceLoadBalancer) raceLoadBalancer.MarkSuccess(service, watch.Elapsed);

            return (service, response, null);
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                if (LoadBalancer is RaceLoadBalancer raceLoadBalancer) raceLoadBalancer.MarkFailure(service, ex);
                service.MarkFailure(ShieldingTime);
            }

            return (service, null, ex);
        }
    }

    /// <summary>异步清理任务列表中的响应</summary>
    /// <param name="tasks">任务列表</param>
    private static async Task CleanupTasksAsync(IList<Task<(ServiceEndpoint Service, HttpResponseMessage? Response, Exception? Error)>> tasks)
    {
        foreach (var task in tasks)
        {
            try { (await task.ConfigureAwait(false)).Response?.Dispose(); } catch { }
        }
    }

    /// <summary>从响应头提取哈希并与预期哈希匹配</summary>
    /// <param name="response">响应</param>
    /// <param name="expectedHash">预期哈希</param>
    /// <returns>是否匹配</returns>
    private static Boolean MatchHashFromHeaders(HttpResponseMessage response, String expectedHash)
    {
        if (expectedHash.IsNullOrEmpty()) return false;

        var (expectedAlgorithm, expectedValue) = ParseHash(expectedHash);
        if (expectedValue.IsNullOrEmpty()) return false;

        var headers = response.Headers;
        var contentHeaders = response.Content.Headers;

        if (headers.TryGetValues("Digest", out var digestValues))
        {
            var value = digestValues.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
            if (TryMatchHash(value, '=', expectedAlgorithm, expectedValue, null)) return true;
        }

        if (headers.TryGetValues("X-File-Hash", out var fileHashValues))
        {
            if (TryMatchHash(fileHashValues.FirstOrDefault(), ':', expectedAlgorithm, expectedValue, null)) return true;
        }

        if (headers.TryGetValues("X-Content-MD5", out var md5Values) || contentHeaders.TryGetValues("Content-MD5", out md5Values))
        {
            if (TryMatchHash(md5Values.FirstOrDefault(), '$', expectedAlgorithm, expectedValue, "md5")) return true;
        }

        if (headers.TryGetValues("X-Content-SHA256", out var sha256Values) || contentHeaders.TryGetValues("Content-SHA256", out sha256Values))
        {
            if (TryMatchHash(sha256Values.FirstOrDefault(), '$', expectedAlgorithm, expectedValue, "sha256")) return true;
        }

        var etag = headers.ETag?.Tag?.Trim().Trim('"');
        if (!etag.IsNullOrEmpty())
        {
            var p = etag.IndexOf('$');
            var actualAlgorithm = p > 0 ? etag[..p] : InferAlgorithm(etag);
            var actualValue = (p > 0 ? etag[(p + 1)..] : etag).Trim('"');
            if (actualAlgorithm.EqualIgnoreCase(expectedAlgorithm) && actualValue.EqualIgnoreCase(expectedValue)) return true;
        }

        return false;
    }

    /// <summary>解析哈希字符串为算法和哈希值</summary>
    /// <param name="hash">哈希字符串</param>
    /// <returns>算法和哈希值</returns>
    private static (String Algorithm, String Hash) ParseHash(String hash)
    {
        if (hash.IsNullOrEmpty()) return (String.Empty, String.Empty);

        hash = hash.Replace(':', '$');
        var p = hash.IndexOf('$');
        var algorithm = p > 0 ? hash[..p] : InferAlgorithm(hash);
        var value = (p > 0 ? hash[(p + 1)..] : hash).Trim('"');
        return (algorithm, value);
    }

    /// <summary>尝试匹配哈希值</summary>
    /// <param name="value">实际值</param>
    /// <param name="separator">分隔符</param>
    /// <param name="expectedAlgorithm">预期算法</param>
    /// <param name="expectedHash">预期哈希</param>
    /// <param name="defaultAlgorithm">默认算法</param>
    /// <returns>是否匹配</returns>
    private static Boolean TryMatchHash(String? value, Char separator, String expectedAlgorithm, String expectedHash, String? defaultAlgorithm)
    {
        if (value.IsNullOrEmpty()) return false;

        value = value.Trim().Trim('"');
        var p = value.IndexOf(separator);
        var actualAlgorithm = p > 0 ? value[..p] : (defaultAlgorithm ?? InferAlgorithm(value));
        var actualHash = (p > 0 ? value[(p + 1)..] : value).Trim('"');

        return actualAlgorithm.EqualIgnoreCase(expectedAlgorithm) && actualHash.EqualIgnoreCase(expectedHash);
    }

    /// <summary>根据哈希长度推断算法</summary>
    /// <param name="hash">哈希值</param>
    /// <returns>算法名</returns>
    private static String InferAlgorithm(String hash)
    {
        var len = hash.Trim().Trim('"').Length;
        return len switch
        {
            8 => "crc32",
            16 or 32 => "md5",
            40 => "sha1",
            64 => "sha256",
            128 => "sha512",
            _ => "md5"
        };
    }

    /// <summary>获取所有可用服务列表用于竞速调用</summary>
    /// <param name="cancellationToken">取消通知</param>
    /// <returns>可用服务列表</returns>
    private async Task<IList<ServiceEndpoint>> GetRaceServicesAsync(CancellationToken cancellationToken)
    {
        if (LoadBalancer is RaceLoadBalancer raceLoadBalancer)
            return await raceLoadBalancer.GetAllServicesAsync(Services, false, cancellationToken).ConfigureAwait(false);

        var available = Services.Where(item => item.IsAvailable()).ToList();
        for (var i = 0; i < available.Count; i++)
        {
            available[i].Score = i * 100;
        }

        return available;
    }
}