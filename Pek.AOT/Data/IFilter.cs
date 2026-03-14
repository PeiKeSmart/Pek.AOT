namespace Pek.Data;

/// <summary>数据过滤器</summary>
public interface IFilter
{
    /// <summary>下一个过滤器</summary>
    IFilter? Next { get; }

    /// <summary>对封包执行过滤器</summary>
    /// <param name="context">过滤上下文</param>
    void Execute(FilterContext context);
}

/// <summary>过滤器上下文</summary>
public class FilterContext
{
    /// <summary>封包</summary>
    public virtual IPacket? Packet { get; set; }
}

/// <summary>过滤器助手</summary>
public static class FilterHelper
{
    /// <summary>在链条里面查找指定类型的过滤器</summary>
    /// <param name="filter">起始过滤器</param>
    /// <param name="filterType">待查找的过滤器类型</param>
    /// <returns>首个匹配的过滤器</returns>
    public static IFilter? Find(this IFilter filter, Type filterType)
    {
        if (filter == null || filterType == null) return null;

        if (filter.GetType() == filterType) return filter;

        return filter.Next?.Find(filterType);
    }

    /// <summary>在链条里面查找指定类型的过滤器</summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <param name="filter">起始过滤器</param>
    /// <returns>首个匹配的过滤器</returns>
    public static TFilter? Find<TFilter>(this IFilter filter) where TFilter : class, IFilter
        => (TFilter?)Find(filter, typeof(TFilter));
}

/// <summary>数据过滤器基类</summary>
public abstract class FilterBase : IFilter
{
    /// <summary>下一个过滤器</summary>
    public IFilter? Next { get; set; }

    /// <summary>对封包执行过滤器</summary>
    /// <param name="context">过滤上下文</param>
    public virtual void Execute(FilterContext context)
    {
        if (!OnExecute(context) || context.Packet == null) return;

        Next?.Execute(context);
    }

    /// <summary>执行过滤</summary>
    /// <param name="context">过滤上下文</param>
    /// <returns>是否继续执行下一个过滤器</returns>
    protected abstract Boolean OnExecute(FilterContext context);
}