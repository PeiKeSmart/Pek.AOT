using System.IO.Compression;
using System.Security.Cryptography;

using Pek.Buffers;
using Pek.Extension;
using Pek.IO;
using Pek.Security;

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
        var directory = String.Empty;

        var arguments = Environment.GetCommandLineArgs();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].EqualIgnoreCase("-BasePath", "--BasePath") && i + 1 < arguments.Length)
            {
                directory = arguments[i + 1];
                break;
            }
        }

        if (String.IsNullOrWhiteSpace(directory)) directory = Pek.Runtime.GetEnvironmentVariable("BasePath");
        if (!String.IsNullOrWhiteSpace(directory)) BaseDirectory = directory;

        if (String.IsNullOrWhiteSpace(directory)) directory = AppDomain.CurrentDomain.BaseDirectory;
        if (String.IsNullOrWhiteSpace(directory)) directory = Environment.CurrentDirectory;
        if (String.IsNullOrWhiteSpace(directory)) directory = Path.GetTempPath();

        BasePath = GetPath(directory, 1);
        if (String.IsNullOrWhiteSpace(BaseDirectory)) BaseDirectory = directory;
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

    /// <summary>获取基于当前目录的完整路径</summary>
    /// <param name="path">原始路径</param>
    /// <returns>完整路径</returns>
    public static String GetCurrentPath(this String path)
    {
        if (String.IsNullOrWhiteSpace(path)) return path;
        return GetPath(path, 3);
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

    /// <summary>合并多段路径</summary>
    /// <param name="path">基础路径</param>
    /// <param name="ps">附加路径片段</param>
    /// <returns>合并后的路径</returns>
    public static String CombinePath(this String? path, params String[] ps)
    {
        path ??= String.Empty;
        if (ps == null || ps.Length == 0) return path;

        foreach (var item in ps)
        {
            if (!String.IsNullOrWhiteSpace(item)) path = Path.Combine(path, item);
        }

        return path;
    }

    /// <summary>文件路径作为文件信息</summary>
    /// <param name="file">文件路径</param>
    /// <returns>文件信息</returns>
    public static FileInfo AsFile(this String file) => new(file.GetFullPath());

    /// <summary>从文件中读取数据</summary>
    /// <param name="file">文件信息</param>
    /// <param name="offset">偏移量</param>
    /// <param name="count">读取字节数</param>
    /// <returns>字节数组</returns>
    public static Byte[] ReadBytes(this FileInfo file, Int32 offset = 0, Int32 count = -1)
    {
        using var stream = file.OpenRead();
        stream.Position = offset;
        if (count < 0) count = (Int32)(stream.Length - offset);

        return stream.ReadExactly(count);
    }

    /// <summary>把数据写入文件指定位置</summary>
    /// <param name="file">文件信息</param>
    /// <param name="data">数据</param>
    /// <param name="offset">偏移量</param>
    /// <returns>文件信息</returns>
    public static FileInfo WriteBytes(this FileInfo file, Byte[] data, Int32 offset = 0)
    {
        file.FullName.EnsureDirectory(true);
        using var stream = file.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        stream.Position = offset;
        stream.Write(data, 0, data.Length);
        stream.SetLength(stream.Position);

        return file;
    }

    /// <summary>复制到目标文件，目标文件必须已存在，且源文件较新</summary>
    /// <param name="file">源文件</param>
    /// <param name="destFileName">目标文件</param>
    /// <returns>是否复制成功</returns>
    public static Boolean CopyToIfNewer(this FileInfo file, String destFileName)
    {
        if (file == null || !file.Exists) return false;

        var destination = destFileName.AsFile();
        if (destination.Exists && file.LastWriteTime > destination.LastWriteTime)
        {
            file.CopyTo(destFileName, true);
            return true;
        }

        return false;
    }

    /// <summary>打开并读取</summary>
    /// <param name="file">文件信息</param>
    /// <param name="compressed">是否压缩</param>
    /// <param name="func">处理函数</param>
    /// <returns>读取位置</returns>
    public static Int64 OpenRead(this FileInfo file, Boolean compressed, Action<Stream> func)
    {
        if (compressed)
        {
            using var stream = file.OpenRead();
            using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, true);
            using var bufferedStream = new BufferedStream(gzipStream);
            func(bufferedStream);
            return stream.Position;
        }

        using var fileStream = file.OpenRead();
        func(fileStream);
        return fileStream.Position;
    }

    /// <summary>打开并写入</summary>
    /// <param name="file">文件信息</param>
    /// <param name="compressed">是否压缩</param>
    /// <param name="func">处理函数</param>
    /// <returns>写入位置</returns>
    public static Int64 OpenWrite(this FileInfo file, Boolean compressed, Action<Stream> func)
    {
        file.FullName.EnsureDirectory(true);

        using var stream = file.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        if (compressed)
        {
            using var gzipStream = new GZipStream(stream, CompressionLevel.Optimal, true);
            func(gzipStream);
        }
        else
        {
            func(stream);
        }

        stream.SetLength(stream.Position);
        stream.Flush();

        return stream.Position;
    }

    /// <summary>解压缩</summary>
    /// <param name="file">压缩文件</param>
    /// <param name="destDir">目标目录</param>
    /// <param name="overwrite">是否覆盖</param>
    public static void Extract(this FileInfo file, String destDir, Boolean overwrite = false)
    {
        if (destDir.IsNullOrEmpty()) destDir = Path.GetDirectoryName(file.FullName).CombinePath(file.Name);
        destDir = destDir.GetFullPath();

        if (file.Name.EndsWithIgnoreCase(".zip"))
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            ZipFile.ExtractToDirectory(file.FullName, destDir, overwrite);
#else
            throw new NotSupportedException("Zip extract requires NETSTANDARD2.1 or NETCOREAPP.");
#endif
            return;
        }

        if (file.Name.EndsWithIgnoreCase(".tar", ".tar.gz", ".tgz"))
        {
#if NET7_0_OR_GREATER
            destDir.EnsureDirectory(false);
            if (file.Name.EndsWithIgnoreCase(".tar"))
                System.Formats.Tar.TarFile.ExtractToDirectory(file.FullName, destDir, overwrite);
            else
            {
                using var stream = file.OpenRead();
                using var gzipStream = new GZipStream(stream, CompressionMode.Decompress, true);
                using var bufferedStream = new BufferedStream(gzipStream);
                System.Formats.Tar.TarFile.ExtractToDirectory(bufferedStream, destDir, overwrite);
            }
#else
            throw new NotSupportedException("Tar extract requires NET7_0_OR_GREATER.");
#endif
            return;
        }

        throw new NotSupportedException("Only zip/tar/tar.gz/tgz archives are supported in Pek.AOT.");
    }

    /// <summary>压缩文件</summary>
    /// <param name="file">文件信息</param>
    /// <param name="destFile">目标压缩文件</param>
    public static void Compress(this FileInfo file, String destFile)
    {
        if (destFile.IsNullOrEmpty()) destFile = file.Name + ".zip";

        destFile = destFile.GetFullPath();
        if (File.Exists(destFile)) File.Delete(destFile);

        if (destFile.EndsWithIgnoreCase(".zip"))
        {
            using var zip = ZipFile.Open(destFile, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.Optimal);
            return;
        }

        if (destFile.EndsWithIgnoreCase(".tar", ".tar.gz", ".tgz"))
        {
#if NET7_0_OR_GREATER
            if (destFile.EndsWithIgnoreCase(".tar"))
            {
                using var stream = new FileStream(destFile, FileMode.OpenOrCreate, FileAccess.Write);
                using var tarWriter = new System.Formats.Tar.TarWriter(stream, System.Formats.Tar.TarEntryFormat.Pax, false);
                tarWriter.WriteEntry(file.FullName, file.Name);
                stream.SetLength(stream.Position);
            }
            else
            {
                using var stream = new FileStream(destFile, FileMode.OpenOrCreate, FileAccess.Write);
                using var gzipStream = new GZipStream(stream, CompressionMode.Compress, true);
                using var tarWriter = new System.Formats.Tar.TarWriter(gzipStream, System.Formats.Tar.TarEntryFormat.Pax, false);
                tarWriter.WriteEntry(file.FullName, file.Name);
                gzipStream.Flush();
                stream.SetLength(stream.Position);
            }
#else
            throw new NotSupportedException("Tar compress requires NET7_0_OR_GREATER.");
#endif
            return;
        }

        throw new NotSupportedException("Only zip/tar/tar.gz/tgz archives are supported in Pek.AOT.");
    }

    /// <summary>路径作为目录信息</summary>
    /// <param name="dir">目录路径</param>
    /// <returns>目录信息</returns>
    public static DirectoryInfo AsDirectory(this String dir) => new(dir.GetFullPath());

    /// <summary>获取目录内所有符合条件的文件</summary>
    /// <param name="directory">目录</param>
    /// <param name="exts">扩展过滤</param>
    /// <param name="allSub">是否递归</param>
    /// <returns>文件枚举</returns>
    public static IEnumerable<FileInfo> GetAllFiles(this DirectoryInfo directory, String? exts = null, Boolean allSub = false)
    {
        if (directory == null || !directory.Exists) yield break;

        if (String.IsNullOrEmpty(exts)) exts = "*";
        var option = allSub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var pattern in exts.Split(";", "|", ","))
        {
            foreach (var item in directory.GetFiles(pattern, option))
            {
                yield return item;
            }
        }
    }

    /// <summary>复制目录中的文件</summary>
    /// <param name="directory">源目录</param>
    /// <param name="destDirName">目标目录</param>
    /// <param name="exts">扩展过滤</param>
    /// <param name="allSub">是否递归</param>
    /// <param name="callback">回调</param>
    /// <returns>复制结果</returns>
    public static String[] CopyTo(this DirectoryInfo directory, String destDirName, String? exts = null, Boolean allSub = false, Action<String>? callback = null)
    {
        if (!directory.Exists) return [];

        var list = new List<String>();
        var root = directory.FullName.EnsureEnd(Path.DirectorySeparatorChar.ToString());
        foreach (var item in directory.GetAllFiles(exts, allSub))
        {
            var name = item.FullName.TrimStart(root).ToString();
            var destination = destDirName.CombinePath(name);
            callback?.Invoke(name);
            item.CopyTo(destination.EnsureDirectory(true), true);
            list.Add(destination);
        }

        return [.. list];
    }

    /// <summary>对比源目录和目标目录，复制较新的文件</summary>
    /// <param name="directory">源目录</param>
    /// <param name="destDirName">目标目录</param>
    /// <param name="exts">扩展过滤</param>
    /// <param name="allSub">是否递归</param>
    /// <param name="callback">回调</param>
    /// <returns>复制结果</returns>
    public static String[] CopyToIfNewer(this DirectoryInfo directory, String destDirName, String? exts = null, Boolean allSub = false, Action<String>? callback = null)
    {
        var destination = destDirName.AsDirectory();
        if (!destination.Exists) return [];

        var list = new List<String>();
        var root = destination.FullName.EnsureEnd(Path.DirectorySeparatorChar.ToString());
        foreach (var item in destination.GetAllFiles(exts, allSub))
        {
            var name = item.FullName.TrimStart(root).ToString();
            var sourceFile = directory.FullName.CombinePath(name).AsFile();
            if (sourceFile.Exists && item.Exists && sourceFile.LastWriteTime > item.LastWriteTime)
            {
                callback?.Invoke(name);
                sourceFile.CopyTo(item.FullName, true);
                list.Add(sourceFile.FullName);
            }
        }

        return [.. list];
    }

    /// <summary>从多个目录复制较新文件到当前目录</summary>
    /// <param name="directory">当前目录</param>
    /// <param name="source">源目录集合</param>
    /// <param name="exts">扩展过滤</param>
    /// <param name="allSub">是否递归</param>
    /// <returns>复制结果</returns>
    public static String[] CopyIfNewer(this DirectoryInfo directory, String[] source, String? exts = null, Boolean allSub = false)
    {
        var list = new List<String>();
        var current = directory.FullName;
        foreach (var item in source)
        {
            if (item.GetFullPath().EqualIgnoreCase(current)) continue;

            var result = item.AsDirectory().CopyToIfNewer(current, exts, allSub);
            if (result.Length > 0) list.AddRange(result);
        }

        return [.. list];
    }

    /// <summary>压缩目录</summary>
    /// <param name="directory">目录</param>
    /// <param name="destFile">目标文件</param>
    public static void Compress(this DirectoryInfo directory, String? destFile = null) => Compress(directory, destFile, false);

    /// <summary>压缩目录</summary>
    /// <param name="directory">目录</param>
    /// <param name="destFile">目标文件</param>
    /// <param name="includeBaseDirectory">是否包含根目录</param>
    public static void Compress(this DirectoryInfo directory, String? destFile, Boolean includeBaseDirectory)
    {
        if (destFile.IsNullOrEmpty()) destFile = directory.Name + ".zip";

        if (File.Exists(destFile)) File.Delete(destFile);

        if (destFile.EndsWithIgnoreCase(".zip"))
        {
            ZipFile.CreateFromDirectory(directory.FullName, destFile, CompressionLevel.Optimal, includeBaseDirectory);
            return;
        }

        if (destFile.EndsWithIgnoreCase(".tar", ".tar.gz", ".tgz"))
        {
#if NET7_0_OR_GREATER
            if (destFile.EndsWithIgnoreCase(".tar"))
                System.Formats.Tar.TarFile.CreateFromDirectory(directory.FullName, destFile, includeBaseDirectory);
            else
            {
                using var stream = new FileStream(destFile, FileMode.OpenOrCreate, FileAccess.Write);
                using var gzipStream = new GZipStream(stream, CompressionMode.Compress, true);
                System.Formats.Tar.TarFile.CreateFromDirectory(directory.FullName, gzipStream, includeBaseDirectory);
                gzipStream.Flush();
                stream.SetLength(stream.Position);
            }
#else
            throw new NotSupportedException("Tar compress requires NET7_0_OR_GREATER.");
#endif
            return;
        }

        throw new NotSupportedException("Only zip/tar/tar.gz/tgz archives are supported in Pek.AOT.");
    }

    /// <summary>验证文件哈希是否匹配预期值</summary>
    /// <param name="file">文件</param>
    /// <param name="hash">期望哈希</param>
    /// <returns>是否匹配</returns>
    public static Boolean VerifyHash(this FileInfo file, String hash)
    {
        if (file == null || !file.Exists) return false;
        if (String.IsNullOrWhiteSpace(hash)) return false;

        hash = hash.Trim();
        var algorithm = default(String);
        var value = hash;
        var position = hash.IndexOf('$');
        if (position > 0)
        {
            algorithm = hash[..position];
            value = hash[(position + 1)..];
        }

        if (String.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();

        if (algorithm.IsNullOrEmpty())
        {
            algorithm = value.Length switch
            {
                8 => "crc32",
                16 or 32 => "md5",
                40 => "sha1",
                64 => "sha256",
                128 => "sha512",
                _ => null,
            };
            if (algorithm.IsNullOrEmpty()) throw new NotSupportedException("Please specify a hash algorithm prefix.");
        }

        if (algorithm.EqualIgnoreCase("md5"))
        {
            var actual = file.MD5().AsSpan().ToHex();
            if (value.Length == 16) return actual[..16].EqualIgnoreCase(value);
            return actual.EqualIgnoreCase(value);
        }

        if (algorithm.EqualIgnoreCase("crc32", "crc"))
        {
            using var stream = file.OpenRead();
            var actual = Crc32.Compute(stream).ToString("X8");
            return actual.EqualIgnoreCase(value);
        }

        if (algorithm.EqualIgnoreCase("sha1", "sha-1"))
        {
            using var sha1 = SHA1.Create();
            using var stream = file.OpenRead();
            var actual = sha1.ComputeHash(stream).AsSpan().ToHex();
            return actual.EqualIgnoreCase(value);
        }

        if (algorithm.EqualIgnoreCase("sha256", "sha-256"))
        {
            using var sha256 = SHA256.Create();
            using var stream = file.OpenRead();
            var actual = sha256.ComputeHash(stream).AsSpan().ToHex();
            return actual.EqualIgnoreCase(value);
        }

        if (algorithm.EqualIgnoreCase("sha512", "sha-512"))
        {
            using var sha512 = SHA512.Create();
            using var stream = file.OpenRead();
            var actual = sha512.ComputeHash(stream).AsSpan().ToHex();
            return actual.EqualIgnoreCase(value);
        }

        throw new NotSupportedException("Only hash algorithms md5/crc/sha1/sha256/sha512 are supported.");
    }

    private static String GetPath(String path, Int32 mode)
    {
        var separator = Path.DirectorySeparatorChar;
        var alternateSeparator = separator == '/' ? '\\' : '/';
        path = path.Replace(alternateSeparator, separator);

        var directory = mode switch
        {
            1 => !String.IsNullOrWhiteSpace(BaseDirectory) ? BaseDirectory : AppDomain.CurrentDomain.BaseDirectory,
            2 => !String.IsNullOrWhiteSpace(BasePath) ? BasePath : AppDomain.CurrentDomain.BaseDirectory,
            3 => Environment.CurrentDirectory,
            _ => String.Empty,
        };

        if (String.IsNullOrWhiteSpace(directory)) return Path.GetFullPath(path);

        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return Path.GetFullPath(path);

        if (!Pek.Runtime.Mono)
        {
            if (path[0] == alternateSeparator || !Path.IsPathRooted(path))
            {
                path = path.TrimStart('~');
                path = path.TrimStart(separator);
                path = Path.Combine(directory, path);
            }
        }
        else if (path[0] == alternateSeparator || !Path.IsPathRooted(path))
        {
            path = path.TrimStart(separator);
            path = Path.Combine(directory, path);
        }

        return Path.GetFullPath(path);
    }
}
