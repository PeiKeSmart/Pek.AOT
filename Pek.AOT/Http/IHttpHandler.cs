namespace Pek.Http;

/// <summary>Http处理器接口</summary>
public interface IHttpHandler
{
    /// <summary>处理请求</summary>
    /// <param name="context">Http上下文</param>
    void ProcessRequest(IHttpContext context);
}

/// <summary>Http请求处理委托</summary>
/// <param name="context">Http上下文</param>
public delegate void HttpProcessDelegate(IHttpContext context);