using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic;

/// <summary>扩展 List，支持遍历中修改元素</summary>
public static class ListExtension
{
    /// <summary>线程安全，搜索并返回第一个，支持遍历中修改元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="match">条件</param>
    /// <returns>匹配到的第一个元素；未找到时返回默认值</returns>
    public static T? Find<T>(this IList<T> list, Predicate<T> match)
    {
        if (list is List<T> list2) return list2.Find(match);

        return list.ToArray().FirstOrDefault(e => match(e));
    }

    /// <summary>线程安全，搜索并返回所有匹配项，支持遍历中修改元素</summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="list">实体列表</param>
    /// <param name="match">条件</param>
    /// <returns>匹配项列表</returns>
    public static IList<T> FindAll<T>(this IList<T> list, Predicate<T> match)
    {
        if (list is List<T> list2) return list2.FindAll(match);

        return list.ToArray().Where(e => match(e)).ToList();
    }

    /// <summary>从集合中删除满足给定条件的项</summary>
    /// <typeparam name="T">集合中项目的类型</typeparam>
    /// <param name="source">集合</param>
    /// <param name="predicate">删除项目的条件</param>
    /// <returns>删除的项目列表</returns>
    public static IList<T> RemoveAll<T>([NotNull] this ICollection<T> source, Func<T, Boolean> predicate)
    {
        var items = source.Where(predicate).ToList();

        foreach (var item in items)
        {
            source.Remove(item);
        }

        return items;
    }

    /// <summary>从集合中删除所有指定项</summary>
    /// <typeparam name="T">集合中项目的类型</typeparam>
    /// <param name="source">集合</param>
    /// <param name="items">要删除的项目</param>
    public static void RemoveAll<T>([NotNull] this ICollection<T> source, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            source.Remove(item);
        }
    }
}