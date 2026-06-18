using System.Globalization;
using System.Text;

namespace Pek.IO;

/// <summary>文件操作对象类型</summary>
public enum FsoMethod
{
    /// <summary>文件夹</summary>
    Folder,
    /// <summary>文件</summary>
    File,
    /// <summary>文件夹和文件均包括</summary>
    All
}

/// <summary>文件管理类（AOT安全 - 纯文件系统操作，无反射）</summary>
public abstract class FileSystemObject
{
    /// <summary>编码转换</summary>
    /// <param name="content">内容</param>
    /// <param name="srcEncoding">源编码</param>
    /// <param name="targetEncoding">目标编码</param>
    /// <returns></returns>
    public static String ConvertEncoding(String content, Encoding srcEncoding, Encoding targetEncoding)
    {
        if ((srcEncoding != targetEncoding) && !String.IsNullOrEmpty(content))
        {
            var bytes = srcEncoding.GetBytes(content);
            bytes = Encoding.Convert(srcEncoding, targetEncoding, bytes);
            var chars = new Char[targetEncoding.GetCharCount(bytes, 0, bytes.Length)];
            targetEncoding.GetChars(bytes, 0, bytes.Length, chars, 0);
            content = new String(chars);
        }
        return content;
    }

    /// <summary>将文件大小转换为显示字符串</summary>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <returns></returns>
    public static String ConvertSizeToShow(Int64 fileSize)
    {
        var num = fileSize / 0x400L;
        if (num < 1L)
            return (fileSize.ToString(CultureInfo.CurrentCulture) + "<span style='color:red'>&nbsp;&nbsp;B</span>");

        if (num < 0x400L)
            return (num.ToString(CultureInfo.CurrentCulture) + "<span style='color:red'>&nbsp;KB</span>");

        var num2 = num / 0x400L;
        if (num2 < 1L)
            return (num.ToString(CultureInfo.CurrentCulture) + "<span style='color:red'>&nbsp;KB</span>");

        if (num2 >= 0x400L)
        {
            num2 /= 0x400L;
            return (num2.ToString(CultureInfo.CurrentCulture) + "<span style='color:red'>&nbsp;GB</span>");
        }
        return (num2.ToString(CultureInfo.CurrentCulture) + "<span style='color:red'>&nbsp;MB</span>");
    }

    /// <summary>复制目录</summary>
    /// <param name="oldDir">源目录</param>
    /// <param name="newDir">目标目录</param>
    public static void CopyDirectory(String oldDir, String newDir)
    {
        var od = new DirectoryInfo(oldDir);
        CopyDirInfo(od, oldDir, newDir);
    }

    private static void CopyDirInfo(DirectoryInfo od, String oldDir, String newDir)
    {
        if (!IsExist(newDir, FsoMethod.Folder))
            Create(newDir, FsoMethod.Folder);

        foreach (var info in od.GetDirectories())
        {
            CopyDirInfo(info, info.FullName, newDir + info.FullName.Replace(oldDir, String.Empty));
        }
        foreach (var info2 in od.GetFiles())
        {
            CopyFile(info2.FullName, newDir + info2.FullName.Replace(oldDir, String.Empty));
        }
    }

    /// <summary>复制目录信息列表</summary>
    /// <param name="parent">父列表</param>
    /// <param name="child">子列表</param>
    /// <returns></returns>
    public static List<DirectoryAllInfo> CopyDT(List<DirectoryAllInfo> parent, List<DirectoryAllInfo> child)
    {
        foreach (var row in child)
        {
            parent.Add(row);
        }
        return parent;
    }

    /// <summary>复制文件</summary>
    /// <param name="oldFile">源文件</param>
    /// <param name="newFile">目标文件</param>
    public static void CopyFile(String oldFile, String newFile)
    {
        System.IO.File.Copy(oldFile, newFile, true);
    }

