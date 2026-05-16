#nullable enable

using System.Globalization;

using Pek.Data;
using Pek.Extension;

namespace Pek.Caching;

/// <summary>Redis 容器基础类</summary>
public abstract class RedisBase
{
    /// <summary>Redis 实例</summary>
    public Redis Redis { get; }

    /// <summary>键</summary>
    public String Key { get; }

    /// <summary>实例化</summary>
    /// <param name="redis">Redis 实例</param>
    /// <param name="key">键</param>
    protected RedisBase(Redis redis, String key)
    {
        Redis = redis ?? throw new ArgumentNullException(nameof(redis));
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="func">执行函数</param>
    /// <param name="write">是否写操作</param>
    /// <returns>结果</returns>
    protected virtual TResult Execute<TResult>(Func<RedisClient, TResult> func, Boolean write = false) => Redis.Execute(Key, func, write);

    /// <summary>异步执行命令</summary>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <param name="func">执行函数</param>
    /// <param name="write">是否写操作</param>
    /// <returns>结果</returns>
    protected virtual Task<TResult> ExecuteAsync<TResult>(Func<RedisClient, Task<TResult>> func, Boolean write = false) => Redis.ExecuteAsync(Key, func, write);

    /// <summary>解码值</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="value">原始值</param>
    /// <returns>目标值</returns>
    protected T? Decode<T>(Object? value)
    {
        if (value == null) return default;
        if (value is T target) return target;
        if (value is IPacket packet) return Redis.Encoder.Decode<T>(packet);

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType == typeof(String)) return (T)(Object)(value.ToString() ?? String.Empty);

        if (value is String text)
        {
            if (targetType.IsEnum) return (T)Enum.Parse(targetType, text, true);

            var textPacket = new ArrayPacket(text.GetBytes());
            return Redis.Encoder.Decode<T>(textPacket);
        }

        if (targetType.IsEnum)
        {
            var underlying = Enum.GetUnderlyingType(targetType);
            var number = System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture)!;
            return (T)Enum.ToObject(targetType, number);
        }

        return (T)System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)!;
    }

    /// <summary>解码数组</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="values">原始值数组</param>
    /// <returns>目标数组</returns>
    protected T[] DecodeArray<T>(Object[]? values)
    {
        if (values == null || values.Length == 0) return [];

        var list = new List<T>(values.Length);
        foreach (var item in values)
        {
            list.Add(Decode<T>(item)!);
        }

        return [.. list];
    }
}

#nullable restore