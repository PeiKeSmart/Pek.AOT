namespace Pek.Remoting;

/// <summary>远程调用异常</summary>
public class ApiException : Exception
{
    /// <summary>代码</summary>
    public Int32 Code { get; set; }

    /// <summary>实例化远程调用异常</summary>
    /// <param name="code">错误码</param>
    /// <param name="message">错误消息</param>
    public ApiException(Int32 code, String message) : base(message) => Code = code;

    /// <summary>实例化远程调用异常</summary>
    /// <param name="code">错误码</param>
    /// <param name="exception">异常对象</param>
    public ApiException(Int32 code, Exception exception) : base(exception.Message, exception) => Code = code;
}