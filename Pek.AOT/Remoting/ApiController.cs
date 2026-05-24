using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;

using Pek;
using Pek.Collections;
using Pek.Extension;
using Pek.Http;
using Pek.Net;

using NewLife;
using NewLife.Reflection;

namespace Pek.Remoting;

/// <summary>API控制器</summary>
public class ApiController : IApi
{
    /// <summary>主机</summary>
    public IApiHost Host { get; set; } = null!;

    /// <summary>会话</summary>
    public IApiSession Session { get; set; } = null!;

    private String[]? _all;

    /// <summary>获取所有接口</summary>
    /// <returns>接口列表</returns>
    public String[] All()
    {
        if (_all != null) return _all;
        if (Host is not ApiServer server) return _all = [];

        var list = new List<String>();
        foreach (var item in server.Manager.Services)
        {
            var action = item.Value;
            var method = action.Method;

            var builder = Pool.StringBuilder.Get();
            builder.AppendFormat("{0} {1}", method.ReturnType.Name, action.Name);
            builder.Append('(');

            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.AppendFormat("{0} {1}", parameters[i].ParameterType.Name, parameters[i].Name);
            }

            builder.Append(')');

            var description = method.GetDescription();
            if (!String.IsNullOrWhiteSpace(description)) builder.AppendFormat(" {0}", description);

            list.Add(builder.Return(true) ?? String.Empty);
        }

        return _all = [.. list];
    }

    private static readonly Int32 _pid = Process.GetCurrentProcess().Id;
    private static readonly String _machineName = Environment.MachineName;
    private static readonly String _localIP = NetHelper.GetIPs().Where(static item => item.AddressFamily == AddressFamily.InterNetwork).Join();

    /// <summary>服务器信息，用户健康检测</summary>
    /// <param name="state">状态信息</param>
    /// <returns>服务器信息</returns>
    public Object Info(String state)
    {
        IDictionary<String, Object?>? parameters = ControllerContext.Current?.Parameters;
        var netSession = ControllerContext.Current?.Session as INetSession;
        if (netSession == null && DefaultHttpContext.Current is IHttpContext http)
        {
            parameters = http.Parameters;
            netSession = http.Connection;
        }

        var entryAssembly = AssemblyX.Entry;
        var apiAssembly = AssemblyX.Create(Assembly.GetExecutingAssembly());
        var machineInfo = MachineInfo.Current;

        var result = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = _pid,
            ["Name"] = entryAssembly?.Name,
            ["Title"] = entryAssembly?.Title,
            ["FileVersion"] = entryAssembly?.FileVersion,
            ["Compile"] = entryAssembly?.Compile,
            ["OS"] = machineInfo?.OSName,
            ["MachineName"] = _machineName,
            ["ApiVersion"] = apiAssembly?.FileVersion,
            ["LocalIP"] = _localIP,
            ["Remote"] = netSession?.Remote?.EndPoint + String.Empty,
            ["State"] = state,
            ["Time"] = DateTime.Now,
        };

        if (Session != null) Session["State"] = state;

        if (Session != null && !String.IsNullOrEmpty(Session.Token))
        {
            result["Token"] = Session.Token;

            if (Host is ApiHost apiHost) result["Uptime"] = (DateTime.Now - apiHost.StartTime).ToString();
            if (Host is ApiServer server && server.Server is NetServer netServer)
            {
                result["Port"] = netServer.Port;
                result["Online"] = netServer.SessionCount;
                result["MaxOnline"] = netServer.MaxSessionCount;
            }

            result["Stat"] = GetStat();
        }
        else if (parameters != null && parameters.TryGetValue("Token", out var token) && !String.IsNullOrEmpty(token + String.Empty))
        {
            result["Token"] = token;
        }

        return result;
    }

    private Object? GetStat()
    {
        if (Host is not ApiServer server) return null;

        var result = new Dictionary<String, Object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["_Total"] = server.StatProcess + String.Empty,
        };
        foreach (var item in server.Manager.Services)
        {
            var action = item.Value;
            result[item.Key] = action.StatProcess + " " + action.LastSession;
        }

        return result;
    }
}