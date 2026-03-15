using System.Collections.Concurrent;
using System.Net;

using Pek.Extension;

namespace Pek.Net;

/// <summary>DNS解析器</summary>
public interface IDnsResolver
{
    /// <summary>解析域名</summary>
    /// <param name="host">域名</param>
    /// <returns>IP地址集合</returns>
    IPAddress[]? Resolve(String host);
}

/// <summary>DNS解析器，带有缓存，解析失败时使用旧数据</summary>
public class DnsResolver : IDnsResolver
{
    /// <summary>静态实例</summary>
    public static DnsResolver Instance { get; set; } = new();

    /// <summary>缓存超时时间</summary>
    public TimeSpan Expire { get; set; } = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<String, DnsItem> _cache = new();
    private readonly ConcurrentDictionary<String, Byte> _refreshing = new();

    /// <summary>解析域名</summary>
    /// <param name="host">域名</param>
    /// <returns>IP地址集合</returns>
    public IPAddress[]? Resolve(String host)
    {
        if (host.IsNullOrEmpty()) return null;

        if (_cache.TryGetValue(host, out var item))
        {
            if (item.UpdateTime.Add(Expire) <= DateTime.Now)
            {
                if (_refreshing.TryAdd(host, 0)) _ = ResolveCoreAsync(host, item, false);
            }
        }
        else
        {
            item = ResolveCoreAsync(host, null, true).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        return item?.Addresses;
    }

    /// <summary>设置缓存</summary>
    /// <param name="host">域名</param>
    /// <param name="addrs">IP地址集合</param>
    /// <param name="expire">过期时间。默认0秒，使用Expire</param>
    public void Set(String host, IPAddress[] addrs, Int32 expire = 0)
    {
        if (host.IsNullOrEmpty()) throw new ArgumentNullException(nameof(host));
        if (addrs == null) throw new ArgumentNullException(nameof(addrs));

        var item = new DnsItem
        {
            Host = host,
            Addresses = addrs,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now.AddSeconds(expire)
        };
        _cache[host] = item;
    }

    private async Task<DnsItem?> ResolveCoreAsync(String host, DnsItem? item, Boolean throwError)
    {
        try
        {
#if NET6_0_OR_GREATER
            using var source = new CancellationTokenSource(5000);
            var addrs = await Dns.GetHostAddressesAsync(host, source.Token).ConfigureAwait(false);
#else
            var task = Dns.GetHostAddressesAsync(host);
            if (!task.Wait(5000)) throw new TaskCanceledException();
            var addrs = task.ConfigureAwait(false).GetAwaiter().GetResult();
#endif
            if (addrs != null && addrs.Length > 0)
            {
                if (item == null)
                {
                    _cache[host] = item = new DnsItem
                    {
                        Host = host,
                        Addresses = addrs,
                        CreateTime = DateTime.Now,
                        UpdateTime = DateTime.Now
                    };
                }
                else
                {
                    item.Addresses = addrs;
                    item.UpdateTime = DateTime.Now;
                }
            }
        }
        catch
        {
            if (item != null) return item;
            if (throwError) throw;
        }
        finally
        {
            _refreshing.TryRemove(host, out _);
        }

        return item;
    }

    private sealed class DnsItem
    {
        public String Host { get; set; } = null!;

        public IPAddress[]? Addresses { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime UpdateTime { get; set; }
    }
}