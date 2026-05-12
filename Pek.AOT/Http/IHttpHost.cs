namespace Pek.Http;

/// <summary>Http主机服务接口</summary>
public interface IHttpHost
{
    /// <summary>匹配处理器</summary>
    /// <param name="path">请求路径</param>
    /// <param name="request">Http请求</param>
    /// <returns>处理器</returns>
    IHttpHandler? MatchHandler(String path, HttpRequest? request);
}