    /// <summary>复制文件流</summary>
    /// <param name="oldPath">源路径</param>
    /// <param name="newPath">目标路径</param>
    /// <returns></returns>
    public static Boolean CopyFileStream(String oldPath, String newPath)
    {
        try
        {
            var input = new FileStream(oldPath, FileMode.Open, FileAccess.Read);
            var output = new FileStream(newPath, FileMode.Create, FileAccess.Write);
            var reader = new BinaryReader(input);
            var writer = new BinaryWriter(output);
            reader.BaseStream.Seek(0L, SeekOrigin.Begin);
            reader.BaseStream.Seek(0L, SeekOrigin.End);
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                writer.Write(reader.ReadByte());
            }
            reader.Dispose();
            writer.Dispose();
            input.Flush();
            input.Dispose();
            output.Flush();
            output.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>创建文件或文件夹</summary>
    /// <param name="file">路径</param>
    /// <param name="method">方法</param>
    public static void Create(String file, FsoMethod method)
    {
        try
        {
            if (method == FsoMethod.File)
                WriteFile(file, String.Empty);
            else if (method == FsoMethod.Folder)
                Directory.CreateDirectory(file);
        }
        catch
        {
            throw new UnauthorizedAccessException("没有权限！");
        }
    }

    /// <summary>删除文件或文件夹</summary>
    /// <param name="file">路径</param>
    /// <param name="method">方法</param>
    public static void Delete(String file, FsoMethod method)
    {
        if ((method == FsoMethod.File) && System.IO.File.Exists(file))
            System.IO.File.Delete(file);

        if ((method == FsoMethod.Folder) && Directory.Exists(file))
            Directory.Delete(file, true);
    }

    private static Int64[] DirInfo(DirectoryInfo d)
    {
        var numArray = new Int64[3];
        var num = 0L;
        var num2 = 0L;
        var num3 = 0L;
        var files = d.GetFiles();
        num3 += files.Length;
        foreach (var info in files)
        {
            num += info.Length;
        }
        var directories = d.GetDirectories();
        num2 += directories.Length;
        foreach (var info2 in directories)
        {
            var dirInfo = DirInfo(info2);
            num += dirInfo[0];
            num2 += dirInfo[1];
            num3 += dirInfo[2];
        }
        numArray[0] = num;
        numArray[1] = num2;
        numArray[2] = num3;
        return numArray;
    }

    private static List<DirectoryAllInfo> GetDirectoryAllInfo(DirectoryInfo d, FsoMethod method)
    {
        var list = new List<DirectoryAllInfo>();
        foreach (var info in d.GetDirectories())
        {
            if (method == FsoMethod.File)
            {
                list = CopyDT(list, GetDirectoryAllInfo(info, method));
            }
            else
            {
                var model = new DirectoryAllInfo
                {
                    name = info.Name,
                    rname = info.FullName,
                    content_type = String.Empty,
                    type = 1,
                    path = info.FullName.Replace(info.Name, String.Empty),
                    creatime = info.CreationTime,
                    lastWriteTime = info.LastWriteTime,
                    size = 0
                };
                list.Add(model);
                list = CopyDT(list, GetDirectoryAllInfo(info, method));
            }
        }
        if (method != FsoMethod.Folder)
        {
            foreach (var info2 in d.GetFiles())
            {
                var model = new DirectoryAllInfo
                {
                    name = info2.Name,
                    rname = info2.FullName,
                    content_type = info2.Extension.Replace(".", String.Empty),
                    type = 2,
                    path = info2.DirectoryName + @"\",
                    creatime = info2.CreationTime,
                    lastWriteTime = info2.LastWriteTime,
                    size = info2.Length
                };
                list.Add(model);
            }
        }
        return list;
    }

    /// <summary>获取目录全部信息</summary>
    /// <param name="dir">目录</param>
    /// <param name="method">方法</param>
    /// <returns></returns>
    public static List<DirectoryAllInfo> GetDirectoryAllInfos(String dir, FsoMethod method)
    {
        List<DirectoryAllInfo> directoryAllInfo;
        try
        {
            var d = new DirectoryInfo(dir);
            directoryAllInfo = GetDirectoryAllInfo(d, method);
        }
        catch (Exception exception)
        {
            throw new FileNotFoundException(exception.ToString());
        }
        return directoryAllInfo;
    }

    /// <summary>获取目录信息列表</summary>
    /// <param name="dir">目录</param>
    /// <param name="method">方法</param>
    /// <returns></returns>
    public static List<DirectoryInfos> GetDirectoryInfos(String dir, FsoMethod method)
    {
        var list = new List<DirectoryInfos>();
        dir = dir.GetFullPath();
        if (method != FsoMethod.File)
        {
            for (var i = 0; i < Directory.GetDirectories(dir).Length; i++)
            {
                var model = new DirectoryInfos();
                var d = new DirectoryInfo(Directory.GetDirectories(dir)[i]);
                var numArray = DirInfo(d);
                model.name = d.Name;
                model.type = 1;
                model.size = numArray[0];
                model.content_type = String.Empty;
                model.createTime = d.CreationTime;
                model.lastWriteTime = d.LastWriteTime;
                model.path = d.Name;
                model.Id = i + 1;
                list.Add(model);
            }
        }
        if (method != FsoMethod.Folder)
        {
            for (var j = 0; j < Directory.GetFiles(dir).Length; j++)
            {
                var model = new DirectoryInfos();
                var info2 = new System.IO.FileInfo(Directory.GetFiles(dir)[j]);
                model.name = info2.Name;
                model.type = 2;
                model.size = info2.Length;
                model.content_type = info2.Extension.Replace(".", String.Empty);
                model.createTime = info2.CreationTime;
                model.lastWriteTime = info2.LastWriteTime;
                model.path = info2.Name;
                model.Id = j + 1;
                list.Add(model);
            }
        }
        return list;
    }

    /// <summary>获取目录信息</summary>
    /// <param name="dir">目录</param>
    /// <returns></returns>
    public static Int64[] GetDirInfos(String dir)
    {
        var numArray = new Int64[3];
        var d = new DirectoryInfo(dir);
        return DirInfo(d);
    }

    /// <summary>获取文件流编码</summary>
    /// <param name="stream">文件流</param>
    /// <returns></returns>
    public static Encoding GetEncoding(FileStream stream)
    {
        var bigEndianUnicode = Encoding.UTF8;
        if ((stream != null) && (stream.Length >= 2L))
        {
            Byte num = 0;
            Byte num2 = 0;
            Byte num3 = 0;
            var offset = stream.Seek(0L, SeekOrigin.Begin);
            stream.Seek(0L, SeekOrigin.Begin);
            num = System.Convert.ToByte(stream.ReadByte());
            num2 = System.Convert.ToByte(stream.ReadByte());
            if (stream.Length >= 3L)
                num3 = System.Convert.ToByte(stream.ReadByte());

            if (stream.Length >= 4L)
                System.Convert.ToByte(stream.ReadByte());

            if ((num == 0xfe) && (num2 == 0xff))
                bigEndianUnicode = Encoding.BigEndianUnicode;

            if (((num == 0xff) && (num2 == 0xfe)) && (num3 != 0xff))
                bigEndianUnicode = Encoding.Unicode;

            if (((num == 0xef) && (num2 == 0xbb)) && (num3 == 0xbf))
                bigEndianUnicode = Encoding.UTF8;

            stream.Seek(offset, SeekOrigin.Begin);
        }
        stream.Dispose();
        return bigEndianUnicode;
    }

    /// <summary>获取指定文件大小（显示用）</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns></returns>
    public static String GetFileSize(String filePath)
    {
        var info = new System.IO.FileInfo(filePath);
        var num = (Single)(info.Length / 0x400L);
        return (num.ToString(CultureInfo.CurrentCulture) + "KB");
    }

    /// <summary>获取文件修改时间</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns></returns>
    public static DateTime GetFileUpdateTime(String filePath)
    {
        var info = new System.IO.FileInfo(filePath);
        var dt = info.LastWriteTime;
        return dt;
    }

    /// <summary>判断是否存在</summary>
    /// <param name="file">物理路径</param>
    /// <param name="method">方法</param>
    /// <returns></returns>
    public static Boolean IsExist(String file, FsoMethod method)
    {
        if (method == FsoMethod.File)
            return System.IO.File.Exists(file);

        return ((method == FsoMethod.Folder) && Directory.Exists(file));
    }

    /// <summary>移动文件或文件夹</summary>
    /// <param name="oldFile">源路径</param>
    /// <param name="newFile">目标路径</param>
    /// <param name="method">方法</param>
    public static void Move(String oldFile, String newFile, FsoMethod method)
    {
        if (method == FsoMethod.File)
            System.IO.File.Move(oldFile, newFile);

        if (method == FsoMethod.Folder)
            Directory.Move(oldFile, newFile);
    }

    /// <summary>读取文件</summary>
    /// <param name="filePath">文件路径</param>
    /// <returns></returns>
    public static String ReadFile(String filePath)
    {
        var content = String.Empty;
        if (!System.IO.File.Exists(filePath))
            return content;

        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            var encoding = GetEncoding(stream);
            var reader = new StreamReader(System.IO.File.OpenRead(filePath), encoding, true, 0x400);
            content = reader.ReadToEnd();
            reader.Dispose();
            if (encoding != Encoding.UTF8)
                content = ConvertEncoding(content, encoding, Encoding.UTF8);

            return content;
        }
    }

