namespace Pek.Collections;

/// <summary>字典数据源接口</summary>
public interface IDictionarySource
{
    /// <summary>把对象转为名值字典，便于序列化传输</summary>
    /// <returns>名值字典</returns>
    IDictionary<String, Object?> ToDictionary();
}