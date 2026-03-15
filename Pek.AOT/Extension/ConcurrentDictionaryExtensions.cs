using System.Collections.Concurrent;

namespace System;

/// <summary>并发字典扩展</summary>
public static class ConcurrentDictionaryExtensions
{
    /// <summary>从并发字典中删除</summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="dict">并发字典</param>
    /// <param name="key">键</param>
    /// <returns>是否删除成功</returns>
    public static Boolean Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key) where TKey : notnull => dict.TryRemove(key, out _);
}