    /// <summary>使用GBK编码读取文件</summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="gbk">GBK标识（保留参数）</param>
    /// <returns></returns>
    public static String ReadFile(String filePath, String gbk)
    {
        var content = String.Empty;
        if (!System.IO.File.Exists(filePath))
            return content;

        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            var encoding = Encoding.GetEncoding(936);
            var reader = new StreamReader(System.IO.File.OpenRead(filePath), encoding, true, 0x400);
            content = reader.ReadToEnd();
            reader.Dispose();
            if (encoding != Encoding.UTF8)
                content = ConvertEncoding(content, encoding, Encoding.UTF8);

            return content;
        }
    }

    /// <summary>替换文件内容</summary>
    /// <param name="dir">目录</param>
    /// <param name="originalContent">原始内容</param>
    /// <param name="newContent">新内容</param>
    public static void ReplaceFileContent(String dir, String originalContent, String newContent)
    {
        if (!String.IsNullOrEmpty(originalContent))
        {
            var info = new DirectoryInfo(dir);
            foreach (var info2 in info.GetFiles("*.*", SearchOption.AllDirectories))
            {
                var reader = info2.OpenText();
                var str = reader.ReadToEnd();
                reader.Dispose();
                if (str.Contains(originalContent))
                {
                    str = str.Replace(originalContent, newContent);
                    var writer = new StreamWriter(System.IO.File.OpenWrite(info2.FullName));
                    writer.Write(str);
                    writer.Dispose();
                }
            }
        }
    }

    /// <summary>搜索文件内容</summary>
    /// <param name="dir">目录</param>
    /// <param name="searchPattern">搜索模式</param>
    /// <param name="searchKeyword">搜索关键词</param>
    /// <returns></returns>
    public static List<DirectoryInfos> SearchFileContent(String dir, String searchPattern, String searchKeyword)
    {
        var list = new List<DirectoryInfos>();
        var info = new DirectoryInfo(dir);
        foreach (var info2 in info.GetFiles(searchPattern, SearchOption.AllDirectories))
        {
            var model = new DirectoryInfos();
            var reader = info2.OpenText();
            var str = reader.ReadToEnd();
            reader.Dispose();
            if (str.Contains(searchKeyword))
            {
                model.name = info2.FullName.Remove(0, info.FullName.Length);
                model.type = 2;
                model.size = info2.Length;
                model.content_type = info2.Extension.Replace(".", String.Empty);
                model.createTime = info2.CreationTime;
                model.lastWriteTime = info2.LastWriteTime;
                model.path = info2.DirectoryName + @"\";
                model.Id = list.Count + 1;
                list.Add(model);
            }
        }
        return list;
    }

    /// <summary>写入文件</summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="content">内容</param>
    public static void WriteFile(String filePath, String content)
    {
        if (String.IsNullOrEmpty(filePath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir) && !String.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch
        {
            throw new UnauthorizedAccessException("没有权限！");
        }
    }
}

