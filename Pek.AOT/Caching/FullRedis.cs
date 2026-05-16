#nullable enable

using Pek.Data;
using Pek.Extension;
using Pek.Messaging;
using Pek.Serialization;

namespace Pek.Caching;

/// <summary>增强版 Redis</summary>
public class FullRedis : Redis
{
    /// <summary>键前缀</summary>
    public String? Prefix { get; set; }

    /// <summary>根据连接字符串创建</summary>
    /// <param name="config">连接字符串</param>
    /// <returns>实例</returns>
    public static FullRedis Create(String config)
    {
        var redis = new FullRedis();
        redis.Init(config);
        return redis;
    }

    /// <summary>实例化</summary>
    public FullRedis() : base() { }

    /// <summary>实例化</summary>
    /// <param name="server">服务器</param>
    /// <param name="password">密码</param>
    /// <param name="db">库</param>
    public FullRedis(String server, String password, Int32 db) : base(server, password, db) { }

    /// <summary>实例化</summary>
    /// <param name="server">服务器</param>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="db">库</param>
    public FullRedis(String server, String userName, String password, Int32 db) : base(server, userName, password, db) { }

    /// <summary>按照配置服务实例化</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="name">名称</param>
    public FullRedis(IServiceProvider provider, String name) : base(provider, name) { }

    /// <summary>获取带前缀的键</summary>
    /// <param name="key">原始键</param>
    /// <returns>实际键</returns>
    public virtual String GetKey(String key) => !Prefix.IsNullOrEmpty() ? key.EnsureStart(Prefix) : key;

    /// <summary>初始化配置</summary>
    /// <param name="config">配置</param>
    public override void Init(String? config)
    {
        base.Init(config);
        if (config.IsNullOrEmpty()) return;

        var dictionary =
            config.Contains(',') && !config.Contains(';')
                ? config.SplitAsDictionary("=", ",", true)
                : config.SplitAsDictionary("=", ";", true);

        if (dictionary.TryGetValue("Prefix", out var prefix)) Prefix = prefix;
    }

    /// <summary>创建子库</summary>
    /// <param name="db">库编号</param>
    /// <returns>子库</returns>
    public override Redis CreateSub(Int32 db)
    {
        var redis = (FullRedis)base.CreateSub(db);
        redis.Prefix = Prefix;
        return redis;
    }

    /// <summary>设置缓存项</summary>
    public override Boolean Set<T>(String key, T value, Int32 expire = -1) => base.Set(GetKey(key), value, expire);

    /// <summary>获取缓存项</summary>
    public override T Get<T>(String key) => base.Get<T>(GetKey(key));

    /// <summary>批量移除缓存项</summary>
    public override Int32 Remove(params String[] keys) => base.Remove([.. keys.Select(GetKey)]);

    /// <summary>是否包含键</summary>
    public override Boolean ContainsKey(String key) => base.ContainsKey(GetKey(key));

    /// <summary>设置过期时间</summary>
    public override Boolean SetExpire(String key, TimeSpan expire) => base.SetExpire(GetKey(key), expire);

    /// <summary>获取过期时间</summary>
    public override TimeSpan GetExpire(String key) => base.GetExpire(GetKey(key));

    /// <summary>批量设置</summary>
    public override void SetAll<T>(IDictionary<String, T> values, Int32 expire = -1) => base.SetAll(values.ToDictionary(item => GetKey(item.Key), item => item.Value), expire);

    /// <summary>获取列表</summary>
    public override IList<T> GetList<T>(String key) => base.GetList<T>(GetKey(key));

    /// <summary>获取字典</summary>
    public override IDictionary<String, T> GetDictionary<T>(String key) => base.GetDictionary<T>(GetKey(key));

    /// <summary>获取所有哈希项</summary>
    public virtual IDictionary<String, T?> GetHashAll<T>(String key)
    {
        if (GetDictionary<T>(key) is RedisHash<String, T> hash) return hash.GetAll();
        return new Dictionary<String, T?>();
    }

    /// <summary>获取队列</summary>
    public override IProducerConsumer<T> GetQueue<T>(String key) => base.GetQueue<T>(GetKey(key));

    /// <summary>获取栈</summary>
    public override IProducerConsumer<T> GetStack<T>(String key) => base.GetStack<T>(GetKey(key));

    /// <summary>获取集合</summary>
    public override ICollection<T> GetSet<T>(String key) => base.GetSet<T>(GetKey(key));

    /// <summary>获取有序集合</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>有序集合</returns>
    public virtual RedisSortedSet<T> GetSortedSet<T>(String key)
        where T : notnull
        => new(this, key);

