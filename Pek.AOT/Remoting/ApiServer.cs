using Pek.Collections;
using Pek.Data;
using Pek.Extension;
using Pek.Http;
using Pek.Log;
using Pek.Messaging;
using Pek.Model;
using Pek.Net;
using Pek.Threading;

using NewLife;

namespace Pek.Remoting;

/// <summary>应用接口服务器</summary>
public class ApiServer : ApiHost, IServer
{
    private TimerX? _timer;
    private String? _last;

    #region 属性
    /// <summary>是否正在工作</summary>
    public Boolean Active { get; private set; }

    /// <summary>端口</summary>
    public Int32 Port { get; set; }

    /// <summary>处理器</summary>
    public IApiHandler? Handler { get; set; }

    /// <summary>服务器</summary>
    public IApiServer? Server { get; set; }

    /// <summary>连接复用。默认true，单个Tcp连接在处理某个请求未完成时，可以接收并处理新的请求</summary>
    public Boolean Multiplex { get; set; } = true;

    /// <summary>是否使用Http状态。默认false，使用json包装响应码</summary>
    public Boolean UseHttpStatus { get; set; }

    /// <summary>服务提供者。创建控制器实例时使用，可实现依赖注入。务必在注册控制器之前设置该属性</summary>
    public IServiceProvider ServiceProvider { get; set; } = ObjectContainer.Provider ?? throw new InvalidOperationException("ObjectContainer.Provider is null.");

    /// <summary>处理统计</summary>
    public ICounter? StatProcess { get; set; }

    /// <summary>性能跟踪器</summary>
    public ITracer Tracer { get; set; } = DefaultTracer.Instance ?? throw new InvalidOperationException("DefaultTracer.Instance is null.");

    /// <summary>接口动作管理器</summary>
    public IApiManager Manager { get; }

    /// <summary>显示统计信息的周期。默认600秒，0表示不显示统计信息</summary>
    public Int32 StatPeriod { get; set; } = 600;
    #endregion

    #region 构造
    /// <summary>实例化一个应用接口服务器</summary>
    public ApiServer()
    {
        var type = GetType();
        Name = type.GetDisplayName() ?? (type.Name.EndsWith("Server", StringComparison.OrdinalIgnoreCase) ? type.Name[..^6] : type.Name);

        Manager = new ApiManager();

        Register<ApiController>();
    }

    /// <summary>使用指定端口实例化网络服务应用接口提供者</summary>
    /// <param name="port">端口</param>
    public ApiServer(Int32 port) : this() => Port = port;

    /// <summary>实例化</summary>
    /// <param name="uri">网络地址</param>
    public ApiServer(NetUri uri) : this() => Use(uri);

