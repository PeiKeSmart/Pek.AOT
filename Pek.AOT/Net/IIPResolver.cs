using System.Net;

namespace Pek.Net;

/// <summary>IP地址提供者</summary>
public interface IIPResolver
{
    /// <summary>获取IP地址的物理地址位置</summary>
    /// <param name="addr">IP地址</param>
    /// <returns>物理地址位置</returns>
    String GetAddress(IPAddress addr);
}