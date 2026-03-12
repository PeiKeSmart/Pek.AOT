namespace System.IO;

/// <summary>路径操作帮助</summary>
public static class PathHelper
{
    /// <summary>基础路径</summary>
    public static String BasePath { get; set; } = String.Empty;

    /// <summary>基准目录</summary>
    public static String BaseDirectory { get; set; } = String.Empty;

    static PathHelper()
    {
        var directory = Pek.Runtime.GetEnvironmentVariable("BasePath");
        if (String.IsNullOrWhiteSpace(directory)) directory = AppDomain.CurrentDomain.BaseDirectory;
        if (String.IsNullOrWhiteSpace(directory)) directory = Environment.CurrentDirectory;
        if (String.IsNullOrWhiteSpace(directory)) directory = Path.GetTempPath();

        BaseDirectory = directory;
        BasePath = GetPath(directory, 1);
    }

    /// <summary>获取基于应用目录的完整路径</summary>
    /// <param name="path">原始路径</param>
    /// <returns>完整路径</returns>
    public static String GetFullPath(this String path)
    {
        if (String.IsNullOrWhiteSpace(path)) return path;
        return GetPath(path, 1);
    }

    /// <summary>获取基础路径</summary>
    /// <param name="path">原始路径</param>
    /// <returns>基础路径</returns>
    public static String GetBasePath(this String path)
    {
        if (String.IsNullOrWhiteSpace(path)) return path;
        return GetPath(path, 2);
    }

    /// <summary>确保目录存在</summary>
    /// <param name="path">文件或目录路径</param>
    /// <param name="isfile">是否文件路径</param>
    /// <returns>完整路径</returns>
    public static String EnsureDirectory(this String path, Boolean isfile = true)
    {
        if (String.IsNullOrWhiteSpace(path)) return path;

        path = path.GetFullPath();
        if (File.Exists(path) || Directory.Exists(path)) return path;

        var directory = path;
        if (directory[^1] == Path.DirectorySeparatorChar)
            directory = Path.GetDirectoryName(path)!;
        else if (isfile)
            directory = Path.GetDirectoryName(path)!;

        if (!String.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
        return path;
    }

    private static String GetPath(String path, Int32 mode)
    {
        var directory = mode switch
        {
            1 => !String.IsNullOrWhiteSpace(BaseDirectory) ? BaseDirectory : AppDomain.CurrentDomain.BaseDirectory,
            2 => !String.IsNullOrWhiteSpace(BasePath) ? BasePath : AppDomain.CurrentDomain.BaseDirectory,
            _ => Environment.CurrentDirectory,
        };

        if (Path.IsPathRooted(path)) return Path.GetFullPath(path);

        path = path.TrimStart('~').TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(directory, path));
    }
}
