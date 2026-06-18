namespace Pek.Exceptions;

/// <summary>异常的扩展方法</summary>
public static class ExceptionExtensions
{
    /// <summary>检查异常是否为指定类型</summary>
    /// <typeparam name="TException">要检查的异常类型</typeparam>
    /// <param name="ex">要检查的异常</param>
    /// <returns>如果异常是指定类型则返回 <see langword="true"/></returns>
    public static Boolean Is<TException>(this Exception ex)
        where TException : Exception
    {
        switch (ex)
        {
            case TException _:
                return true;
            case AggregateException aggregateException:
                return aggregateException.InnerException is TException;
            default:
                break;
        }

        return false;
    }

    /// <summary>将异常转换为指定类型</summary>
    /// <typeparam name="TException">目标异常类型</typeparam>
    /// <param name="ex">要转换的异常</param>
    /// <returns>目标异常类型的异常</returns>
    public static TException Get<TException>(this Exception ex)
        where TException : Exception
    {
        switch (ex)
        {
            case TException expectedException:
                return expectedException;
            case AggregateException aggregateException:
                if (aggregateException.InnerException is TException expectedExceptionFromAggregate)
                {
                    return expectedExceptionFromAggregate;
                }

                break;
        }

        throw new InvalidCastException();
    }
}
