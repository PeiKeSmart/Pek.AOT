using System.Collections.Generic;
using System.Net;

namespace System;

/// <summary>网络结点扩展</summary>
public static class EndPointExtensions
{
    /// <summary>把网络结点转为地址文本</summary>
    /// <param name="endpoint">网络结点</param>
    /// <returns>地址文本</returns>
    public static String ToAddress(this EndPoint endpoint)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

        return ((IPEndPoint)endpoint).ToAddress();
    }

    /// <summary>把网络结点转为地址文本</summary>
    /// <param name="endpoint">网络结点</param>
    /// <returns>地址文本</returns>
    public static String ToAddress(this IPEndPoint endpoint)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

        return String.Format("{0}:{1}", endpoint.Address, endpoint.Port);
    }

    /// <summary>把地址文本转为网络结点</summary>
    /// <param name="address">地址文本</param>
    /// <returns>网络结点</returns>
    public static IPEndPoint ToEndPoint(this String address)
    {
        if (String.IsNullOrWhiteSpace(address)) throw new ArgumentNullException(nameof(address));

        var array = address.Split([":"], StringSplitOptions.RemoveEmptyEntries);
        if (array.Length != 2) throw new Exception("Invalid endpoint address: " + address);

        var ip = IPAddress.Parse(array[0]);
        var port = Int32.Parse(array[1]);
        return new IPEndPoint(ip, port);
    }

    /// <summary>把地址文本集合转为网络结点集合</summary>
    /// <param name="addresses">地址文本集合</param>
    /// <returns>网络结点集合</returns>
    public static IEnumerable<IPEndPoint> ToEndPoints(this String addresses)
    {
        if (String.IsNullOrWhiteSpace(addresses)) throw new ArgumentNullException(nameof(addresses));

        var array = addresses.Split([","], StringSplitOptions.RemoveEmptyEntries);
        var list = new List<IPEndPoint>();
        foreach (var item in array)
        {
            list.Add(item.ToEndPoint());
        }

        return list;
    }
}