    /// <summary>获取延迟队列</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="topic">主题</param>
    /// <returns>延迟队列</returns>
    public virtual RedisDelayQueue<T> GetDelayQueue<T>(String topic)
        where T : notnull
        => new(this, topic);

    /// <summary>获取可靠队列</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="topic">主题</param>
    /// <returns>可靠队列</returns>
    public virtual RedisReliableQueue<T> GetReliableQueue<T>(String topic)
        where T : notnull
        => new(this, topic);

    /// <summary>创建事件总线</summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="topic">主题</param>
    /// <param name="clientId">客户标识</param>
    /// <returns>事件总线</returns>
    public override IEventBus<TEvent> CreateEventBus<TEvent>(String topic, String clientId = "")
        => new QueueEventBus<TEvent>(this, topic)
        {
            Tracer = Tracer,
            Log = Log,
        };

    /// <summary>添加，已存在时不更新</summary>
    public override Boolean Add<T>(String key, T value, Int32 expire = -1) => base.Add(GetKey(key), value, expire);

    /// <summary>替换并返回旧值</summary>
    public override T Replace<T>(String key, T value) => base.Replace(GetKey(key), value);

    /// <summary>尝试获取缓存项</summary>
    public override Boolean TryGetValue<T>(String key, [System.Diagnostics.CodeAnalysis.MaybeNull] out T value) => base.TryGetValue(GetKey(key), out value);

    /// <summary>整数累加</summary>
    public override Int64 Increment(String key, Int64 value) => base.Increment(GetKey(key), value);

    /// <summary>浮点累加</summary>
    public override Double Increment(String key, Double value) => base.Increment(GetKey(key), value);

    /// <summary>整数递减</summary>
    public override Int64 Decrement(String key, Int64 value) => base.Decrement(GetKey(key), value);

    /// <summary>浮点递减</summary>
    public override Double Decrement(String key, Double value) => base.Decrement(GetKey(key), value);

    /// <summary>搜索键</summary>
    public override IEnumerable<String> Search(String pattern, Int32 offset = 0, Int32 count = -1) => base.Search(GetKey(pattern), offset, count);

    /// <summary>申请分布式锁</summary>
    public override IDisposable? AcquireLock(String key, Int32 msTimeout) => base.AcquireLock(GetKey(key), msTimeout);

    /// <summary>申请分布式锁</summary>
    public override IDisposable? AcquireLock(String key, Int32 msTimeout, Int32 msExpire, Boolean throwOnFailure) => base.AcquireLock(GetKey(key), msTimeout, msExpire, throwOnFailure);

    /// <summary>获取键类型</summary>
    /// <param name="key">键</param>
    /// <returns>类型</returns>
    public virtual String? TYPE(String key) => Execute(GetKey(key), redisClient => redisClient.Execute<String>("TYPE", GetKey(key)));

    /// <summary>重命名键</summary>
    /// <param name="key">原键</param>
    /// <param name="newKey">新键</param>
    /// <param name="overwrite">是否覆盖</param>
    /// <returns>是否成功</returns>
    public virtual Boolean Rename(String key, String newKey, Boolean overwrite = true)
    {
        var source = GetKey(key);
        var destination = GetKey(newKey);
        var command = overwrite ? "RENAME" : "RENAMENX";
        var result = Execute(source, redisClient => redisClient.Execute<String>(command, source, destination), true);
        if (result.IsNullOrEmpty()) return false;
        return result == "OK" || result.ToInt() > 0;
    }

    /// <summary>附加字符串</summary>
    public virtual Int32 Append(String key, String value) => Execute(GetKey(key), redisClient => redisClient.Execute<Int32>("APPEND", GetKey(key), value), true);

    /// <summary>获取字符串区间</summary>
    public virtual String? GetRange(String key, Int32 start, Int32 end) => Execute(GetKey(key), redisClient => redisClient.Execute<String>("GETRANGE", GetKey(key), start, end));

    /// <summary>设置字符串区间</summary>
    public virtual String? SetRange(String key, Int32 offset, String value) => Execute(GetKey(key), redisClient => redisClient.Execute<String>("SETRANGE", GetKey(key), offset, value), true);

    /// <summary>获取字符串长度</summary>
    public virtual Int32 StrLen(String key) => Execute(GetKey(key), redisClient => redisClient.Execute<Int32>("STRLEN", GetKey(key)));

    /// <summary>模糊搜索，支持 ? 和 *</summary>
    /// <param name="pattern">匹配表达式</param>
    /// <param name="count">返回个数</param>
    /// <returns>匹配键</returns>
    [Obsolete("=>Search(String pattern, Int32 offset = 0, Int32 count = -1)")]
    public virtual IEnumerable<String> Search(String pattern, Int32 count) => Search(pattern, 0, count);

