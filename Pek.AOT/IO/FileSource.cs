using System;

using Pek.Extension;
using System.Collections.Generic;

using Pek.Extension;
using System.IO;

using Pek.Extension;
using System.Linq;

using Pek.Extension;
using System.Reflection;

using Pek.Extension;

namespace Pek.IO;

/// <summary>鏂囦欢璧勬簮</summary>
public static class FileSource
{
    /// <summary>閲婃斁鏂囦欢</summary>
    /// <param name="asm"></param>
    /// <param name="fileName"></param>
    /// <param name="destFile"></param>
    /// <param name="overWrite"></param>
    public static void ReleaseFile(this Assembly asm, String fileName, String destFile = null, Boolean overWrite = false)
    {
        if (fileName.IsNullOrEmpty()) return;

        if (asm == null) asm = Assembly.GetCallingAssembly();
        var stream = GetFileResource(asm, fileName);
        if (stream == null) throw new ArgumentException(nameof(fileName), $"在程序集{asm.GetName().Name}中无法找到名为{fileName}的资源！");

        if (destFile.IsNullOrEmpty()) destFile = fileName;
        destFile = destFile.GetFullPath();

        if (File.Exists(destFile) && !overWrite) return;

        //var path = Path.GetDirectoryName(dest);
        //if (!path.IsNullOrWhiteSpace() && !Directory.Exists(path)) Directory.CreateDirectory(path);
        destFile.EnsureDirectory(true);
        try
        {
            if (File.Exists(destFile)) File.Delete(destFile);

            using var fs = File.Create(destFile);
            //IOHelper.CopyTo(stream, fs);
            stream.CopyTo(fs);
        }
        catch { }
        finally { stream.Dispose(); }
    }

    /// <summary>閲婃斁鏂囦欢澶?/summary>
    /// <param name="asm"></param>
    /// <param name="prefix"></param>
    /// <param name="dest"></param>
    /// <param name="overWrite"></param>
    public static void ReleaseFolder(this Assembly asm, String prefix, String dest, Boolean overWrite = false)
    {
        if (asm == null) asm = Assembly.GetCallingAssembly();
        ReleaseFolder(asm, prefix, dest, overWrite, null);
    }

    /// <summary>閲婃斁鏂囦欢澶?/summary>
    /// <param name="asm"></param>
    /// <param name="prefix"></param>
    /// <param name="dest"></param>
    /// <param name="overWrite"></param>
    /// <param name="filenameResolver"></param>
    public static void ReleaseFolder(this Assembly asm, String prefix, String dest, Boolean overWrite = false, Func<String, String> filenameResolver = null)
    {
        if (asm == null) asm = Assembly.GetCallingAssembly();

        // 鎵惧埌绗﹀悎鏉′欢鐨勮祫婧?
        var names = asm.GetManifestResourceNames();
        if (names == null || names.Length <= 0) return;
        IEnumerable<String> ns = null;
        if (prefix.IsNullOrWhiteSpace())
            ns = names.AsEnumerable();
        else
            ns = names.Where(e => e.StartsWithIgnoreCase(prefix));

        if (String.IsNullOrEmpty(dest)) dest = ".".GetFullPath();
        dest = dest.GetFullPath();

        // 寮€濮嬪鐞?
        foreach (var item in ns)
        {
            var stream = asm.GetManifestResourceStream(item);

            // 璁＄畻filename
            String filename = null;
            // 鍘绘帀鍓嶇紑
            if (filenameResolver != null) filename = filenameResolver(item);

            if (String.IsNullOrEmpty(filename))
            {
                filename = item;
                if (!String.IsNullOrEmpty(prefix)) filename = filename[prefix.Length..];
                if (filename[0] == '.') filename = filename[1..];

                var ext = Path.GetExtension(item);
                filename = filename[..^ext.Length];
                filename = filename.Replace(".", @"\") + ext;
                filename = Path.Combine(dest, filename);
            }

            if (File.Exists(filename) && !overWrite) return;

            //var path = Path.GetDirectoryName(filename);
            //if (!path.IsNullOrWhiteSpace() && !Directory.Exists(path)) Directory.CreateDirectory(path);
            filename.EnsureDirectory(true);
            try
            {
                if (File.Exists(filename)) File.Delete(filename);

                using var fs = File.Create(filename);
                //IOHelper.CopyTo(stream, fs);
                stream.CopyTo(fs);
            }
            catch { }
            finally { stream.Dispose(); }
        }
    }

    /// <summary>鑾峰彇鏂囦欢璧勬簮</summary>
    /// <param name="asm"></param>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static Stream GetFileResource(this Assembly asm, String filename)
    {
        if (String.IsNullOrEmpty(filename)) return null;

        var name = String.Empty;
        if (asm == null) asm = Assembly.GetCallingAssembly();
        var ss = asm.GetManifestResourceNames();
        if (ss != null && ss.Length > 0)
        {
            //鎵惧埌璧勬簮鍚?
            name = ss.FirstOrDefault(e => e == filename);
            if (String.IsNullOrEmpty(name)) name = ss.FirstOrDefault(e => e.EqualIgnoreCase(filename));
            if (String.IsNullOrEmpty(name)) name = ss.FirstOrDefault(e => e.EndsWith(filename));

            if (!String.IsNullOrEmpty(name)) return asm.GetManifestResourceStream(name);
        }
        return null;
    }
}
