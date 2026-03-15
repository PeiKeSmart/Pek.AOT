namespace Pek.Log;

/// <summary>链路追踪功能接口</summary>
public interface ITracerFeature
{
    /// <summary>链路追踪</summary>
    ITracer? Tracer { get; set; }
}

/// <summary>携带链路追踪标识的消息接口</summary>
public interface ITraceMessage
{
    /// <summary>链路追踪标识</summary>
    String? TraceId { get; set; }
}