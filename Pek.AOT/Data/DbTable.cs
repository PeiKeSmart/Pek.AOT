// AOT: stub - full DbTable implementation depends on Serialization/Binary which has not been migrated yet.
// This stub provides the minimal API surface needed by DbRow and other consumers.
// Full implementation from DH.NCore/DH.NCore/Data/DbTable.cs will be completed after Serialization batch.
using Pek.Extension;

namespace Pek.Data;

/// <summary>鏁版嵁琛紙瀛樻牴锛屽緟 Serialization 杩佺Щ瀹屾垚鍚庤ˉ榻愬畬鏁村疄鐜帮級</summary>
public class DbTable
{
    /// <summary>鏁版嵁鍒?/summary>
    public String[] Columns { get; set; } = [];

    /// <summary>鏁版嵁鍒楃被鍨?/summary>
    public Type[] Types { get; set; } = [];

    /// <summary>鏁版嵁琛?/summary>
    public IList<Object?[]> Rows { get; set; } = [];

    /// <summary>鎬昏鏁?/summary>
    public Int32 Total { get; set; }

    /// <summary>鑾峰彇鎸囧畾鍒楀悕鐨勭储寮?/summary>
    /// <param name="name">鍒楀悕</param>
    /// <returns>鍒楃储寮曪紝鏈壘鍒拌繑鍥?-1</returns>
    public Int32 GetColumn(String name)
    {
        var cs = Columns;
        for (var i = 0; i < cs.Length; i++)
        {
            if (cs[i].EqualIgnoreCase(name)) return i;
        }
        return -1;
    }

    /// <summary>璇诲彇鎸囧畾琛岀殑瀛楁鍊?/summary>
    /// <typeparam name="T">鍊肩被鍨?/typeparam>
    /// <param name="row">琛岀储寮?/param>
    /// <param name="name">鍒楀悕</param>
    /// <returns>瀛楁鍊?/returns>
    public T? Get<T>(Int32 row, String name)
    {
        var col = GetColumn(name);
        if (col < 0) return default;

        var rows = Rows;
        if (rows == null || row >= rows.Count) return default;

        var obj = rows[row][col];
        if (obj == null || obj == DBNull.Value) return default;

        return (T)Convert.ChangeType(obj, typeof(T));
    }
}
