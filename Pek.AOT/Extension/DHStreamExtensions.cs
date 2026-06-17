namespace System.IO;

/// <summary>
/// Stream扩展方法
/// </summary>
public static class DHStreamExtensions
{
    /// <summary>
    /// 获取流的所有字节
    /// </summary>
    /// <param name="stream">流</param>
    /// <returns>字节数组</returns>
    public static Byte[] GetAllBytes(this Stream stream)
    {
        using var memoryStream = new MemoryStream();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 异步获取流的所有字节
    /// </summary>
    /// <param name="stream">流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>字节数组</returns>
    public static async Task<Byte[]> GetAllBytesAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// 异步复制流到目标流
    /// </summary>
    /// <param name="stream">源流</param>
    /// <param name="destination">目标流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static Task CopyToAsync(this Stream stream, Stream destination, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        return stream.CopyToAsync(
            destination,
            81920, // 这已经是默认值，但需要设置才能传递cancellationToken
            cancellationToken
        );
    }
}