    /// <summary>销毁时停止服务</summary>
    /// <param name="disposing">是否显式释放</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _timer.TryDispose();
        Stop(GetType().Name + (disposing ? "Dispose" : "GC"));
    }
    #endregion

    #region 控制器管理
    /// <summary>注册服务提供类。该类的所有公开方法将直接暴露</summary>
    /// <typeparam name="TService">服务类型</typeparam>
    public void Register<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TService>() => Manager.Register<TService>();

    /// <summary>注册服务</summary>
    /// <param name="controller">控制器对象</param>
    /// <param name="method">动作名称。为空时遍历控制器所有公有成员方法</param>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Registering arbitrary controller instances relies on runtime method discovery. Prefer Register<TService>().")]
    public void Register(Object controller, String? method) => Manager.Register(controller, method);

    /// <summary>显示可用服务</summary>
    protected virtual void ShowService()
    {
        var services = Manager.Services;
        if (services.Count <= 0) return;

        Log.Info("可用服务{0}个：", services.Count);
        var max = services.Max(item => item.Key.Length);
        foreach (var item in services)
        {
            Log.Info("\t{0,-" + (max + 1) + "}{1}\t{2}", item.Key, item.Value, item.Value.Type.FullName);
        }
    }
    #endregion

    #region 启动停止
    /// <summary>添加服务器</summary>
    /// <param name="uri">网络地址</param>
    /// <returns>服务器实例</returns>
    public IApiServer? Use(NetUri uri)
    {
        var server = uri.Type == NetType.Http ? new ApiHttpServer() : new ApiNetServer();
        if (!server.Init(uri, this)) return null;

        Server = server;
        return server;
    }

    /// <summary>确保已创建服务器对象</summary>
    /// <returns>服务器实例</returns>
    public IApiServer EnsureCreate()
    {
        var server = Server;
        if (server != null) return server;

        if (Port <= 0) throw new ArgumentNullException(nameof(Server), "未指定服务器Server，且未指定端口Port！");

        server = new ApiNetServer { Host = this, Tracer = Tracer };
        server.Init(new NetUri(NetType.Unknown, "*", Port), this);

        return Server = server;
    }

    /// <summary>开始服务</summary>
    public virtual void Start()
    {
        if (Active) return;

        Encoder ??= new JsonEncoder();
        Handler ??= new ApiHandler();
        if (Handler is ApiHandler apiHandler) apiHandler.Host = this;

        Encoder.Log = EncoderLog;

        Log.Info("启动{0}，服务器 {1}", GetType().Name, Server);
        Log.Info("编码：{0}", Encoder);
        Log.Info("处理：{0}", Handler);

        var server = EnsureCreate();
        server.Host = this;
        server.Log = Log;
        server.Start();

        ShowService();

        var period = StatPeriod * 1000;
        if (period > 0)
        {
            StatProcess ??= new PerfCounter();
            _timer = new TimerX(DoStat, null, period, period) { Async = true };
        }

        Active = true;
    }

    /// <summary>停止服务</summary>
    /// <param name="reason">关闭原因。便于日志分析</param>
    public virtual void Stop(String? reason)
    {
        if (!Active) return;

        Log.Info("停止{0} {1}", GetType().Name, reason);
        Server?.Stop(reason ?? (GetType().Name + "Stop"));

        Active = false;
    }
    #endregion

    #region 请求处理
    /// <summary>处理会话收到的消息，并返回结果消息</summary>
    /// <remarks>这里是网络RPC的消息处理核心，目标协议只要能封装为IMessage，即可通过重载该方法得到支持。</remarks>
    /// <param name="session">网络会话</param>
    /// <param name="msg">消息</param>
    /// <returns>要应答对方的消息，为空表示不应答</returns>
    internal protected virtual IMessage? Process(IApiSession session, IMessage msg)
    {
        if (msg.Reply) return null;

        var action = String.Empty;
        var code = 0;

        ISpan? span = null;
        var counter = StatProcess;
        var startTicks = counter.StartCount();
        try
        {
            var encoder = session["Encoder"] as IEncoder ?? Encoder;

            Object? result;
            IPacket? args = null;
            try
            {
                if (!encoder.Decode(msg, out action, out _, out args)) return null;

                span = Tracer?.NewSpan("rps:" + action, args);
                result = OnProcess(session, action, args, msg);
            }
            catch (Exception ex)
            {
                ex = ex.GetTrue();

                if (ShowError) WriteLog("{0}", ex);

                if (ex is ApiException apiException)
                {
                    code = apiException.Code;
                    result = ex.Message;
                }
                else
                {
                    code = ApiCode.InternalServerError;
                    result = ex.Message;
                }

                span?.SetError(ex, args?.ToStr());
            }

            if (msg.OneWay) return null;

            if (encoder is HttpEncoder httpEncoder) httpEncoder.UseHttpStatus = UseHttpStatus;

            return encoder.CreateResponse(msg, action, code, result);
        }
        finally
        {
            var elapsed = counter.StopCount(startTicks) / 1000;
            if (SlowTrace > 0 && elapsed >= SlowTrace) WriteLog($"慢处理[{action}]，Code={code}，耗时{elapsed:n0}ms");

            span?.Dispose();
        }
    }

    /// <summary>执行消息处理，交给Handler</summary>
    /// <param name="session">会话</param>
    /// <param name="action">动作</param>
    /// <param name="args">参数</param>
    /// <param name="msg">消息</param>
    /// <returns>处理结果</returns>
    protected virtual Object? OnProcess(IApiSession session, String action, IPacket? args, IMessage msg)
    {
        var handler = Handler ?? throw new InvalidOperationException("未配置 IApiHandler。请提供 AOT 安全的处理器实现。");
        return handler.Execute(session, action, args, msg);
    }
    #endregion

    #region 统计
    private void DoStat(Object? state)
    {
        var builder = Pool.StringBuilder.Get();
        var counter = StatProcess;
        if (counter != null && counter.Value > 0) builder.AppendFormat("处理：{0} ", counter);

        if (Server is NetServer netServer)
            builder.AppendFormat("在线：{0} 最大在线：{1}", netServer.SessionCount, netServer.MaxSessionCount);

        var message = builder.Return(true) ?? String.Empty;
        if (message.IsNullOrEmpty() || message == _last) return;
        _last = message;

        WriteLog(message);
    }
    #endregion
}