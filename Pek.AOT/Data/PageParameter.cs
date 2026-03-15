using System.Runtime.Serialization;
using System.Xml.Serialization;

using Pek.Extension;

namespace Pek.Data;

/// <summary>分页参数信息。可携带统计和数据权限扩展查询等信息</summary>
public class PageParameter
{
    private String? _Sort;

    /// <summary>排序字段，前台接收，便于做 SQL 安全性校验</summary>
    [XmlIgnore, IgnoreDataMember]
    public virtual String? Sort
    {
        get => _Sort;
        set
        {
            _Sort = value;

            if (!_Sort.IsNullOrEmpty() && !_Sort.Contains(','))
            {
                _Sort = _Sort.Trim();
                var p = _Sort.LastIndexOf(' ');
                if (p > 0)
                {
                    var dir = _Sort[(p + 1)..];
                    if (dir.EqualIgnoreCase("asc"))
                    {
                        Desc = false;
                        _Sort = _Sort[..p].Trim();
                    }
                    else if (dir.EqualIgnoreCase("desc"))
                    {
                        Desc = true;
                        _Sort = _Sort[..p].Trim();
                    }
                }
            }

            OrderBy = null;
        }
    }

    /// <summary>是否降序</summary>
    [XmlIgnore, IgnoreDataMember]
    public virtual Boolean Desc { get; set; }

    /// <summary>页面索引。从 1 开始，默认 1</summary>
    public virtual Int32 PageIndex { get; set; } = 1;

    /// <summary>页面大小。默认 20，若为 0 表示不分页</summary>
    public virtual Int32 PageSize { get; set; } = 20;

    /// <summary>总记录数</summary>
    public virtual Int64 TotalCount { get; set; }

    /// <summary>页数</summary>
    public virtual Int64 PageCount => PageSize <= 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;

    /// <summary>自定义排序字句</summary>
    public virtual String? OrderBy { get; set; }

    /// <summary>开始行</summary>
    [XmlIgnore, IgnoreDataMember]
    public virtual Int64 StartRow { get; set; } = -1;

    /// <summary>是否获取总记录数</summary>
    [XmlIgnore, IgnoreDataMember]
    public Boolean RetrieveTotalCount { get; set; }

    /// <summary>状态。用于传递统计、扩展查询等用户数据</summary>
    [XmlIgnore, IgnoreDataMember]
    public virtual Object? State { get; set; }

    /// <summary>是否获取统计</summary>
    [XmlIgnore, IgnoreDataMember]
    public Boolean RetrieveState { get; set; }

    /// <summary>实例化分页参数</summary>
    public PageParameter() { }

    /// <summary>通过另一个分页参数来实例化当前分页参数</summary>
    /// <param name="pm">源分页参数</param>
    public PageParameter(PageParameter pm) => CopyFrom(pm);

    /// <summary>从另一个分页参数拷贝到当前分页参数</summary>
    /// <param name="pm">源分页参数</param>
    /// <returns>当前实例</returns>
    public virtual PageParameter CopyFrom(PageParameter pm)
    {
        if (pm == null) return this;

        OrderBy = pm.OrderBy;
        Sort = pm.Sort;
        Desc = pm.Desc;
        PageIndex = pm.PageIndex;
        PageSize = pm.PageSize;
        StartRow = pm.StartRow;
        TotalCount = pm.TotalCount;
        RetrieveTotalCount = pm.RetrieveTotalCount;
        State = pm.State;
        RetrieveState = pm.RetrieveState;

        return this;
    }

    /// <summary>获取表示分页参数唯一性的键值</summary>
    /// <returns>唯一键</returns>
    public virtual String GetKey() => $"{PageIndex}-{PageCount}-{OrderBy}";

    /// <summary>验证分页参数的有效性</summary>
    /// <returns>是否有效</returns>
    public virtual Boolean IsValid() => PageIndex > 0 && PageSize >= 0;

    /// <summary>重置分页参数到默认状态</summary>
    public virtual void Reset()
    {
        PageIndex = 1;
        PageSize = 20;
        Sort = null;
        OrderBy = null;
        Desc = false;
        StartRow = -1;
        TotalCount = 0;
        RetrieveTotalCount = false;
        State = null;
        RetrieveState = false;
    }
}