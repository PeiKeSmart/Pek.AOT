namespace Pek.Log;

/// <summary>追踪器解析器</summary>
public interface ITracerResolver
{
    /// <summary>从 Uri 中解析埋点名称</summary>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    String? ResolveName(Uri uri, Object? userState);

    /// <summary>解析埋点名称</summary>
    /// <param name="name">原始名称</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    String? ResolveName(String name, Object? userState);

    /// <summary>创建 Http 请求埋点</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点实例</returns>
    ISpan? CreateSpan(ITracer tracer, Uri uri, Object? userState);
}

/// <summary>默认追踪器解析器</summary>
public class DefaultTracerResolver : ITracerResolver
{
    /// <summary>从 Uri 中解析埋点名称</summary>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    public virtual String? ResolveName(Uri uri, Object? userState)
    {
        String name;
        if (uri.IsAbsoluteUri)
        {
            var segments = uri.Segments.Skip(1).TakeWhile(e => e.Length <= 16).ToArray();
            name = segments.Length > 0
                ? $"{uri.Scheme}://{uri.Authority}/{String.Concat(segments)}"
                : $"{uri.Scheme}://{uri.Authority}";
        }
        else
        {
            name = uri.ToString();
            var p = name.IndexOf('?');
            if (p > 0) name = name[..p];
        }

        return ResolveName(name, userState);
    }

    /// <summary>解析埋点名称</summary>
    /// <param name="name">原始名称</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点名称</returns>
    public virtual String? ResolveName(String name, Object? userState) => name;

    /// <summary>创建 Http 请求埋点</summary>
    /// <param name="tracer">跟踪器</param>
    /// <param name="uri">目标地址</param>
    /// <param name="userState">用户状态</param>
    /// <returns>埋点实例</returns>
    public virtual ISpan? CreateSpan(ITracer tracer, Uri uri, Object? userState)
    {
        var name = ResolveName(uri, userState);
        if (String.IsNullOrWhiteSpace(name)) return null;

        var span = tracer.NewSpan(name);
        span.SetTag(uri.ToString());
        return span;
    }
}