    /// <summary>向列表末尾插入</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="values">元素集合</param>
    /// <returns>列表长度</returns>
    public virtual Int32 RPUSH<T>(String key, params T[] values)
    {
        var actualKey = GetKey(key);
        var args = new List<Object?> { actualKey };
        foreach (var item in values)
        {
            args.Add(item);
        }

        return Execute(actualKey, redisClient => redisClient.Execute<Int32>("RPUSH", [.. args]), true);
    }

    /// <summary>向列表头部插入</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="values">元素集合</param>
    /// <returns>列表长度</returns>
    public virtual Int32 LPUSH<T>(String key, params T[] values)
    {
        var actualKey = GetKey(key);
        var args = new List<Object?> { actualKey };
        foreach (var item in values)
        {
            args.Add(item);
        }

        return Execute(actualKey, redisClient => redisClient.Execute<Int32>("LPUSH", [.. args]), true);
    }

    /// <summary>从列表末尾弹出一个元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>元素</returns>
    public virtual T? RPOP<T>(String key) => Execute(GetKey(key), redisClient => redisClient.Execute<T>("RPOP", GetKey(key)), true);

    /// <summary>从列表末尾弹出一个元素并插入到另一个列表头部</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">源列表</param>
    /// <param name="destination">目标列表</param>
    /// <returns>元素</returns>
    public virtual T? RPOPLPUSH<T>(String source, String destination)
    {
        var actualSource = GetKey(source);
        var actualDestination = GetKey(destination);
        return Execute(actualSource, redisClient => redisClient.Execute<T>("RPOPLPUSH", actualSource, actualDestination), true);
    }

    /// <summary>阻塞弹出并插入另一个列表</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">源列表</param>
    /// <param name="destination">目标列表</param>
    /// <param name="secTimeout">阻塞秒数</param>
    /// <returns>元素</returns>
    public virtual T? BRPOPLPUSH<T>(String source, String destination, Int32 secTimeout)
    {
        var actualSource = GetKey(source);
        var actualDestination = GetKey(destination);
        return Execute(actualSource, redisClient => redisClient.Execute<T>("BRPOPLPUSH", actualSource, actualDestination, secTimeout), true);
    }

    /// <summary>从列表头部弹出一个元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>元素</returns>
    public virtual T? LPOP<T>(String key) => Execute(GetKey(key), redisClient => redisClient.Execute<T>("LPOP", GetKey(key)), true);

    /// <summary>从列表末尾弹出一个元素，阻塞</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="keys">键集合</param>
    /// <param name="secTimeout">阻塞秒数</param>
    /// <returns>键和值</returns>
    public virtual Tuple<String, T?>? BRPOP<T>(String[] keys, Int32 secTimeout = 0)
    {
        if (keys == null || keys.Length == 0) return null;

        var actualKeys = keys.Select(GetKey).ToArray();
        var args = new List<Object?>(actualKeys.Length + 1);
        foreach (var item in actualKeys)
        {
            args.Add(item);
        }

        args.Add(secTimeout);

        var result = Execute(actualKeys[0], redisClient => redisClient.Execute<Object[]>("BRPOP", [.. args]), true);
        return DecodePopResult<T>(result);
    }

    /// <summary>从列表末尾弹出一个元素，阻塞</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="secTimeout">阻塞秒数</param>
    /// <returns>元素</returns>
    public virtual T? BRPOP<T>(String key, Int32 secTimeout = 0)
    {
        var result = BRPOP<T>([key], secTimeout);
        return result == null ? default : result.Item2;
    }

    /// <summary>从列表头部弹出一个元素，阻塞</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="keys">键集合</param>
    /// <param name="secTimeout">阻塞秒数</param>
    /// <returns>键和值</returns>
    public virtual Tuple<String, T?>? BLPOP<T>(String[] keys, Int32 secTimeout = 0)
    {
        if (keys == null || keys.Length == 0) return null;

        var actualKeys = keys.Select(GetKey).ToArray();
        var args = new List<Object?>(actualKeys.Length + 1);
        foreach (var item in actualKeys)
        {
            args.Add(item);
        }

        args.Add(secTimeout);

        var result = Execute(actualKeys[0], redisClient => redisClient.Execute<Object[]>("BLPOP", [.. args]), true);
        return DecodePopResult<T>(result);
    }

    /// <summary>从列表头部弹出一个元素，阻塞</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="secTimeout">阻塞秒数</param>
    /// <returns>元素</returns>
    public virtual T? BLPOP<T>(String key, Int32 secTimeout = 0)
    {
        var result = BLPOP<T>([key], secTimeout);
        return result == null ? default : result.Item2;
    }

