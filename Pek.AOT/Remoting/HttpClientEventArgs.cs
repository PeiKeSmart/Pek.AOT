using BclHttpClient = System.Net.Http.HttpClient;

namespace Pek.Remoting;

/// <summary>Http客户端事件参数</summary>
public class HttpClientEventArgs : EventArgs
{
    /// <summary>客户端</summary>
    public BclHttpClient Client { get; set; } = null!;
}