/// <summary>目录全部信息</summary>
public class DirectoryAllInfo
{
    /// <summary>名称</summary>
    public String name { get; set; }
    /// <summary>真实名称</summary>
    public String rname { get; set; }
    /// <summary>内容类型</summary>
    public String content_type { get; set; }
    /// <summary>类型：1-文件夹，2-文件</summary>
    public Int32 type { get; set; }
    /// <summary>路径</summary>
    public String path { get; set; }
    /// <summary>创建时间</summary>
    public DateTime creatime { get; set; }
    /// <summary>最后写入时间</summary>
    public DateTime lastWriteTime { get; set; }
    /// <summary>大小</summary>
    public Int64 size { get; set; }
}

/// <summary>目录信息</summary>
public class DirectoryInfos
{
    /// <summary>ID</summary>
    public Int32 Id { get; set; }
    /// <summary>名称</summary>
    public String name { get; set; }
    /// <summary>类型：1-文件夹，2-文件</summary>
    public Int32 type { get; set; }
    /// <summary>内容类型</summary>
    public String content_type { get; set; }
    /// <summary>大小</summary>
    public Int64 size { get; set; }
    /// <summary>创建时间</summary>
    public DateTime createTime { get; set; }
    /// <summary>最后写入时间</summary>
    public DateTime lastWriteTime { get; set; }
    /// <summary>路径</summary>
    public String path { get; set; }
}
