namespace Pek.Collections;

/// <summary>可空字典。获取数据时如果指定键不存在可返回空而不是抛出异常</summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class NullableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDictionary<TKey, TValue> where TKey : notnull
{
    /// <summary>实例化一个可空字典</summary>
    public NullableDictionary() { }

    /// <summary>指定比较器实例化一个可空字典</summary>
    /// <param name="comparer">比较器</param>
    public NullableDictionary(IEqualityComparer<TKey> comparer) : base(comparer) { }

    /// <summary>实例化一个可空字典</summary>
    /// <param name="dic">源字典</param>
    public NullableDictionary(IDictionary<TKey, TValue> dic) : base(dic) { }

    /// <summary>实例化一个可空字典</summary>
    /// <param name="dic">源字典</param>
    /// <param name="comparer">比较器</param>
    public NullableDictionary(IDictionary<TKey, TValue> dic, IEqualityComparer<TKey> comparer) : base(dic, comparer) { }

    /// <summary>获取或设置数据</summary>
    /// <param name="item">键</param>
    /// <returns>键对应的值或默认值</returns>
    public new TValue this[TKey item]
    {
        get
        {
            if (TryGetValue(item, out var value)) return value;

            return default!;
        }
        set => base[item] = value;
    }
}