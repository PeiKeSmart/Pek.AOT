#nullable enable

using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;

using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.IO;
using Pek.Log;
using Pek.Serialization;

namespace Pek.Caching;

/// <summary>Redis 客户端</summary>
/// <remarks>以极简原则设计，每个客户端不支持并行命令处理。</remarks>
public class RedisClient : DisposeBase, ILogFeature
{
    /// <summary>客户端</summary>
    public TcpClient? Client { get; set; }

    /// <summary>服务器地址</summary>
    public Net.NetUri Server { get; set; }

    /// <summary>宿主</summary>
    public Redis Host { get; set; }

    /// <summary>是否已登录</summary>
    public Boolean Logined { get; private set; }

    /// <summary>登录时间</summary>
    public DateTime LoginTime { get; private set; }

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private IList<Command>? _pipeline;

    /// <summary>实例化</summary>
    /// <param name="redis">宿主</param>
    /// <param name="server">服务器地址</param>
    public RedisClient(Redis redis, Net.NetUri server)
    {
        Host = redis ?? throw new ArgumentNullException(nameof(redis));
        Server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <summary>销毁</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        if (Logined)
        {
            try
            {
                var client = Client;
                if (client?.Connected == true && client.GetStream() != null) Quit();
            }
            catch { }
        }

        Client.TryDispose();
        Client = null;
    }

    /// <summary>已重载</summary>
    /// <returns>文本描述</returns>
    public override String ToString() => Server + String.Empty;

    /// <summary>管道命令个数</summary>
    public Int32 PipelineCommands => _pipeline?.Count ?? 0;

    /// <summary>执行命令。返回字符串、Packet、Packet 数组</summary>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    /// <returns>执行结果</returns>
    public virtual Object? Execute(String cmd, params Object?[] args)
    {
        using var span = cmd.IsNullOrEmpty() ? null : Host.Tracer?.NewSpan($"redis:{Host.Name}:{cmd}", args);
        return ExecuteCommand(cmd, args?.Select(EncodeArgument).ToArray(), args);
    }

    /// <summary>执行命令。返回基础类型、对象、对象数组</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    /// <returns>执行结果</returns>
    public virtual TResult Execute<TResult>(String cmd, params Object?[] args)
    {
        if (_pipeline != null)
        {
            _pipeline.Add(new Command(cmd, args, typeof(TResult)));
            return default!;
        }

        var result = Execute(cmd, args);
        if (result == null) return default!;
        if (result is TResult typed) return typed;
        if (TryChangeType(result, typeof(TResult), out var target)) return (TResult)target;

        return default!;
    }

