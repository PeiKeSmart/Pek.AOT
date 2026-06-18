using Pek.Helpers;

namespace Pek.IO;

/// <summary>目录操作辅助类</summary>
public static class DirectoryHelper
{
    #region CreateIfNotExists(创建文件夹，如果不存在)

    /// <summary>创建文件夹，如果不存在</summary>
    /// <param name="directory">要创建的文件夹路径</param>
    public static void CreateIfNotExists(String directory)
    {
        if (String.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    #endregion

    #region IsSubDirectoryOf(是否指定父目录路径的子目录)

    /// <summary>是否指定父目录路径的子目录</summary>
    /// <param name="parentDirectoryPath">父目录路径</param>
    /// <param name="childDirectoryPath">子目录路径</param>
    public static Boolean IsSubDirectoryOf(String parentDirectoryPath, String childDirectoryPath)
    {
        Check.NotNull(parentDirectoryPath, nameof(parentDirectoryPath));
        Check.NotNull(childDirectoryPath, nameof(childDirectoryPath));

        return IsSubDirectoryOf(new DirectoryInfo(parentDirectoryPath), new DirectoryInfo(childDirectoryPath));
    }

    /// <summary>是否指定父目录路径的子目录</summary>
    /// <param name="parentDirectory">父目录</param>
    /// <param name="childDirectory">子目录</param>
    public static Boolean IsSubDirectoryOf(DirectoryInfo parentDirectory, DirectoryInfo childDirectory)
    {
        Check.NotNull(parentDirectory, nameof(parentDirectory));
        Check.NotNull(childDirectory, nameof(childDirectory));

        if (parentDirectory.FullName == childDirectory.FullName)
        {
            return true;
        }

        var parentOfChild = childDirectory.Parent;
        if (parentOfChild == null)
        {
            return false;
        }

        return IsSubDirectoryOf(parentDirectory, parentOfChild);
    }

    #endregion

    #region ChangeCurrentDirectory(更改当前目录)

    /// <summary>更改当前目录</summary>
    /// <param name="targetDirectory">目标目录</param>
    public static IDisposable ChangeCurrentDirectory(String targetDirectory)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (currentDirectory.Equals(targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return NullDisposable.Instance;
        }

        Directory.SetCurrentDirectory(targetDirectory);

        return new DisposeAction(() => { Directory.SetCurrentDirectory(currentDirectory); });
    }

    #endregion

    #region GetFileNames(获取指定目录中的文件列表)

    /// <summary>获取指定目录中的文件列表</summary>
    /// <param name="directoryPath">目录的绝对路径</param>
    /// <param name="pattern">通配符</param>
    public static String[] GetFileNames(String directoryPath, String pattern = "*")
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new FileNotFoundException();
        }

        return Directory.GetFiles(directoryPath, pattern);
    }

    /// <summary>获取指定目录及子目录中所有文件列表</summary>
    /// <param name="directoryPath">目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串。"*"代表0或N个字符，"?"代表1个字符。范例："Log*.xml"表示搜索所有以Log开头的Xml文件。</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    public static String[] GetFileNames(String directoryPath, String searchPattern, Boolean isSearchChild)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new FileNotFoundException();
        }

        try
        {
            return Directory.GetFiles(directoryPath, searchPattern,
                isSearchChild ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
        catch (IOException e)
        {
            throw e;
        }
    }

    #endregion

    #region GetDirectories(获取指定目录中所有子目录列表)

    /// <summary>获取指定目录中所有子目录列表</summary>
    /// <param name="directoryPath">目录的绝对路径</param>
    public static String[] GetDirectories(String directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new FileNotFoundException();
        }

        return Directory.GetDirectories(directoryPath);
    }

    #endregion

    #region Contains(查找指定目录中是否存在指定的文件)

    /// <summary>查找指定目录中是否存在指定的文件</summary>
    /// <param name="directoryPath">目录的绝对路径</param>
    /// <param name="searchPattern">模式字符串。"*"代表0或N个字符，"?"代表1个字符。范例："Log*.xml"表示搜索所有以Log开头的Xml文件。</param>
    /// <param name="isSearchChild">是否搜索子目录</param>
    public static Boolean Contains(String directoryPath, String searchPattern, Boolean isSearchChild = false)
    {
        try
        {
            var fileNames = GetFileNames(directoryPath, searchPattern, isSearchChild);
            return fileNames.Length != 0;
        }
        catch (Exception e)
        {
            throw e;
        }
    }

    #endregion

    #region IsEmpty(是否空目录)

    /// <summary>是否空目录</summary>
    /// <param name="directoryPath">目录的绝对路径</param>
    public static Boolean IsEmpty(String directoryPath)
    {
        try
        {
            var fileNames = GetFileNames(directoryPath);
            if (fileNames.Length > 0)
            {
                return false;
            }

            var directoryNames = GetDirectories(directoryPath);
            return directoryNames.Length <= 0;
        }
        catch
        {
            return true;
        }
    }

    #endregion
}
