using System.Diagnostics.CodeAnalysis;

namespace Pek.Models;

/// <summary>分页列表结果接口</summary>
/// <typeparam name="T">数据类型</typeparam>
public interface IPagedListResult<out T>
{
    /// <summary>数据</summary>
    IReadOnlyList<T> Data { get; }

    /// <summary>数量</summary>
    Int32 Count { get; }

    /// <summary>页码</summary>
    Int32 PageNumber { get; }

    /// <summary>每页大小</summary>
    Int32 PageSize { get; }

    /// <summary>总数据量</summary>
    Int32 TotalCount { get; set; }

    /// <summary>总页数</summary>
    Int32 PageCount { get; }
}

/// <summary>分页Model</summary>
/// <typeparam name="T">类型</typeparam>
[Serializable]
public class PagedListResult<T> : IPagedListResult<T>
{
    /// <summary>空实例</summary>
    public static readonly IPagedListResult<T> Empty = new PagedListResult<T>();

    private IReadOnlyList<T> _data = [];

    /// <summary>数据</summary>
    [NotNull]
    public IReadOnlyList<T> Data
    {
        get => _data;
        set
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (value != null)
            {
                _data = value;
            }
        }
    }

    private Int32 _pageNumber = 1;

    /// <summary>页码</summary>
    public Int32 PageNumber
    {
        get => _pageNumber;
        set
        {
            if (value > 0)
            {
                _pageNumber = value;
            }
        }
    }

    private Int32 _pageSize = 10;

    /// <summary>每页大小</summary>
    public Int32 PageSize
    {
        get => _pageSize;
        set
        {
            if (value > 0)
            {
                _pageSize = value;
            }
        }
    }

    private Int32 _totalCount;

    /// <summary>总数据量</summary>
    public Int32 TotalCount
    {
        get => _totalCount;
        set
        {
            if (value > 0)
            {
                _totalCount = value;
            }
        }
    }

    /// <summary>总页数</summary>
    public Int32 PageCount => (_totalCount + _pageSize - 1) / _pageSize;

    /// <summary>索引器</summary>
    /// <param name="index">索引</param>
    public T this[Int32 index] => Data[index];

    /// <summary>数据数量</summary>
    public Int32 Count => Data.Count;
}
