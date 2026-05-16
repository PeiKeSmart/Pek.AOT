#nullable enable

using System.Collections;
using System.IO;
using System.Net.Sockets;

using Pek.Collections;
using Pek.Configuration;
using Pek.Data;
using Pek.Extension;
using Pek.Log;
using Pek.Net;

namespace Pek.Caching;

/// <summary>Redis 客户端</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/redis
/// </remarks>
public class Redis : Cache, IConfigMapping, ILogFeature, ITracerFeature
{
    /// <summary>服务器，带端口，支持逗号分隔的多地址</summary>
    public String? Server { get; set; }

    /// <summary>用户名。Redis 6.0 支持</summary>
    public String? UserName { get; set; }

    /// <summary>密码</summary>
    public String? Password { get; set; }

    /// <summary>目标数据库。默认 0</summary>
    public Int32 Db { get; set; }

    /// <summary>读写超时时间。默认 3000ms</summary>
    public Int32 Timeout { get; set; } = 3_000;

    /// <summary>出错重试次数。默认 3</summary>
    public Int32 Retry { get; set; } = 3;

    /// <summary>完全管道。读取操作是否合并进入管道</summary>
    public Boolean FullPipeline { get; set; }

    /// <summary>自动管道。管道操作达到一定数量时自动提交</summary>
    public Int32 AutoPipeline { get; set; }

    /// <summary>编码器。决定对象在 Redis 中的存储格式</summary>
    public IPacketEncoder Encoder { get; set; } = new RedisJsonEncoder();

    /// <summary>失败时抛出异常。默认 true</summary>
    public Boolean ThrowOnFailure { get; set; } = true;

    /// <summary>最大消息大小。超过时抛出异常</summary>
    public Int32 MaxMessageSize { get; set; } = 1024 * 1024;

    /// <summary>性能计数器</summary>
    public PerfCounter? Counter { get; set; }

    /// <summary>性能跟踪器</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private IDictionary<String, String>? _info;
    private String? _configOld;
    private MyPool? _pool;
    private NetUri[]? _servers;
    private Int32 _idxServer;
    private Int32 _idxLast = -1;
    private DateTime _nextTrace;
    private readonly ThreadLocal<RedisClient?> _client = new();

    /// <summary>服务器信息</summary>
    public IDictionary<String, String> Info => _info ??= GetInfo();

    /// <summary>实例化</summary>
    public Redis() { }

    /// <summary>实例化 Redis，指定服务器地址、密码、库</summary>
    /// <param name="server">服务器地址</param>
    /// <param name="password">密码</param>
    /// <param name="db">数据库编号</param>
    public Redis(String server, String password, Int32 db)
    {
        Server = server?.Trim();
        Password = password?.Trim();
        Db = db;
    }

    /// <summary>实例化 Redis，指定服务器地址、用户、密码、库</summary>
    /// <param name="server">服务器地址</param>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="db">数据库编号</param>
    public Redis(String server, String userName, String password, Int32 db)
    {
        Server = server?.Trim();
        UserName = userName?.Trim();
        Password = password?.Trim();
        Db = db;
    }

    /// <summary>按照配置服务实例化 Redis</summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="name">缓存名称</param>
    public Redis(IServiceProvider provider, String name)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        Name = name;
        Tracer = provider.GetService(typeof(ITracer)) as ITracer;

