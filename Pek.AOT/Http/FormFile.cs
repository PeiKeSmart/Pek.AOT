using Pek.Data;
using Pek.Extension;
using Pek.IO;

namespace Pek.Http;

/// <summary>表单部分</summary>
public class FormFile
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = null!;

    /// <summary>内容描述</summary>
    public String? ContentDisposition { get; set; }

    /// <summary>内容类型</summary>
    public String? ContentType { get; set; }

    /// <summary>文件名</summary>
    public String? FileName { get; set; }

    /// <summary>数据</summary>
    public IPacket? Data { get; set; }

    /// <summary>长度</summary>
    public Int64 Length => Data?.Total ?? 0;

    /// <summary>是否为空</summary>
    public Boolean IsEmpty => Length == 0;
    #endregion

    #region 读取
    /// <summary>打开数据流</summary>
    /// <returns>只读流</returns>
    public Stream? OpenReadStream() => Data?.GetStream(false);

    /// <summary>复制数据到目标流</summary>
    /// <param name="destination">目标流</param>
    public void WriteTo(Stream destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));

        Data?.CopyTo(destination);
    }
    #endregion

    #region 文件保存
    /// <summary>获取安全文件名</summary>
    /// <param name="strict">是否替换非法字符</param>
    /// <returns>安全文件名</returns>
    public String? GetSafeFileName(Boolean strict = true)
    {
        if (FileName.IsNullOrEmpty()) return null;

        var name = Path.GetFileName(FileName);
        if (name.IsNullOrEmpty()) return null;

        if (strict)
        {
            foreach (var item in Path.GetInvalidFileNameChars())
            {
                if (name!.IndexOf(item) >= 0) name = name.Replace(item, '_');
            }
        }

        return name;
    }

    /// <summary>保存到文件</summary>
    /// <param name="fileName">文件名</param>
    public void SaveToFile(String? fileName = null) => SaveToFile(fileName, true, false);

    /// <summary>保存到文件</summary>
    /// <param name="fileName">文件名</param>
    /// <param name="overwrite">是否覆盖</param>
    /// <param name="sanitize">是否清理文件名</param>
    public void SaveToFile(String? fileName, Boolean overwrite, Boolean sanitize)
    {
        if (fileName.IsNullOrEmpty()) fileName = sanitize ? GetSafeFileName() : Path.GetFileName(FileName);
        if (fileName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(fileName));
        if (Data == null) throw new ArgumentNullException(nameof(Data));

        InternalSave(fileName!, overwrite);
    }

    /// <summary>保存到目录</summary>
    /// <param name="directory">目录</param>
    /// <param name="overwrite">是否覆盖</param>
    /// <returns>完整路径</returns>
    public String SaveToDirectory(String directory, Boolean overwrite = false)
    {
        if (directory.IsNullOrEmpty()) throw new ArgumentNullException(nameof(directory));

        var name = GetSafeFileName() ?? throw new ArgumentNullException(nameof(FileName));
        directory.EnsureDirectory(false);

        var fullName = Path.Combine(directory, name);
        SaveToFile(fullName, overwrite, false);
        return fullName;
    }

    private void InternalSave(String fileName, Boolean overwrite)
    {
        fileName.EnsureDirectory(true);
        var fullName = fileName.GetFullPath();
        if (!overwrite && File.Exists(fullName)) throw new IOException("目标文件已存在且不允许覆盖：" + fullName);

        using var stream = File.Open(fullName, FileMode.Create, FileAccess.Write, FileShare.None);
        Data?.CopyTo(stream);
        stream.Flush();
    }
    #endregion
}