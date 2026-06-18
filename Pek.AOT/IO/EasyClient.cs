// AOT: skipped - EasyClient depends on ApiHttpClient, TokenHttpFilter from Pek.Remoting
// which have not been migrated yet. Also uses System.Web.HttpUtility.UrlEncode.
// Original source: DH.NCore/DH.NCore/IO/EasyClient.cs
// Will be migrated after Remoting batch is complete.
using Pek.Data;

namespace Pek.IO;

/// <summary>文件存储客户端 - 待 Remoting 迁移完成后补齐</summary>
public class EasyClient : IObjectStorage
{
    /// <summary>服务端地址</summary>
    public String? Server { get; set; }

    /// <summary>应用标识</summary>
    public String? AppId { get; set; }

    /// <summary>应用密钥</summary>
    public String? Secret { get; set; }

    /// <summary>基础控制器路径。默认/io/</summary>
    public String BaseAction { get; set; } = "/io/";

    /// <summary>是否支持获取文件直接访问Url</summary>
    public Boolean CanGetUrl => true;

    /// <summary>是否支持删除</summary>
    public Boolean CanDelete => true;

    /// <summary>是否支持搜索</summary>
    public Boolean CanSearch => true;

    /// <summary>是否支持复制</summary>
    public Boolean CanCopy => false;

    /// <summary>上传对象 - AOT模式下暂不支持，依赖未迁移的 Remoting 组件</summary>
    public virtual Task<IObjectInfo?> PutAsync(String id, IPacket data) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>根据Id获取对象</summary>
    public virtual Task<IObjectInfo?> GetAsync(String id) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>获取对象下载Url</summary>
    public virtual Task<String?> GetUrlAsync(String id) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>检查文件是否存在</summary>
    public virtual Task<Boolean> ExistsAsync(String id) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>删除文件对象</summary>
    public virtual Task<Int32> DeleteAsync(String id) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>批量删除文件对象</summary>
    public virtual Task<Int32> DeleteAsync(String[] ids) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>复制文件对象</summary>
    public virtual Task<IObjectInfo?> CopyAsync(String sourceId, String destId) => throw new NotSupportedException();

    /// <summary>搜索文件</summary>
    public virtual Task<IList<IObjectInfo>?> SearchAsync(String? pattern = null, Int32 start = 0, Int32 count = 100) => throw new NotSupportedException("EasyClient requires Pek.Remoting which has not been migrated yet.");

    /// <summary>获取文件对象（旧版）</summary>
    [Obsolete("请使用 GetAsync")]
    public virtual Task<IObjectInfo?> Get(String id) => GetAsync(id);

    /// <summary>获取文件直接访问Url（旧版）</summary>
    [Obsolete("请使用 GetUrlAsync")]
    public virtual Task<String?> GetUrl(String id) => GetUrlAsync(id);

    /// <summary>上传文件对象（旧版）</summary>
    [Obsolete("请使用 PutAsync")]
    public virtual Task<IObjectInfo?> Put(String id, IPacket data) => PutAsync(id, data);

    /// <summary>删除文件对象（旧版）</summary>
    [Obsolete("请使用 DeleteAsync")]
    public virtual Task<Int32> Delete(String id) => DeleteAsync(id);

    /// <summary>搜索文件（旧版）</summary>
    [Obsolete("请使用 SearchAsync")]
    public virtual Task<IList<IObjectInfo>?> Search(String? pattern = null, Int32 start = 0, Int32 count = 100) => SearchAsync(pattern, start, count);
}