        if (provider.GetService(typeof(IConfigProvider)) is IConfigProvider configProvider)
        {
            var section = configProvider.GetSection(name);
            if (section != null)
                ((IConfigMapping)this).MapConfig(configProvider, section);
            else if (configProvider[name] is String value)
                Init(value);
        }
    }

    /// <summary>实例化 Redis，支持从环境变量 Redis_{Name} 读取配置</summary>
    /// <param name="name">缓存名称</param>
    public Redis(String name)
    {
        if (name.IsNullOrEmpty()) throw new ArgumentNullException(nameof(name));

        Name = name;
        foreach (DictionaryEntry item in Environment.GetEnvironmentVariables())
        {
            if (item.Key is String key && item.Value is String value && key.EqualIgnoreCase($"Redis_{name}"))
                Init(value);
        }
    }

    /// <summary>销毁</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            Commit();
        }
        catch { }

        _pool.TryDispose();
    }

    /// <summary>已重载</summary>
    /// <returns>文本描述</returns>
    public override String ToString() => $"{Name} Server={Server} Db={Db}";

    /// <summary>使用连接字符串初始化</summary>
    /// <param name="config">连接字符串</param>
    public override void Init(String? config)
    {
        if (config.IsNullOrEmpty()) return;
        if (config == _configOld) return;
        if (!_configOld.IsNullOrEmpty()) XTrace.WriteLine("Redis[{0}]连接字符串改变！", Name);

        var dictionary =
            config.Contains(',') && !config.Contains(';')
                ? config.SplitAsDictionary("=", ",", true)
                : config.SplitAsDictionary("=", ";", true);

        if (dictionary.Count > 0)
        {
            Server = dictionary.TryGetValue("Server", out var server) ? server?.Trim() : Server;
            UserName = dictionary.TryGetValue("UserName", out var userName) ? userName?.Trim() : UserName;
            Password = dictionary.TryGetValue("Password", out var password) ? password?.Trim() : Password;

            if (dictionary.TryGetValue("Db", out var db)) Db = db.ToInt();
            if (Server.IsNullOrEmpty() && dictionary.TryGetValue("[0]", out var server0)) Server = server0;

            var port = dictionary.TryGetValue("Port", out var portValue) ? portValue.ToInt() : 0;
            if (port > 0 && !Server.IsNullOrEmpty() && !Server.Contains(':')) Server += ":" + port;

            if (dictionary.TryGetValue("Timeout", out var timeout)) Timeout = timeout.ToInt(Timeout);
            else if (dictionary.TryGetValue("responseTimeout", out timeout)) Timeout = timeout.ToInt(Timeout);
            else if (dictionary.TryGetValue("connectTimeout", out timeout)) Timeout = timeout.ToInt(Timeout);

            if (dictionary.TryGetValue("ThrowOnFailure", out var throwOnFailure)) ThrowOnFailure = throwOnFailure.ToBoolean(ThrowOnFailure);
            if (dictionary.TryGetValue("MaxMessageSize", out var maxMessageSize) && maxMessageSize.ToInt(-1) >= 0) MaxMessageSize = maxMessageSize.ToInt();
            if (dictionary.TryGetValue("Expire", out var expire) && expire.ToInt(-1) >= 0) Expire = expire.ToInt();
        }

        _servers = null;
        if (!_configOld.IsNullOrEmpty())
        {
            _pool = null;
            _info = null;
        }

        _configOld = config;
    }

    /// <summary>映射配置树到当前对象</summary>
    /// <param name="provider">配置提供者</param>
    /// <param name="section">配置段</param>
    void IConfigMapping.MapConfig(IConfigProvider provider, IConfigSection section)
    {
        if (section?.Value is String value && !value.IsNullOrEmpty()) Init(value);
    }

    /// <summary>连接池</summary>
    public IPool<RedisClient> Pool
    {
        get
        {
            if (_pool != null) return _pool;

            lock (this)
            {
                if (_pool != null) return _pool;

                _pool = new MyPool
                {
                    Name = Name + "Pool",
                    Instance = this,
                    Min = 10,
                    Max = 100_000,
                    IdleTime = 30,
                    AllIdleTime = 300,
                    Log = Log,
                };

                return _pool;
            }
        }
    }

    /// <summary>执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="key">命令键</param>
    /// <param name="func">执行函数</param>
    /// <param name="write">是否写操作</param>
    /// <returns>执行结果</returns>
    public virtual TResult Execute<TResult>(String? key, Func<RedisClient, TResult> func, Boolean write = false)
    {
        if (write || FullPipeline)
        {
            var redisClient = _client.Value;
            if (redisClient == null && AutoPipeline > 0) redisClient = StartPipeline();
            if (redisClient != null)
            {
                var result = func(redisClient);
                if (AutoPipeline > 0 && redisClient.PipelineCommands >= AutoPipeline)
                {
                    StopPipeline(true);
                    StartPipeline();
                }

                return result;
            }
        }

        if (!write) StopPipeline(true);

        var counter = Counter?.StartCount();
        var retry = 0;
        var delay = 100;
        do
        {
            var pool = Pool;
            var client = pool.Get();
            try
            {
                client.Reset();
                return func(client);
            }
            catch (InvalidDataException)
            {
                if (retry++ >= Retry) throw;

                client.TryDispose();
                Thread.Sleep(delay);
                delay *= 2;
            }
            catch (Exception ex)
            {
                if (ex is SocketException or IOException)
                {
                    client.TryDispose();

                    _idxServer++;
                    var length = _servers?.Length ?? 1;
                    if (++retry < length)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                }

                throw;
            }
            finally
            {
                pool.Return(client);
                Counter?.StopCount(counter);
            }
        } while (true);
    }

    /// <summary>异步执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="key">命令键</param>
    /// <param name="func">执行函数</param>
    /// <param name="write">是否写操作</param>
    /// <returns>执行结果</returns>
    public virtual async Task<TResult> ExecuteAsync<TResult>(String? key, Func<RedisClient, Task<TResult>> func, Boolean write = false)
    {
        if (write || FullPipeline)
        {
            var redisClient = _client.Value;
            if (redisClient == null && AutoPipeline > 0) redisClient = StartPipeline();
            if (redisClient != null)
            {
                var result = await func(redisClient).ConfigureAwait(false);
                if (AutoPipeline > 0 && redisClient.PipelineCommands >= AutoPipeline)
                {
                    StopPipeline(true);
                    StartPipeline();
                }

                return result;
            }
        }

        if (!write) StopPipeline(true);

        var counter = Counter?.StartCount();
        var retry = 0;
        var delay = 100;
        do
        {
            var client = Pool.Get();
            try
            {
                client.Reset();
                return await func(client).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                if (retry++ >= Retry) throw;

                client.TryDispose();
                await Task.Delay(delay).ConfigureAwait(false);
                delay *= 2;
            }
            catch (Exception ex)
            {
                if (ex is SocketException or IOException)
                {
                    client.TryDispose();
                    _idxServer++;
                    var length = _servers?.Length ?? 1;
                    if (++retry < length)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                        continue;
                    }
                }

                throw;
            }
            finally
            {
                Pool.Return(client);
                Counter?.StopCount(counter);
            }
        } while (true);
    }

    /// <summary>开始管道模式</summary>
    /// <returns>客户端</returns>
    public virtual RedisClient StartPipeline()
    {
        var redisClient = _client.Value;
        if (redisClient == null)
        {
            redisClient = Pool.Get();
            redisClient.Reset();
            redisClient.StartPipeline();
            _client.Value = redisClient;
        }

        return redisClient;
    }

    /// <summary>结束管道模式</summary>
    /// <param name="requireResult">是否要求结果</param>
    /// <returns>结果数组</returns>
    public virtual Object[]? StopPipeline(Boolean requireResult = true)
    {
        var redisClient = _client.Value;
        if (redisClient == null) return null;

        _client.Value = null;
        var counter = Counter?.StartCount();
        try
        {
            return redisClient.StopPipeline(requireResult);
        }
        finally
        {
            if (!requireResult) Thread.Sleep(10);

            redisClient.Reset();
            Pool.Return(redisClient);
            Counter?.StopCount(counter);
        }
    }

    /// <summary>提交变更</summary>
    /// <returns>影响数量</returns>
    public override Int32 Commit()
    {
        var result = StopPipeline(true);
        return result?.Length ?? 0;
    }

    /// <summary>为同一服务器创建不同 Db 的子库</summary>
    /// <param name="db">数据库编号</param>
    /// <returns>子库实例</returns>
    public virtual Redis CreateSub(Int32 db)
    {
        var redis = CreateInstance();
        redis.Server = Server;
        redis.Db = db;
        redis.UserName = UserName;
        redis.Password = Password;
        redis.Encoder = Encoder;
        redis.Timeout = Timeout;
        redis.Retry = Retry;
        redis.Tracer = Tracer;
        redis.Log = Log;
        redis.ThrowOnFailure = ThrowOnFailure;
        redis.MaxMessageSize = MaxMessageSize;
        redis.Expire = Expire;
        redis.FullPipeline = FullPipeline;
        redis.AutoPipeline = AutoPipeline;
        return redis;
    }

    /// <summary>缓存个数</summary>
    public override Int32 Count => Execute(null, redisClient => redisClient.Execute<Int32>("DBSIZE"));

    /// <summary>获取所有键。数量过大时应改用 Search</summary>
    public override ICollection<String> Keys
    {
        get
        {
            if (Count > 10_000) throw new InvalidOperationException("数量过大时，禁止获取所有键，请使用 Search");
            return Execute(null, redisClient => redisClient.Execute<String[]>("KEYS", "*") ?? []);
        }
    }

    /// <summary>获取信息</summary>
    /// <param name="all">是否获取全部信息</param>
    /// <returns>信息字典</returns>
    public virtual IDictionary<String, String> GetInfo(Boolean all = false)
    {
        var result = all
            ? Execute(null, redisClient => redisClient.Execute("INFO", "all") as IPacket)
            : Execute(null, redisClient => redisClient.Execute("INFO") as IPacket);

        if (result == null || result.Total == 0) return new Dictionary<String, String>();
        return result.ToStr().SplitAsDictionary(":", "\r\n");
    }

    /// <summary>设置缓存项</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="expire">过期秒数</param>
    /// <returns>是否成功</returns>
    public override Boolean Set<T>(String key, T value, Int32 expire = -1)
    {
        if (expire < 0) expire = Expire;

        String? result;
        if (expire <= 0)
            result = Execute(key, redisClient => redisClient.Execute<String>("SET", key, value), true);
        else
            result = Execute(key, redisClient => redisClient.Execute<String>("SETEX", key, expire, value), true);

        if (result == "OK") return true;
        if (result.IsNullOrEmpty()) return false;

        using var span = Tracer?.NewSpan($"redis:{Name}:ErrorSet", new { key, value });
        if (ThrowOnFailure) throw new XException("Redis.Set({0},{1})失败。{2}", key, value, result);

        return false;
    }

    /// <summary>获取缓存项</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>值</returns>
    public override T Get<T>(String key) => Execute(key, redisClient => redisClient.Execute<T>("GET", key));

    /// <summary>移除缓存项</summary>
    /// <param name="key">键</param>
    /// <returns>移除数量</returns>
    public override Int32 Remove(String key) => Remove([key]);

    /// <summary>批量移除缓存项</summary>
    /// <param name="keys">键集合</param>
    /// <returns>移除数量</returns>
    public override Int32 Remove(params String[] keys) => Execute(keys.FirstOrDefault(), redisClient => redisClient.Execute<Int32>("DEL", keys), true);

    /// <summary>清空所有缓存项</summary>
    public override void Clear() => Execute<String?>(null, redisClient => redisClient.Execute<String>("FLUSHDB"), true);

    /// <summary>是否存在</summary>
    /// <param name="key">键</param>
    /// <returns>是否存在</returns>
    public override Boolean ContainsKey(String key) => Execute(key, redisClient => redisClient.Execute<Int32>("EXISTS", key) > 0);

    /// <summary>设置缓存项有效期</summary>
    /// <param name="key">键</param>
    /// <param name="expire">过期时间</param>
    /// <returns>是否成功</returns>
    public override Boolean SetExpire(String key, TimeSpan expire) => Execute(key, redisClient => redisClient.Execute<Int32>("EXPIRE", key, (Int32)expire.TotalSeconds) > 0, true);

    /// <summary>获取缓存项有效期</summary>
    /// <param name="key">键</param>
    /// <returns>剩余时间</returns>
    public override TimeSpan GetExpire(String key) => TimeSpan.FromSeconds(Execute(key, redisClient => redisClient.Execute<Int32>("TTL", key)));

    /// <summary>批量设置缓存项</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="values">键值集合</param>
    /// <param name="expire">过期秒数</param>
    public override void SetAll<T>(IDictionary<String, T> values, Int32 expire = -1)
    {
        if (values == null || values.Count == 0) return;
        if (expire < 0) expire = Expire;

        if (values.Count <= 2)
        {
            foreach (var item in values)
            {
                Set(item.Key, item.Value, expire);
            }

            return;
        }

        Execute(values.First().Key, redisClient => redisClient.SetAll(values), true);
        if (expire > 0)
        {
            var timeSpan = TimeSpan.FromSeconds(expire);
            StartPipeline();
            try
            {
                foreach (var item in values)
                {
                    SetExpire(item.Key, timeSpan);
                }
            }
            finally
            {
                StopPipeline(true);
            }
        }
    }

    /// <summary>获取列表</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>列表</returns>
    public override IList<T> GetList<T>(String key) => new RedisList<T>(this, key);

    /// <summary>获取字典</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>字典</returns>
    public override IDictionary<String, T> GetDictionary<T>(String key) => new RedisHash<String, T>(this, key);

    /// <summary>获取队列</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>队列</returns>
    public override IProducerConsumer<T> GetQueue<T>(String key) => new RedisQueue<T>(this, key);

    /// <summary>获取栈</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>栈</returns>
    public override IProducerConsumer<T> GetStack<T>(String key) => new RedisStack<T>(this, key);

    /// <summary>获取集合</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>集合</returns>
    public override ICollection<T> GetSet<T>(String key) => new RedisSet<T>(this, key);

    /// <summary>搜索键</summary>
    /// <param name="pattern">匹配模式</param>
    /// <param name="offset">游标偏移</param>
    /// <param name="count">返回数量，-1 表示尽量取完</param>
    /// <returns>键集合</returns>
    public override IEnumerable<String> Search(String pattern, Int32 offset = 0, Int32 count = -1)
    {
        var cursor = Math.Max(0, offset);
        var remain = count < 0 ? Int32.MaxValue : count;
        var batch = count > 0 && count < 1000 ? count : 1000;
        if (batch <= 0) batch = 1000;

        do
        {
            var result = Execute<Object[]?>(null, redisClient => redisClient.Execute("SCAN", cursor, "MATCH", pattern + String.Empty, "COUNT", batch) as Object[]);
            if (result == null || result.Length != 2) yield break;

            cursor = 0;
            if (result[0] is IPacket packet)
                cursor = packet.ToStr().ToInt();
            else if (result[0] != null)
                cursor = result[0].ToString().ToInt();

            if (result[1] is Object[] items)
            {
                foreach (var item in items)
                {
                    if (remain-- == 0) yield break;

                    if (item is IPacket itemPacket)
                        yield return itemPacket.ToStr();
                    else if (item != null)
                        yield return item.ToString() ?? String.Empty;
                }
            }
        } while (cursor != 0 && remain > 0);
    }

    /// <summary>添加，已存在时不更新</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <param name="expire">过期秒数</param>
    /// <returns>是否成功</returns>
    public override Boolean Add<T>(String key, T value, Int32 expire = -1)
    {
        if (expire < 0) expire = Expire;
        if (expire <= 0) return Execute(key, redisClient => redisClient.Execute<Int32>("SETNX", key, value), true) > 0;

        var info = Info;
        if (info.TryGetValue("redis_version", out var version) && IsAtLeastVersion(version, "2.6.12"))
        {
            var result = Execute(key, redisClient => redisClient.Execute<String>("SET", key, value, "EX", expire, "NX"), true);
            if (result.IsNullOrEmpty()) return false;
            if (result == "OK") return true;

            using var span = Tracer?.NewSpan($"redis:{Name}:ErrorAdd", new { key, value });
            if (ThrowOnFailure) throw new XException("Redis.Add({0},{1})失败。{2}", key, value, result);

            return false;
        }

        var count = Execute(key, redisClient => redisClient.Execute<Int32>("SETNX", key, value), true);
        if (count > 0) SetExpire(key, TimeSpan.FromSeconds(expire));
        return count > 0;
    }

    /// <summary>设置新值并获取旧值</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">新值</param>
    /// <returns>旧值</returns>
    public override T Replace<T>(String key, T value) => Execute(key, redisClient => redisClient.Execute<T>("GETSET", key, value), true);

    /// <summary>尝试获取指定键</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">输出值</param>
    /// <returns>是否包含值</returns>
    public override Boolean TryGetValue<T>(String key, [System.Diagnostics.CodeAnalysis.MaybeNull] out T value)
    {
        T current = default!;
        var result = Execute(key, redisClient =>
        {
            var success = redisClient.TryExecute("GET", [key], out T target);
            current = target;
            return success;
        });

        value = current;
        return result;
    }

    /// <summary>累加</summary>
    /// <param name="key">键</param>
    /// <param name="value">变化量</param>
    /// <returns>新值</returns>
    public override Int64 Increment(String key, Int64 value) => value == 1
        ? Execute(key, redisClient => redisClient.Execute<Int64>("INCR", key), true)
        : Execute(key, redisClient => redisClient.Execute<Int64>("INCRBY", key, value), true);

    /// <summary>浮点累加</summary>
    /// <param name="key">键</param>
    /// <param name="value">变化量</param>
    /// <returns>新值</returns>
    public override Double Increment(String key, Double value) => Execute(key, redisClient => redisClient.Execute<Double>("INCRBYFLOAT", key, value), true);

    /// <summary>递减</summary>
    /// <param name="key">键</param>
    /// <param name="value">变化量</param>
    /// <returns>新值</returns>
    public override Int64 Decrement(String key, Int64 value) => value == 1
        ? Execute(key, redisClient => redisClient.Execute<Int64>("DECR", key), true)
        : Execute(key, redisClient => redisClient.Execute<Int64>("DECRBY", key, value.ToString()), true);

    /// <summary>浮点递减</summary>
    /// <param name="key">键</param>
    /// <param name="value">变化量</param>
    /// <returns>新值</returns>
    public override Double Decrement(String key, Double value) => Increment(key, -value);

    /// <summary>创建 Redis 实例</summary>
    /// <returns>实例</returns>
    protected virtual Redis CreateInstance() => new();

    /// <summary>创建连接客户端</summary>
    /// <returns>客户端</returns>
    protected virtual RedisClient OnCreate()
    {
        var server = Server?.Trim();
        if (server.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Server));

        var servers = _servers;
        if (servers == null)
        {
            var segments = server.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var uris = new NetUri[segments.Length];
            for (var i = 0; i < segments.Length; i++)
            {
                var item = segments[i];
                if (!item.Contains("://")) item = "tcp://" + item;

                var uri = new NetUri(item);
                if (uri.Port == 0) uri.Port = 6379;
                uris[i] = uri;
            }

            servers = _servers = uris;
        }

        var index = _idxServer;
        if (index > 0)
        {
            var now = DateTime.Now;
            if (_nextTrace.Year < 2000) _nextTrace = now.AddSeconds(300);
            if (now > _nextTrace)
            {
                _nextTrace = DateTime.MinValue;
                index = _idxServer = 0;
            }
        }

        if (index != _idxLast)
        {
            XTrace.WriteLine("Redis使用 {0}", servers[index % servers.Length]);
            _idxLast = index;
        }

        var serverUri = servers[index % servers.Length];
        if (Name.IsNullOrEmpty() || Name.EqualIgnoreCase("Redis", "FullRedis")) Name = serverUri.Host ?? serverUri.Address.ToString();

        return new RedisClient(this, serverUri) { Log = Log };
    }

    private static Boolean IsAtLeastVersion(String? version, String minimumVersion)
    {
        if (version.IsNullOrEmpty()) return false;
        if (!Version.TryParse(version, out var left)) return false;
        if (!Version.TryParse(minimumVersion, out var right)) return false;
        return left >= right;
    }

    private sealed class MyPool : ObjectPool<RedisClient>
    {
        public Redis Instance { get; set; } = null!;

        protected override RedisClient OnCreate() => Instance.OnCreate();

        protected override Boolean OnGet(RedisClient value)
        {
            if (value == null) return false;

            value.Reset();
            return base.OnGet(value);
        }
    }
}

#nullable restore