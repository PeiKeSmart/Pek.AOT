using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Pek.Extension;

namespace Pek.IO;

/// <summary>鏂囦欢鎿嶄綔杈呭姪绫伙紙AOT瀹夊叏锛?/summary>
public static partial class FileUtil
{
    #region CreateIfNotExists(鍒涘缓鏂囦欢锛屽鏋滄枃浠朵笉瀛樺湪)

    /// <summary>鍒涘缓鏂囦欢锛屽鏋滄枃浠朵笉瀛樺湪</summary>
    /// <param name="fileName">鏂囦欢鍚嶏紝缁濆璺緞</param>
    public static void CreateIfNotExists(String fileName)
    {
        if (File.Exists(fileName))
            return;

        File.Create(fileName);
    }

    #endregion

    #region Delete(鍒犻櫎鏂囦欢)

    /// <summary>鍒犻櫎鏂囦欢</summary>
    /// <param name="filePaths">鏂囦欢闆嗗悎鐨勭粷瀵硅矾寰?/param>
    public static void Delete(IEnumerable<String> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            Delete(filePath);
        }
    }

    /// <summary>鍒犻櫎鏂囦欢</summary>
    /// <param name="filePath">鏂囦欢鐨勭粷瀵硅矾寰?/param>
    public static void Delete(String filePath)
    {
        if (String.IsNullOrWhiteSpace(filePath))
            return;

        if (!File.Exists(filePath))
            return;

        // 璁剧疆鏂囦欢鐨勫睘鎬т负姝ｅ父锛堝鏋滄枃浠朵负鍙鐨勮瘽鐩存帴鍒犻櫎浼氭姤閿欙級
        File.SetAttributes(filePath, FileAttributes.Normal);
        File.Delete(filePath);
    }

    #endregion

    #region KillFile(寮哄姏绮夌鏂囦欢)

    /// <summary>寮哄姏绮夌鏂囦欢锛屽鏋滄枃浠惰鎵撳紑锛屽緢闅剧矇纰?/summary>
    /// <param name="fileName">鏂囦欢鍏ㄨ矾寰?/param>
    /// <param name="deleteCount">鍒犻櫎娆℃暟</param>
    /// <param name="randomData">闅忔満鏁版嵁濉厖鏂囦欢锛岄粯璁rue</param>
    /// <param name="blanks">绌虹櫧濉厖鏂囦欢锛岄粯璁alse</param>
    /// <returns>true:绮夌鎴愬姛,false:绮夌澶辫触</returns>
    public static Boolean KillFile(String fileName, Int32 deleteCount, Boolean randomData = true, Boolean blanks = false)
    {
        const Int32 bufferLength = 1024000;
        var ret = true;
        try
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var file = new FileInfo(fileName);
            var count = file.Length;
            Int64 offset = 0;
            var rowDataBuffer = new Byte[bufferLength];
            while (count >= 0)
            {
                var iNumOfDataRead = stream.Read(rowDataBuffer, 0, bufferLength);
                if (iNumOfDataRead == 0)
                    break;

                if (randomData)
                {
                    var randomByte = new Random();
                    randomByte.NextBytes(rowDataBuffer);
                }
                else if (blanks)
                {
                    for (var i = 0; i < iNumOfDataRead; i++)
                    {
                        rowDataBuffer[i] = Convert.ToByte(Convert.ToChar(deleteCount));
                    }
                }

                // 鍐欐柊鍐呭鍒版枃浠?
                for (var i = 0; i < deleteCount; i++)
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.Write(rowDataBuffer, 0, iNumOfDataRead);
                }

                offset += iNumOfDataRead;
                count -= iNumOfDataRead;
            }

            // 姣忎竴涓枃浠跺悕瀛楃浠ｆ浛闅忔満鏁颁粠0鍒?
            var newName = "";
            do
            {
                var random = new Random();
                var cleanName = Path.GetFileName(fileName);
                var dirName = Path.GetDirectoryName(fileName);
                var iMoreRandomLetters = random.Next(9);
                // 涓轰簡鏇村畨鍏紝涓嶈鍙娇鐢ㄥ師鏂囦欢鍚嶇殑澶у皬锛屾坊鍔犱竴浜涢殢鏈哄瓧姣?
                for (var i = 0; i < cleanName.Length + iMoreRandomLetters; i++)
                {
                    newName += random.Next(9).ToString();
                }

                newName = dirName + "\\" + newName;
            } while (File.Exists(newName));

            // 閲嶅懡鍚嶆枃浠剁殑鏂伴殢鏈虹殑鍚嶅瓧
            File.Move(fileName, newName);
            File.Delete(newName);
        }
        catch
        {
            // 鍙兘鍏朵粬鍘熷洜鍒犻櫎澶辫触锛屼娇鐢ㄦ垜浠嚜宸辩殑鏂规硶寮哄埗鍒犻櫎
            try
            {
                var filename = fileName; // 瑕佹鏌ヨ鍝釜杩涚▼鍗犵敤鐨勬枃浠?
                var tool = new Process()
                {
                    StartInfo =
                    {
                        FileName = "handle.exe",
                        Arguments = filename + " /accepteula",
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    }
                };
                tool.Start();
                tool.WaitForExit();
                var outputTool = tool.StandardOutput.ReadToEnd();
                var matchPattern = @"(?<=\s+pid:\s+)\b(\d+)\b(?=\s+)";
                foreach (Match match in Regex.Matches(outputTool, matchPattern))
                {
                    // 缁撴潫鎺夋墍鏈夋鍦ㄤ娇鐢ㄨ繖涓枃浠剁殑绋嬪簭
                    Process.GetProcessById(Int32.Parse(match.Value)).Kill();
                }

                File.Delete(filename);
            }
            catch
            {
                ret = false;
            }
        }

        return ret;
    }

    #endregion

    #region SetAttribute(璁剧疆鏂囦欢灞炴€?

    /// <summary>璁剧疆鏂囦欢灞炴€?/summary>
    /// <param name="fileName">鏂囦欢鍚?/param>
    /// <param name="attribute">鏂囦欢灞炴€?/param>
    /// <param name="isSet">鏄惁涓鸿缃睘鎬?true:璁剧疆,false:鍙栨秷</param>
    public static void SetAttribute(String fileName, FileAttributes attribute, Boolean isSet)
    {
        var fi = new FileInfo(fileName);
        if (!fi.Exists)
            throw new FileNotFoundException("要设置属性的文件不存在。", fileName);

        if (isSet)
            fi.Attributes |= attribute;
        else
            fi.Attributes &= ~attribute;
    }

    #endregion

    #region GetAllFiles(鑾峰彇鐩綍涓叏閮ㄦ枃浠跺垪琛?

    /// <summary>鑾峰彇鐩綍涓叏閮ㄦ枃浠跺垪琛紝鍖呮嫭瀛愮洰褰?/summary>
    /// <param name="directoryPath">鐩綍缁濆璺緞</param>
    /// <returns></returns>
    public static List<String> GetAllFiles(String directoryPath) => [.. Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)];

    #endregion

    #region Read(璇诲彇鏂囦欢鍒板瓧绗︿覆)

    /// <summary>璇诲彇鏂囦欢鍒板瓧绗︿覆</summary>
    /// <param name="filePath">鏂囦欢鐨勭粷瀵硅矾寰?/param>
    public static String Read(String filePath) => Read(filePath, Encoding.UTF8);

    /// <summary>璇诲彇鏂囦欢鍒板瓧绗︿覆</summary>
    /// <param name="filePath">鏂囦欢鐨勭粷瀵硅矾寰?/param>
    /// <param name="encoding">瀛楃缂栫爜</param>
    public static String Read(String filePath, Encoding encoding)
    {
        encoding ??= Encoding.UTF8;
        if (!File.Exists(filePath))
            return String.Empty;
        using var reader = new StreamReader(filePath, encoding);
        return reader.ReadToEnd();
    }

    #endregion

    #region ReadToBytes(灏嗘枃浠惰鍙栧埌瀛楄妭娴佷腑)

    /// <summary>灏嗘枃浠惰鍙栧埌瀛楄妭娴佷腑</summary>
    /// <param name="filePath">鏂囦欢鐨勭粷瀵硅矾寰?/param>
    /// <returns></returns>
    public static Byte[]? ReadToBytes(String filePath)
    {
        if (!File.Exists(filePath))
            return null;

        return ReadToBytes(new FileInfo(filePath));
    }

    /// <summary>灏嗘枃浠惰鍙栧埌瀛楄妭娴佷腑</summary>
    /// <param name="fileInfo">鏂囦欢淇℃伅</param>
    /// <returns></returns>
    public static Byte[]? ReadToBytes(FileInfo fileInfo)
    {
        if (fileInfo == null)
            return null;

        var fileSize = (Int32)fileInfo.Length;
        using var reader = new BinaryReader(fileInfo.Open(FileMode.Open));
        return reader.ReadBytes(fileSize);
    }

    #endregion

    #region Write(灏嗗瓧鑺傛祦鍐欏叆鏂囦欢)

    /// <summary>灏嗗瓧绗︿覆鍐欏叆鏂囦欢锛屾枃浠朵笉瀛樺湪鍒欏垱寤?/summary>
    /// <param name="filePath">鏂囦欢鐨勭粷瀵硅矾寰?/param>
    /// <param name="content">鏁版嵁</param>
    public static void Write(String filePath, String content) => Write(filePath, ToBytes(content.SafeString()));

    /// <summary>灏嗗瓧绗︿覆鍐欏叆鏂囦欢锛屾枃浠朵笉瀛樺湪鍒欏垱寤?/summary>
    /// <param name="filePath">鏂囦欢鐨勭粷瀵硅矾寰?/param>
    /// <param name="bytes">鏁版嵁</param>
    public static void Write(String filePath, Byte[] bytes)
    {
        if (String.IsNullOrWhiteSpace(filePath))
            return;
        if (bytes == null)
            return;
        File.WriteAllBytes(filePath, bytes);
    }

    #endregion

    #region JoinPath(杩炴帴鍩鸿矾寰勫拰瀛愯矾寰?

    /// <summary>杩炴帴鍩鸿矾寰勫拰瀛愯矾寰勶紝姣斿鎶?c: 涓?test.doc 杩炴帴鎴?c:\test.doc</summary>
    /// <param name="basePath">鍩鸿矾寰勶紝鑼冧緥锛歝:</param>
    /// <param name="subPath">瀛愯矾寰勶紝鍙互鏄枃浠跺悕锛岃寖渚嬶細test.doc</param>
    /// <returns></returns>
    public static String JoinPath(String basePath, String subPath)
    {
        basePath = basePath.TrimEnd('/').TrimEnd('\\');
        subPath = subPath.TrimStart('/').TrimStart('\\');
        var path = basePath + "\\" + subPath;
        return path.Replace("/", "\\").ToLower();
    }

    #endregion

    #region CopyToStringAsync(澶嶅埗娴佸苟杞崲鎴愬瓧绗︿覆)

    /// <summary>澶嶅埗娴佸苟杞崲鎴愬瓧绗︿覆</summary>
    /// <param name="stream">娴?/param>
    /// <param name="encoding">瀛楃缂栫爜</param>
    public static async Task<String> CopyToStringAsync(Stream? stream, Encoding? encoding = null)
    {
        if (stream == null)
            return String.Empty;

        encoding ??= Encoding.UTF8;

        if (stream.CanRead == false)
            return String.Empty;

        using var memoryStream = new MemoryStream();
        using var reader = new StreamReader(memoryStream, encoding);
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        stream.CopyTo(memoryStream);
        if (memoryStream.CanSeek)
            memoryStream.Seek(0, SeekOrigin.Begin);

        var result = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        return result;
    }

    #endregion

    #region Combine(鍚堝苟鏂囦欢)

    /// <summary>鍚堝苟鏂囦欢</summary>
    /// <param name="files">鏂囦欢璺緞鍒楄〃</param>
    /// <param name="fileName">鐢熸垚鏂囦欢鍚?/param>
    /// <param name="delete">鍚堝苟鍚庢槸鍚﹀垹闄ゆ簮鏂囦欢</param>
    public static void Combine(IList<String> files, String fileName, Boolean delete = false)
    {
        if (files == null || files.Count == 0)
            return;

        files.Sort();
        using var ws = new FileStream(fileName, FileMode.Create);
        foreach (var file in files)
        {
            if (file == null || !File.Exists(file))
                continue;

            using (var rs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                var data = new Byte[1024];
                var readLen = 0;
                while ((readLen = rs.Read(data, 0, data.Length)) > 0)
                {
                    ws.Write(data, 0, readLen);
                    ws.Flush();
                }
            }
            if (delete)
                Delete(file);
        }
    }

    #endregion

    #region Split(鍒嗗壊鏂囦欢)

    /// <summary>鍒嗗壊鏂囦欢</summary>
    /// <param name="file">鏂囦欢</param>
    /// <param name="dirPath">鐢熸垚鏂囦欢璺緞銆備笉鍚枃浠跺悕</param>
    /// <param name="suffix">鍚庣紑鍚?/param>
    /// <param name="size">鍒嗗壊澶у皬銆傚崟浣嶏細KB</param>
    /// <param name="delete">鍒嗗壊鍚庢槸鍚﹀垹闄ゆ簮鏂囦欢</param>
    public static void Split(String file, String dirPath, String suffix = "bin", Int32 size = 2048, Boolean delete = false)
    {
        if (String.IsNullOrWhiteSpace(file) || !File.Exists(file))
            return;

        var fileName = Path.GetFileNameWithoutExtension(file);
        var fileSize = GetFileSize(file);
        var total = GetSplitFileTotal(fileSize.GetSize(), size);
        using var rs = new FileStream(file, FileMode.Open, FileAccess.Read);
        var data = new Byte[1024];
        Int32 len = 0, i = 1;
        var readLen = 0;
        FileStream? ws = null;
        while (readLen > 0 || (readLen = rs.Read(data, 0, data.Length)) > 0)
        {
            if (len == 0 || ws == null)
            {
                ws?.Dispose();
                ws = new FileStream($"{dirPath}\\{fileName}.{i++}.{total}.{suffix}", FileMode.Create);
            }

            // 杈撳嚭锛岀紦瀛樻暟鎹啓鍏ュ瓙鏂囦欢
            ws.Write(data, 0, readLen);
            ws.Flush();
            // 棰勮涓嬩竴杞紦瀛樻暟鎹?
            readLen = rs.Read(data, 0, data.Length);
            // 瀛愭枃浠惰揪鍒版寚瀹氬ぇ灏忔垨鑰呮枃浠跺凡璇诲畬
            if (++len >= size || readLen == 0)
            {
                ws.Close();
                len = 0;
            }
        }

        if (delete)
            Delete(file);
    }

    /// <summary>鑾峰彇鍒嗗壊鏂囦欢鏁伴噺</summary>
    /// <param name="fileSize">鏂囦欢澶у皬</param>
    /// <param name="splitSize">鍒嗗壊澶у皬銆傚崟浣嶏細瀛楄妭</param>
    /// <returns></returns>
    private static Int32 GetSplitFileTotal(Int32 fileSize, Int32 splitSize)
    {
        fileSize /= 1024;
        if (fileSize % splitSize == 0)
            return fileSize / splitSize;

        return fileSize / splitSize + 1;
    }

    #endregion

    #region Compress(鍘嬬缉)

    /// <summary>鍘嬬缉</summary>
    /// <param name="file">鏂囦欢</param>
    /// <param name="saveFile">淇濆瓨鏂囦欢</param>
    /// <returns></returns>
    public static Boolean Compress(String file, String saveFile)
    {
        if (String.IsNullOrWhiteSpace(file) || String.IsNullOrWhiteSpace(saveFile))
            return false;

        if (!File.Exists(file))
            return false;

        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            using var ws = new FileStream(saveFile, FileMode.Create);
            using var zip = new GZipStream(ws, CompressionMode.Compress);
            fs.CopyTo(zip);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Decompress(瑙ｅ帇缂?

    /// <summary>瑙ｅ帇缂?/summary>
    /// <param name="file">鏂囦欢</param>
    /// <param name="saveFile">淇濆瓨鏂囦欢</param>
    /// <returns></returns>
    public static Boolean Decompress(String file, String saveFile)
    {
        if (String.IsNullOrWhiteSpace(file))
            return false;

        if (String.IsNullOrWhiteSpace(saveFile))
            return false;

        if (!File.Exists(file))
            return false;

        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            using var ws = new FileStream(saveFile, FileMode.Create);
            using var zip = new GZipStream(fs, CompressionMode.Decompress);
            zip.CopyTo(ws);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region CompressMulti(澶氭枃浠跺帇缂?

    /// <summary>澶氭枃浠跺帇缂┿€傦紙鐢熸垚鐨勫帇缂╁寘鍜岀涓夋柟鐨勫帇缂╂枃浠惰В鍘嬩笉鍏煎锛?/summary>
    /// <param name="sourceFileList">鏂囦欢鍒楄〃</param>
    /// <param name="saveFullPath">鍘嬬缉鍖呭叏璺緞</param>
    public static void CompressMulti(String[] sourceFileList, String saveFullPath)
    {
        if (sourceFileList == null || sourceFileList.Length == 0 || String.IsNullOrWhiteSpace(saveFullPath))
            return;

        using var ms = new MemoryStream();
        foreach (var filePath in sourceFileList)
        {
            if (!File.Exists(filePath))
                continue;

            var fileName = Path.GetFileName(filePath);
            var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
            var sizeBytes = BitConverter.GetBytes(fileNameBytes.Length);
            ms.Write(sizeBytes, 0, sizeBytes.Length);
            ms.Write(fileNameBytes, 0, fileNameBytes.Length);
            var fileContentBytes = File.ReadAllBytes(filePath);
            ms.Write(BitConverter.GetBytes(fileContentBytes.Length), 0, 4);
            ms.Write(fileContentBytes, 0, fileContentBytes.Length);
        }

        ms.Flush();
        ms.Position = 0;

        using var fs = File.Create(saveFullPath);
        using var zipStream = new GZipStream(fs, CompressionMode.Compress);
        ms.Position = 0;
        ms.CopyTo(zipStream);
    }

    #endregion

    #region DecompressMulti(澶氭枃浠惰В鍘嬬缉)

    /// <summary>澶氭枃浠惰В鍘嬬缉</summary>
    /// <param name="zipPath">鍘嬬缉鏂囦欢璺緞</param>
    /// <param name="targetPath">瑙ｅ帇鐩綍</param>
    public static void DecompressMulti(String zipPath, String targetPath)
    {
        if (String.IsNullOrWhiteSpace(zipPath) || String.IsNullOrWhiteSpace(targetPath))
            return;

        var fileSize = new Byte[4];
        if (!File.Exists(zipPath))
            return;

        using var fs = File.Open(zipPath, FileMode.Open);
        using var ms = new MemoryStream();
        using (var zipStream = new GZipStream(fs, CompressionMode.Decompress))
        {
            zipStream.CopyTo(ms);
        }

        ms.Position = 0;
        while (ms.Position != ms.Length)
        {
            ms.Read(fileSize, 0, fileSize.Length);
            var fileNameLength = BitConverter.ToInt32(fileSize, 0);
            var fileNameBytes = new Byte[fileNameLength];
            ms.Read(fileNameBytes, 0, fileNameBytes.Length);
            var fileName = Encoding.UTF8.GetString(fileNameBytes);
            var fileFullName = targetPath + fileName;
            ms.Read(fileSize, 0, 4);
            var fileContentLength = BitConverter.ToInt32(fileSize, 0);
            var fileContentBytes = new Byte[fileContentLength];
            ms.Read(fileContentBytes, 0, fileContentBytes.Length);
            using var childFileStream = File.Create(fileFullName);
            childFileStream.Write(fileContentBytes, 0, fileContentBytes.Length);
        }
    }

    #endregion

    /// <summary>灏嗚矾寰勮浆鎹负鏂囦欢璺緞鏍煎紡</summary>
    /// <param name="path">璺緞</param>
    /// <returns></returns>
    public static String ToFilePath(this String path) => Path.Combine(path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));

    #region 涓婁紶閰嶇疆

    /// <summary>鏍规嵁鏂囦欢绫诲瀷鍒嗛厤璺緞</summary>
    /// <param name="fileExt">鏂囦欢鎵╁睍鍚?/param>
    /// <param name="path">鍩虹璺緞</param>
    /// <returns></returns>
    public static String AssigendPath(String fileExt, String path)
    {
        if (IsImage(fileExt))
            return path + "/upload/images/" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + "/";
        if (IsVideos(fileExt))
            return path + "/upload/videos/" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + "/";
        if (IsDocument(fileExt))
            return "/upload/files/" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + "/";
        if (IsMusics(fileExt))
            return "/upload/musics/" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + "/";
        return path + "/upload/others/";
    }

    #endregion

    #region 鏂囦欢鏍煎紡

    /// <summary>鏄惁涓哄浘鐗?/summary>
    /// <param name="_fileExt">鏂囦欢鎵╁睍鍚嶏紝涓嶅惈"."</param>
    /// <returns></returns>
    private static Boolean IsImage(String _fileExt)
    {
        var images = new List<String> { "bmp", "gif", "jpg", "jpeg", "png" };
        if (images.Contains(_fileExt.ToLower())) return true;
        return false;
    }

    /// <summary>鏄惁涓鸿棰?/summary>
    /// <param name="_fileExt">鏂囦欢鎵╁睍鍚嶏紝涓嶅惈"."</param>
    /// <returns></returns>
    private static Boolean IsVideos(String _fileExt)
    {
        var videos = new List<String> { "rmvb", "mkv", "ts", "wma", "avi", "rm", "mp4", "flv", "mpeg", "mov", "3gp", "mpg" };
        if (videos.Contains(_fileExt.ToLower())) return true;
        return false;
    }

    /// <summary>鏄惁涓洪煶棰?/summary>
    /// <param name="_fileExt">鏂囦欢鎵╁睍鍚嶏紝涓嶅惈"."</param>
    /// <returns></returns>
    private static Boolean IsMusics(String _fileExt)
    {
        var musics = new List<String> { "mp3", "wav" };
        if (musics.Contains(_fileExt.ToLower())) return true;
        return false;
    }

    /// <summary>鏄惁涓烘枃妗?/summary>
    /// <param name="_fileExt">鏂囦欢鎵╁睍鍚嶏紝涓嶅惈"."</param>
    /// <returns></returns>
    private static Boolean IsDocument(String _fileExt)
    {
        var documents = new List<String> { "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "pdf" };
        if (documents.Contains(_fileExt.ToLower())) return true;
        return false;
    }

    #endregion

    /// <summary>鏂囦欢鍐欏叆绫诲瀷</summary>
    public enum WriteType
    {
        /// <summary>杩藉姞</summary>
        Append = 1,
        /// <summary>瑕嗙洊</summary>
        Covered = 2
    }

    /// <summary>MD5璁＄畻鏁版嵁娴?/summary>
    /// <param name="stream">娴?/param>
    /// <returns></returns>
    public static String MD5Stream(Stream stream)
    {
        using var md5 = MD5.Create();
        md5.ComputeHash(stream);

        var b = md5.Hash;
        md5.Clear();

        var sb = new StringBuilder(32);
        for (var i = 0; i < b?.Length; i++)
        {
            sb.Append(b[i].ToString("X2"));
        }
        return sb.ToString();
    }
}
