using System.Text;

namespace Pek;

/// <summary>
/// 可枚举类型<see cref="IEnumerable{T}"/> 扩展
/// </summary>
public static class EnumerableExtensions
{
    #region ForEach(对指定集合中的每个元素执行指定操作)

    /// <summary>
    /// 对指定集合中的每个元素执行指定操作
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="enumerable">值</param>
    /// <param name="action">操作</param>
    /// <exception cref="ArgumentNullException">源集合对象为空、操作表达式为空</exception>
    public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        if (enumerable == null)
            throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
        if (action == null)
            throw new ArgumentNullException(nameof(action), @"操作表达式不可为空！");
        foreach (var item in enumerable)
            action(item);
    }

    /// <summary>
    /// 对指定集合中的每个元素执行指定操作（异步）
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="enumerable">值</param>
    /// <param name="action">操作</param>
    /// <exception cref="ArgumentNullException">源集合对象为空、操作表达式为空</exception>
    public static Task ForEachAsync<T>(this IEnumerable<T> enumerable, Func<T, Task> action)
    {
        if (enumerable == null)
            throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
        if (action == null)
            throw new ArgumentNullException(nameof(action), @"操作表达式不可为空！");
        return Task.WhenAll(from item in enumerable select Task.Run(() => action(item)));
    }

    #endregion

    #region EqualsTo(判断两个集合中的元素是否相等)

    /// <summary>
    /// 判断两个集合中的元素是否相等（仅浅度对比，AOT 兼容版本不包含 JSON 深度序列化对比）
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="sourceList">源集合</param>
    /// <param name="targetList">目标集合</param>
    /// <exception cref="ArgumentNullException">源集合对象为空、目标集合对象为空</exception>
    /// <remarks>
    /// AOT 兼容说明：上游 Pek.Common 版本在此方法中还包含通过 ToJson() 的深度对比逻辑，
    /// 但 Pek.AOT 不包含 NewLife.Serialization 的 JSON 序列化支持，故此版本仅保留计数对比和 Except 浅度对比。
    /// 如需深度对比，请在调用方自行序列化后比较。
    /// </remarks>
    public static Boolean EqualsTo<T>(this IEnumerable<T> sourceList, IEnumerable<T> targetList)
    {
        if (sourceList == null)
            throw new ArgumentNullException(nameof(sourceList), $@"源{typeof(T).Name}集合对象不可为空！");
        if (targetList == null)
            throw new ArgumentNullException(nameof(targetList), $@"目标{typeof(T).Name}集合对象不可为空！");
        // 长度对比
        if (sourceList.Count() != targetList.Count())
            return false;
        if (!sourceList.Any() && !targetList.Any())
            return true;
        // 浅度对比
        if (!sourceList.Except(targetList).Any() && !targetList.Except(sourceList).Any())
            return true;
        // 深度对比已移除（AOT 兼容：NewLife.Serialization.ToJson() 不可用）
        return false;
    }

    #endregion

    #region DistinctBy(根据指定条件返回集合中不重复的元素)

    /// <summary>
    /// 根据指定条件返回集合中不重复的元素
    /// </summary>
    /// <typeparam name="T">动态类型</typeparam>
    /// <typeparam name="TKey">动态筛选条件类型</typeparam>
    /// <param name="enumerable">源集合</param>
    /// <param name="keySelector">字段选择委托</param>
    /// <exception cref="ArgumentNullException">源集合对象为空、参照字段表达式为空</exception>
    public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> enumerable, Func<T, TKey> keySelector)
    {
        if (enumerable == null)
            throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
        if (keySelector == null)
            throw new ArgumentNullException(nameof(keySelector), @"参照字段表达式不可为空");
        enumerable = enumerable as IList<T> ?? enumerable.ToList();
        var seenKeys = new HashSet<TKey>();
        return enumerable.Where(item => seenKeys.Add(keySelector(item)));
    }

    #endregion

    #region ExpandAndToString(展开集合并转换为字符串)

    /// <summary>
    /// 将集合展开并分别转换成字符串，再以指定的分隔符衔接，拼成一个字符串返回。默认分隔符为逗号
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="collection">要处理的集合</param>
    /// <param name="separator">分隔符，默认为逗号</param>
    /// <param name="wrapItem">项目包裹符</param>
    public static String ExpandAndToString<T>(this IEnumerable<T> collection, String separator = ",",
        String wrapItem = "") =>
        collection.ExpandAndToString(t => t.ToString(), separator, wrapItem);

    /// <summary>
    /// 将集合展开并转为字符串，循环集合每一项，调用委托生成字符串，返回合并后的字符串。默认分隔符为逗号
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="collection">要处理的集合</param>
    /// <param name="itemFormatFunc">单个集合项的转换委托</param>
    /// <param name="separator">分隔符，默认为逗号</param>
    /// <param name="wrapItem">项目包裹符</param>
    public static String ExpandAndToString<T>(this IEnumerable<T> collection, Func<T, String> itemFormatFunc,
        String separator = ",", String wrapItem = "")
    {
        collection = collection as IList<T> ?? collection.ToList();

        if (itemFormatFunc == null) throw new ArgumentNullException(nameof(itemFormatFunc));

        if (!collection.Any())
            return null;
        var sb = new StringBuilder();
        var i = 0;
        var count = collection.Count();
        foreach (var t in collection)
        {
            if (!String.IsNullOrWhiteSpace(wrapItem))
            {
                sb.Append(i == count - 1
                    ? $"{wrapItem}{itemFormatFunc(t)}{wrapItem}"
                    : $"{wrapItem}{itemFormatFunc(t)}{wrapItem}{separator}");
            }
            else
            {
                if (i == count - 1)
                    sb.Append(itemFormatFunc(t));
                else
                    sb.Append(itemFormatFunc(t) + separator);
            }
            i++;
        }
        return sb.ToString();
    }

    #endregion

    // ToDataTable<T> 方法未迁移：
    // 上游 Pek.Common 版本使用 System.Data.DataTable 及 Type.GetProperties()/PropertyInfo.GetValue()
    // 等反射 API，这些在 NativeAOT 下不受支持且 DataTable 本身在 AOT 场景不适用。
    // 如需 DataTable 转换，请在非 AOT 项目中使用 Pek.Common 的原始版本。

    #region WhereIf(是否执行条件查询)

    /// <summary>
    /// 是否执行指定条件的查询，根据第三方条件是否为真来决定
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="enumerable">源集合</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="condition">第三方条件</param>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> enumerable, Func<T, Boolean> predicate, Boolean condition)
    {
        if (enumerable == null)
            throw new ArgumentNullException(nameof(enumerable), $@"源{typeof(T).Name}集合对象不可为空！");
        enumerable = enumerable as IList<T> ?? enumerable.ToList();
        return condition ? enumerable.Where(predicate) : enumerable;
    }

    #endregion
}