    /// <summary>向集合添加多个元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="members">元素集合</param>
    /// <returns>新增数量</returns>
    public virtual Int32 SADD<T>(String key, params T[] members)
    {
        var actualKey = GetKey(key);
        var args = new List<Object?> { actualKey };
        foreach (var item in members)
        {
            args.Add(item);
        }

        return Execute(actualKey, redisClient => redisClient.Execute<Int32>("SADD", [.. args]), true);
    }

    /// <summary>向集合删除多个元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="members">元素集合</param>
    /// <returns>删除数量</returns>
    public virtual Int32 SREM<T>(String key, params T[] members)
    {
        var actualKey = GetKey(key);
        var args = new List<Object?> { actualKey };
        foreach (var item in members)
        {
            args.Add(item);
        }

        return Execute(actualKey, redisClient => redisClient.Execute<Int32>("SREM", [.. args]), true);
    }

    /// <summary>获取所有集合元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>元素数组</returns>
    public virtual T?[] SMEMBERS<T>(String key) => DecodeArray<T>(Execute(GetKey(key), redisClient => redisClient.Execute<Object[]>("SMEMBERS", GetKey(key))));

    /// <summary>返回集合元素个数</summary>
    /// <param name="key">键</param>
    /// <returns>个数</returns>
    public virtual Int32 SCARD(String key) => Execute(GetKey(key), redisClient => redisClient.Execute<Int32>("SCARD", GetKey(key)));

    /// <summary>成员是否属于集合</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="member">成员</param>
    /// <returns>结果</returns>
    public virtual Int32 SISMEMBER<T>(String key, T member) => Execute(GetKey(key), redisClient => redisClient.Execute<Int32>("SISMEMBER", GetKey(key), member));

    /// <summary>随机获取多个元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="count">数量</param>
    /// <returns>元素数组</returns>
    public virtual T?[] SRANDMEMBER<T>(String key, Int32 count) => DecodeArray<T>(Execute(GetKey(key), redisClient => redisClient.Execute<Object[]>("SRANDMEMBER", GetKey(key), count)));

    /// <summary>随机获取并弹出多个元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="count">数量</param>
    /// <returns>元素数组</returns>
    public virtual T?[] SPOP<T>(String key, Int32 count) => DecodeArray<T>(Execute(GetKey(key), redisClient => redisClient.Execute<Object[]>("SPOP", GetKey(key), count), true));

    /// <summary>执行 Lua 脚本</summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="script">脚本</param>
    /// <param name="keys">键集合</param>
    /// <param name="args">参数集合</param>
    /// <returns>执行结果</returns>
    public virtual T? Eval<T>(String script, String[] keys, Object[] args)
    {
        keys ??= [];
        args ??= [];

        var actualKeys = keys.Select(GetKey).ToArray();
        var parameters = new List<Object?>
        {
            script,
            actualKeys.Length
        };

        foreach (var item in actualKeys)
        {
            parameters.Add(item);
        }

        foreach (var item in args)
        {
            parameters.Add(item);
        }

        var routeKey = actualKeys.FirstOrDefault() ?? String.Empty;
        return Execute(routeKey, redisClient => redisClient.Execute<T>("EVAL", [.. parameters]), true);
    }

    private static Tuple<String, T?>? DecodePopResult<T>(Object[]? values)
    {
        if (values == null || values.Length != 2) return null;

        var key = values[0] switch
        {
            IPacket packet => packet.ToStr(),
            null => String.Empty,
            _ => values[0].ToString() ?? String.Empty,
        };

        return new Tuple<String, T?>(key, DecodeValue<T>(values[1]));
    }

    private static T?[] DecodeArray<T>(Object[]? values)
    {
        if (values == null || values.Length == 0) return [];

        var result = new T?[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = DecodeValue<T>(values[i]);
        }

        return result;
    }

    private static T? DecodeValue<T>(Object? value)
    {
        if (value == null) return default;
        if (value is T target) return target;
        if (value is IPacket packet)
        {
            var packetText = packet.ToStr();
            if (typeof(T) == typeof(String)) return (T?)(Object?)packetText;

            var entity = packetText.ToJsonEntity(typeof(T));
            if (entity is T packetEntity) return packetEntity;

            try
            {
                return (T?)System.Convert.ChangeType(packetText, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            }
            catch
            {
                return default;
            }
        }

        if (value is String text)
        {
            if (typeof(T) == typeof(String)) return (T?)(Object?)text;

            var entity = text.ToJsonEntity(typeof(T));
            if (entity is T stringEntity) return stringEntity;

            try
            {
                return (T?)System.Convert.ChangeType(text, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    /// <summary>创建实例</summary>
    protected override Redis CreateInstance() => new FullRedis();
}

#nullable restore