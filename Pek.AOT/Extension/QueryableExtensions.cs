using System.Linq.Expressions;

namespace Pek;

/// <summary>
/// <see cref="IQueryable{T}"/> 扩展
/// </summary>
/// <remarks>AOT 兼容说明：上游 Pek.Common 使用 Pek.Helpers.Check.NotNull，AOT 版本替换为内联 null 检查</remarks>
public static class QueryableExtensions
{
    #region WhereIf(是否执行指定条件的查询)

    /// <summary>
    /// 是否执行指定条件的查询，根据第三方条件是否为真来决定
    /// </summary>
    /// <typeparam name="T">动态类型</typeparam>
    /// <param name="source">要查询的源</param>
    /// <param name="predicate">查询条件</param>
    /// <param name="condition">第三方条件</param>
    /// <returns>查询结果</returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, Expression<Func<T, Boolean>> predicate,
        Boolean condition)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        return condition ? source.Where(predicate) : source;
    }

    #endregion

    #region PageBy(分页)

    /// <summary>
    /// 分页
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="skipCount">跳过的行数</param>
    /// <param name="pageSize">每页记录数</param>
    /// <returns></returns>
    public static IQueryable<T> PageBy<T>(this IQueryable<T> queryable, Int32 skipCount, Int32 pageSize)
    {
        if (queryable == null) throw new ArgumentNullException(nameof(queryable));

        return queryable.Skip(skipCount).Take(pageSize);
    }

    /// <summary>
    /// 分页
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <typeparam name="TQueryable">查询源类型</typeparam>
    /// <param name="queryable">数据源</param>
    /// <param name="skipCount">跳过的行数</param>
    /// <param name="pageSize">每页记录数</param>
    /// <returns></returns>
    public static TQueryable PageBy<T, TQueryable>(this IQueryable<T> queryable, Int32 skipCount, Int32 pageSize)
        where TQueryable : IQueryable
    {
        if (queryable == null) throw new ArgumentNullException(nameof(queryable));

        return (TQueryable)queryable.Skip(skipCount).Take(pageSize);
    }

    #endregion
}
