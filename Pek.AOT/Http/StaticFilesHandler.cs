using System.Net;

using Pek.Extension;
using Pek.IO;

namespace Pek.Http;

/// <summary>静态文件处理器</summary>
public class StaticFilesHandler : IHttpHandler
{
    /// <summary>映射路径</summary>
    public String Path { get; set; } = null!;

    /// <summary>内容目录</summary>
    public String ContentPath { get; set; } = null!;

    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    public virtual void ProcessRequest(IHttpContext context)
    {
        if (!context.Path.StartsWithIgnoreCase(Path))
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            context.Response.StatusDescription = $"File {context.Path} not found";
            return;
        }

        var file = context.Path[Path.Length..];
        file = ContentPath.CombinePath(file);

        if (!file.GetFullPath().StartsWithIgnoreCase(ContentPath.GetFullPath()))
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            context.Response.StatusDescription = $"File {context.Path} not found";
            return;
        }

        var fileInfo = file.AsFile();
        if (!fileInfo.Exists)
        {
            context.Response.StatusCode = HttpStatusCode.NotFound;
            context.Response.StatusDescription = $"File {context.Path} not found";
            return;
        }

        var contentType = GetContentType(fileInfo.Extension);
        using var stream = fileInfo.OpenRead();
        context.Response.SetResult(stream, contentType);
    }

    /// <summary>根据文件扩展名获取MIME类型</summary>
    /// <param name="extension">扩展名</param>
    /// <returns>内容类型</returns>
    protected virtual String? GetContentType(String extension) => extension switch
    {
        ".htm" or ".html" => "text/html",
        ".txt" or ".log" => "text/plain",
        ".xml" => "text/xml",
        ".css" => "text/css",
        ".csv" => "text/csv",
        ".js" => "text/javascript",
        ".json" or ".map" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".ico" => "image/x-icon",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".eot" => "application/vnd.ms-fontobject",
        ".zip" => "application/zip",
        ".pdf" => "application/pdf",
        _ => null,
    };
}