    /// <summary>尝试执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    /// <param name="value">输出值</param>
    /// <returns>是否成功</returns>
    public virtual Boolean TryExecute<TResult>(String cmd, Object?[] args, out TResult value)
    {
        var result = Execute(cmd, args);
        if (result is TResult typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        if (result == null) return false;
        if (TryChangeType(result, typeof(TResult), out var target)) value = (TResult)target;
        return true;
    }

    /// <summary>异步执行命令</summary>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    public virtual async Task<Object?> ExecuteAsync(String cmd, Object?[] args, CancellationToken cancellationToken = default)
    {
        using var span = cmd.IsNullOrEmpty() ? null : Host.Tracer?.NewSpan($"redis:{Host.Name}:{cmd}", args);
        return await ExecuteCommandAsync(cmd, args?.Select(EncodeArgument).ToArray(), args, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>异步执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    /// <returns>执行结果</returns>
    public virtual Task<TResult> ExecuteAsync<TResult>(String cmd, params Object?[] args) => ExecuteAsync<TResult>(cmd, args, CancellationToken.None);

    /// <summary>异步执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cmd">命令</param>
    /// <param name="args">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    public virtual async Task<TResult> ExecuteAsync<TResult>(String cmd, Object?[] args, CancellationToken cancellationToken)
    {
        if (_pipeline != null)
        {
            _pipeline.Add(new Command(cmd, args, typeof(TResult)));
            return default!;
        }

        var result = await ExecuteAsync(cmd, args, cancellationToken).ConfigureAwait(false);
        if (result == null) return default!;
        if (result is TResult typed) return typed;
        if (TryChangeType(result, typeof(TResult), out var target)) return (TResult)target;

        return default!;
    }

    /// <summary>读取更多响应。用于 PubSub 等场景</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <returns>响应结果</returns>
    public virtual TResult ReadMore<TResult>()
    {
        var stream = GetStream(false);
        if (stream == null) return default!;

        var result = GetResponse(stream, 1).FirstOrDefault();
        if (result == null) return default!;
        if (result is TResult typed) return typed;
        if (TryChangeType(result, typeof(TResult), out var target)) return (TResult)target;

        return default!;
    }

    /// <summary>异步读取更多响应</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应结果</returns>
    public virtual async Task<TResult> ReadMoreAsync<TResult>(CancellationToken cancellationToken)
    {
        var stream = await GetStreamAsync(false).ConfigureAwait(false);
        if (stream == null) return default!;

        var result = (await GetResponseAsync(stream, 1, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        if (result == null) return default!;
        if (result is TResult typed) return typed;
        if (TryChangeType(result, typeof(TResult), out var target)) return (TResult)target;

        return default!;
    }

    /// <summary>开始管道模式</summary>
    public virtual void StartPipeline()
    {
        _pipeline ??= new List<Command>();
    }

    /// <summary>结束管道模式</summary>
    /// <param name="requireResult">是否要求结果</param>
    /// <returns>结果数组</returns>
    public virtual Object[]? StopPipeline(Boolean requireResult)
    {
        var pipeline = _pipeline;
        if (pipeline == null) return null;

        _pipeline = null;

        var stream = GetStream(true);
        if (stream == null) return null;

        using var span = Host.Tracer?.NewSpan($"redis:{Host.Name}:Pipeline", null);
        CheckLogin(null);

        var memoryStream = Pool.MemoryStream.Get();
        var commands = new List<String>(pipeline.Count);
        foreach (var item in pipeline)
        {
            commands.Add(item.Name);
            GetRequest(memoryStream, item.Name, item.Args.Select(EncodeArgument).ToArray(), item.Args);
        }

        if (span != null) span.SetTag(commands);

        if (memoryStream.Length > 0) memoryStream.WriteTo(stream);
        Pool.MemoryStream.Return(memoryStream);

        if (!requireResult) return new Object[pipeline.Count];

        var list = GetResponse(stream, pipeline.Count);
        for (var i = 0; i < list.Count && i < pipeline.Count; i++)
        {
            if (TryChangeType(list[i], pipeline[i].Type, out var target) && target != null) list[i] = target;
        }

        return list.ToArray();
    }

    /// <summary>心跳</summary>
    /// <returns>是否成功</returns>
    public Boolean Ping() => Execute<String>("PING") == "PONG";

    /// <summary>选择数据库</summary>
    /// <param name="db">数据库编号</param>
    /// <returns>是否成功</returns>
    public Boolean Select(Int32 db) => Execute<String>("SELECT", db + String.Empty) == "OK";

    /// <summary>验证密码</summary>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <returns>是否成功</returns>
    public Boolean Auth(String? userName, String? password)
    {
        var result = userName.IsNullOrEmpty()
            ? Execute<String>("AUTH", password ?? String.Empty)
            : Execute<String>("AUTH", userName!, password ?? String.Empty);

        return result == "OK";
    }

    /// <summary>退出</summary>
    /// <returns>是否成功</returns>
    public Boolean Quit() => Execute<String>("QUIT") == "OK";

    /// <summary>批量设置</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="values">键值集合</param>
    /// <returns>是否成功</returns>
    public Boolean SetAll<T>(IDictionary<String, T> values)
    {
        if (values == null || values.Count == 0) throw new ArgumentNullException(nameof(values));

        var parameters = new List<Object>();
        foreach (var item in values)
        {
            parameters.Add(item.Key);
            if (item.Value == null) throw new NullReferenceException();
            parameters.Add(item.Value);
        }

        var result = Execute<String>("MSET", parameters.ToArray());
        if (result != "OK")
        {
            using var span = Host.Tracer?.NewSpan($"redis:{Host.Name}:ErrorSetAll", values);
            if (Host.ThrowOnFailure) throw new XException("Redis.SetAll({0})失败。{1}", values.ToJson(), result);
        }

        return result == "OK";
    }

    /// <summary>批量获取</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="keys">键集合</param>
    /// <returns>结果字典</returns>
    public IDictionary<String, T> GetAll<T>(IEnumerable<String> keys)
    {
        var array = keys?.ToArray() ?? throw new ArgumentNullException(nameof(keys));
        if (array.Length == 0) throw new ArgumentNullException(nameof(keys));

        var dictionary = new Dictionary<String, T>();
        if (Execute("MGET", array) is not Object[] results) return dictionary;

        for (var i = 0; i < array.Length && i < results.Length; i++)
        {
            if (results[i] is IPacket packet && Host.Encoder.Decode(packet, typeof(T)) is T value)
                dictionary[array[i]] = value;
        }

        return dictionary;
    }

    /// <summary>重置。清理历史残留数据</summary>
    public void Reset()
    {
        var stream = GetStream(false);
        if (stream == null) return;

        if (stream is NetworkStream networkStream && networkStream.DataAvailable)
        {
            var buffer = new Byte[1024];
            Int32 count;
            do
            {
                count = stream.Read(buffer, 0, buffer.Length);
            } while (count > 0 && networkStream.DataAvailable);
        }
    }

    /// <summary>尝试转换类型</summary>
    /// <param name="value">原始值</param>
    /// <param name="type">目标类型</param>
    /// <param name="target">转换结果</param>
    /// <returns>是否成功</returns>
    public virtual Boolean TryChangeType(Object value, Type type, out Object target)
    {
        target = null!;

        if (value is String text)
        {
            try
            {
                if (type == typeof(Boolean) && text == "OK")
                    target = true;
                else
                    target = System.Convert.ChangeType(text, Nullable.GetUnderlyingType(type) ?? type)!;
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"不能把字符串[{text}]转为类型[{type.FullName}]", ex);
            }
        }

        if (value is IPacket packet)
        {
            var decoded = Host.Encoder.Decode(packet, type);
            if (decoded == null) return false;
            target = decoded;
            return true;
        }

        if (value is Object[] objects)
        {
            if (type == typeof(Object[]))
            {
                target = value;
                return true;
            }

            if (type == typeof(IPacket[]))
            {
                target = objects.OfType<IPacket>().ToArray();
                return true;
            }

            if (type == typeof(String[]))
            {
                var array = new String?[objects.Length];
                for (var i = 0; i < objects.Length; i++)
                {
                    var item = objects[i];
                    array[i] = item switch
                    {
                        null => null,
                        IPacket packet2 => packet2.ToStr(),
                        _ => item.ToString(),
                    };
                }

                target = array;
                return true;
            }

            return false;
        }

        return false;
    }

    private Stream? GetStream(Boolean create)
    {
        var client = Client;
        NetworkStream? stream = null;
        var active = false;
        try
        {
            stream = client?.GetStream();
            active = stream != null && client != null && client.Connected && stream.CanWrite && stream.CanRead;
        }
        catch { }

        if (!active)
        {
            Logined = false;

            Client = null;
            client.TryDispose();
            if (!create) return null;

            var timeout = Host.Timeout;
            client = new TcpClient
            {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            };

            try
            {
                var asyncResult = client.BeginConnect(Server.Address, Server.Port, null, null);
                if (!asyncResult.AsyncWaitHandle.WaitOne(timeout, true))
                {
                    client.Close();
                    throw new TimeoutException($"连接[{Server}][{timeout}ms]超时！");
                }

                client.EndConnect(asyncResult);
                Client = client;
                stream = client.GetStream();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        return stream;
    }

    private async Task<Stream?> GetStreamAsync(Boolean create)
    {
        var client = Client;
        NetworkStream? stream = null;
        var active = false;
        try
        {
            stream = client?.GetStream();
            active = stream != null && client != null && client.Connected && stream.CanWrite && stream.CanRead;
        }
        catch { }

        if (!active)
        {
            Logined = false;

            Client = null;
            client.TryDispose();
            if (!create) return null;

            var timeout = Host.Timeout;
            client = new TcpClient
            {
                SendTimeout = timeout,
                ReceiveTimeout = timeout
            };

            await client.ConnectAsync(Server.Address, Server.Port).ConfigureAwait(false);
            Client = client;
            stream = client.GetStream();
        }

        return stream;
    }

    private static readonly Byte[] NewLine = [(Byte)'\r', (Byte)'\n'];
    private static readonly ConcurrentDictionary<String, Byte[]> Header0 = [];
    private static readonly ConcurrentDictionary<String, Byte[]> Header1 = [];
    private static readonly ConcurrentDictionary<String, Byte[]> Header2 = [];
    private static readonly ConcurrentDictionary<String, Byte[]> Header3 = [];

    private void CheckLogin(String? cmd)
    {
        if (Logined) return;
        if (!cmd.IsNullOrEmpty() && cmd.EqualIgnoreCase("Auth", "Select")) return;

        if (!Host.Password.IsNullOrEmpty() && !Auth(Host.UserName, Host.Password)) throw new Exception("登录失败！");
        if (Host.Db > 0) Select(Host.Db);

        Logined = true;
        LoginTime = DateTime.Now;
    }

    private void GetRequest(Stream stream, String cmd, IPacket?[]? args, Object?[]? originalArgs)
    {
        var log = ReferenceEquals(Log, Logger.Null) ? null : Pool.StringBuilder.Get();
        log?.Append(cmd);

        if (args == null || args.Length == 0)
        {
            stream.Write(GetHeaderBytes(cmd, 0));
        }
        else
        {
            stream.Write(GetHeaderBytes(cmd, args.Length));
            for (var i = 0; i < args.Length; i++)
            {
                var item = args[i];
                var packet = item == null ? new ArrayPacket([]) : item;
                var size = packet.Total;
                var sizeBytes = size.ToString().GetBytes();

                if (log != null)
                {
                    log.Append(' ');
                    var original = originalArgs != null && i < originalArgs.Length ? originalArgs[i] : null;
                    switch (original)
                    {
                        case null:
                            log.Append("null");
                            break;
                        case DateTime dateTime:
                            log.Append(dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                            break;
                        default:
                            if (Type.GetTypeCode(original.GetType()) == TypeCode.Object)
                                log.AppendFormat("[{0}]{1}", size, packet.ToStr(null, 0, 1024)?.TrimEnd());
                            else
                                log.Append(original);
                            break;
                    }
                }

                stream.WriteByte((Byte)'$');
                stream.Write(sizeBytes);
                stream.Write(NewLine);
                packet.CopyTo(stream);
                stream.Write(NewLine);
            }
        }

        if (log != null)
        {
            this.WriteLog("=> {0}", Pool.Return(log, true));
        }
    }

    private IList<Object> GetResponse(Stream stream, Int32 count)
    {
        var buffered = new BufferedStream(stream);
        var log = ReferenceEquals(Log, Logger.Null) ? null : Pool.StringBuilder.Get();
        var list = new List<Object>();

        for (var i = 0; i < count; i++)
        {
            var data = buffered.ReadByte();
            if (data == -1) break;

            var header = (Char)data;
            log?.Append(header);
            if (header == '$')
            {
                var block = ReadBlock(buffered, log);
                if (block != null) list.Add(block);
            }
            else if (header == '*')
            {
                list.Add(ReadBlocks(buffered, log));
            }
            else
            {
                var line = ReadLine(buffered);
                log?.Append(line);

                if (header is '+' or ':')
                    list.Add(line);
                else if (header == '-')
                    throw new Exception(line);
                else
                {
                    XTrace.WriteLine("无法解析响应[{0:X2}] {1}", (Byte)header, ReadRemainingBytes(buffered).ToHex("-"));
                    throw new InvalidDataException($"无法解析响应 [{header}]");
                }
            }
        }

        if (log != null) this.WriteLog("<= {0}", Pool.Return(log, true));
        return list;
    }

    private async Task<IList<Object>> GetResponseAsync(Stream stream, Int32 count, CancellationToken cancellationToken)
    {
        var list = new List<Object>();
        var log = ReferenceEquals(Log, Logger.Null) ? null : Pool.StringBuilder.Get();

        var first = new Byte[1];
        if (cancellationToken == CancellationToken.None) cancellationToken = new CancellationTokenSource(Host.Timeout).Token;

        var read = await stream.ReadAsync(first.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        if (read <= 0) return list;

        var header = (Char)first[0];
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                var data = stream.ReadByte();
                if (data == -1) break;
                header = (Char)data;
            }

            log?.Append(header);
            if (header == '$')
            {
                var block = ReadBlock(stream, log);
                if (block != null) list.Add(block);
            }
            else if (header == '*')
            {
                list.Add(ReadBlocks(stream, log));
            }
            else
            {
                var line = ReadLine(stream);
                log?.Append(line);

                if (header is '+' or ':')
                    list.Add(line);
                else if (header == '-')
                    throw new Exception(line);
                else
                {
                    XTrace.WriteLine("无法解析响应[{0:X2}] {1}", (Byte)header, ReadRemainingBytes(stream).ToHex("-"));
                    throw new InvalidDataException($"无法解析响应 [{header}]");
                }
            }
        }

        if (log != null) this.WriteLog("<= {0}", Pool.Return(log, true));
        return list;
    }

    private Object? ExecuteCommand(String cmd, IPacket?[]? args, Object?[]? originalArgs)
    {
        var isQuit = cmd == "QUIT";
        var stream = GetStream(!isQuit);
        if (stream == null) return null;

        if (!cmd.IsNullOrEmpty())
        {
            CheckLogin(cmd);

            var memoryStream = Pool.MemoryStream.Get();
            GetRequest(memoryStream, cmd, args, originalArgs);

            if (Host.MaxMessageSize > 0 && memoryStream.Length > Host.MaxMessageSize)
                throw new InvalidOperationException($"命令[{cmd}]的数据包大小[{memoryStream.Length}]超过最大限制[{Host.MaxMessageSize}]，大 key 会拖累整个 Redis 实例。");

            if (memoryStream.Length > 0) memoryStream.WriteTo(stream);
            Pool.MemoryStream.Return(memoryStream);
        }

        var result = GetResponse(stream, 1).FirstOrDefault();
        if (isQuit) Logined = false;
        return result;
    }

    private async Task<Object?> ExecuteCommandAsync(String cmd, IPacket?[]? args, Object?[]? originalArgs, CancellationToken cancellationToken)
    {
        var isQuit = cmd == "QUIT";
        var stream = await GetStreamAsync(!isQuit).ConfigureAwait(false);
        if (stream == null) return null;

        if (!cmd.IsNullOrEmpty())
        {
            CheckLogin(cmd);

            var memoryStream = Pool.MemoryStream.Get();
            GetRequest(memoryStream, cmd, args, originalArgs);
            memoryStream.Position = 0;
            if (memoryStream.Length > 0) await memoryStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            Pool.MemoryStream.Return(memoryStream);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = (await GetResponseAsync(stream, 1, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        if (isQuit) Logined = false;
        return result;
    }

    private static IPacket? ReadBlock(Stream stream, StringBuilder? log) => ReadPacket(stream, log);

    private Object[] ReadBlocks(Stream stream, StringBuilder? log)
    {
        var length = ReadLine(stream).ToInt(-1);
        log?.Append(length);
        if (length < 0) return [];

        var array = new Object[length];
        for (var i = 0; i < length; i++)
        {
            var data = stream.ReadByte();
            if (data == -1) break;

            var header = (Char)data;
            log?.Append(' ');
            log?.Append(header);
            if (header == '$')
            {
                array[i] = ReadPacket(stream, log)!;
            }
            else if (header is '+' or ':')
            {
                array[i] = ReadLine(stream);
                log?.Append(array[i]);
            }
            else if (header == '*')
            {
                array[i] = ReadBlocks(stream, log);
            }
        }

        return array;
    }

    private static IPacket? ReadPacket(Stream stream, StringBuilder? log)
    {
        var length = ReadLine(stream).ToInt(-1);
        log?.Append(length);
        if (length == 0)
        {
            ReadLine(stream);
            return null;
        }

        if (length <= 0) return null;

        var buffer = new Byte[length + 2];
        var position = 0;
        while (position < buffer.Length)
        {
            var count = stream.Read(buffer, position, buffer.Length - position);
            if (count <= 0) break;
            position += count;
        }

        var packet = new ArrayPacket(buffer, 0, Math.Max(0, position - 2));
        log?.AppendFormat(" {0}", packet.ToStr(null, 0, 1024)?.TrimEnd());
        return packet;
    }

    private static String ReadLine(Stream stream)
    {
        var builder = Pool.StringBuilder.Get();
        while (true)
        {
            var data = stream.ReadByte();
            if (data < 0) break;

            if (data == '\r')
            {
                var next = stream.ReadByte();
                if (next < 0) break;
                if (next == '\n') break;

                builder.Append((Char)data);
                builder.Append((Char)next);
            }
            else
            {
                builder.Append((Char)data);
            }
        }

        return Pool.Return(builder, true);
    }

    private static Byte[] GetHeaderBytes(String cmd, Int32 args = 0)
    {
        if (args == 0) return Header0.GetOrAdd(cmd, static key => $"*1\r\n${key.Length}\r\n{key}\r\n".GetBytes());
        if (args == 1) return Header1.GetOrAdd(cmd, static key => $"*2\r\n${key.Length}\r\n{key}\r\n".GetBytes());
        if (args == 2) return Header2.GetOrAdd(cmd, static key => $"*3\r\n${key.Length}\r\n{key}\r\n".GetBytes());
        if (args == 3) return Header3.GetOrAdd(cmd, static key => $"*4\r\n${key.Length}\r\n{key}\r\n".GetBytes());

        return $"*{1 + args}\r\n${cmd.Length}\r\n{cmd}\r\n".GetBytes();
    }

    private IPacket EncodeArgument(Object? value) => value == null
        ? new ArrayPacket([])
        : Host.Encoder.Encode(value) ?? new ArrayPacket([]);

    private static Byte[] ReadRemainingBytes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        var buffer = new Byte[1024];
        Int32 count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            memoryStream.Write(buffer, 0, count);
            if (count < buffer.Length) break;
        }

        return memoryStream.ToArray();
    }

    private sealed class Command
    {
        public String Name { get; }
        public Object?[] Args { get; }
        public Type Type { get; }

        public Command(String name, Object?[] args, Type type)
        {
            Name = name;
            Args = args;
            Type = type;
        }
    }
}

#nullable restore