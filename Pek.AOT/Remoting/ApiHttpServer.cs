using Pek.Http;
using Pek.Net;

namespace Pek.Remoting;

class ApiHttpServer : ApiNetServer
{
    public ApiHttpServer()
    {
        Name = "Http";
        ProtocolType = NetType.Http;
    }

    /// <summary>初始化</summary>
    /// <param name="config">配置</param>
    /// <param name="host">主机</param>
    /// <returns>是否成功</returns>
    public override Boolean Init(Object config, IApiHost host)
    {
        if (config is not NetUri uri) throw new ArgumentNullException(nameof(config));

        Host = host;
        Port = uri.Port;

        Add(new HttpCodec { AllowParseHeader = true });
        host.Encoder = new HttpEncoder();

        return true;
    }
}