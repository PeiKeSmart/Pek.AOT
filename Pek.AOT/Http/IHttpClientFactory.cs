namespace Pek.Http;

/// <summary>HttpClient工厂</summary>
public interface IHttpClientFactory
{
    /// <summary>创建HttpClient</summary>
    /// <param name="name">名称</param>
    /// <returns>HttpClient</returns>
    HttpClient CreateClient(String name);
}