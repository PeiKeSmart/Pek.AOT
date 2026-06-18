namespace Pek.Helpers;

/// <summary>重试类</summary>
public static class Retry
{
    /// <summary>无论遇到任何错误，最多尝试<paramref name="times"/>次</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="times"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T RetryOnAny<T>(Int32 times, Func<T> action)
    {
        return RetryOnAny(times, a => action.Invoke(), ef =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(ef.current * 5));
        });
    }

    /// <summary>当遇到<typeparamref name="E"/>的异常时重试指定<paramref name="times"/>次，遇到其他异常则认为失败</summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="E"></typeparam>
    /// <param name="times"></param>
    /// <param name="action"></param>
    /// <param name="efunc"></param>
    /// <returns></returns>
    public static T RetryOnException<T, E>(Int32 times, Func<Int32, T> action, Action<(Int32 current, Exception ex)>? efunc) where E : Exception
    {
        Exception? exception = null;
        for (var i = 0; i < times; i++)
        {
            try
            {
                try
                {
                    return action.Invoke(i + 1);
                }
                catch (E)
                {
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                break;
            }
        }
        throw exception!;
    }

    /// <summary>重试指定任务，除非遇到异常<typeparamref name="E"/>就不再重试</summary>
    /// <typeparam name="T">返回值</typeparam>
    /// <typeparam name="E">遇到此异常不再重试</typeparam>
    /// <param name="times">次数</param>
    /// <param name="action">调用的方法</param>
    /// <returns></returns>
    public static T RetryUnlessException<T, E>(Int32 times, Func<Int32, T> action) where E : Exception
    {
        Exception? exception = null;
        for (var i = 0; i < times; i++)
        {
            try
            {
                try
                {
                    return action.Invoke(i + 1);
                }
                catch (E ex)
                {
                    exception = ex;
                    break;
                }
            }
            catch (Exception)
            {
            }
        }
        throw exception!;
    }

    /// <summary>重试指定次数，每次失败后执行回调</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="times"></param>
    /// <param name="action"></param>
    /// <param name="efunc"></param>
    /// <returns></returns>
    public static T RetryOnAny<T>(Int32 times, Func<Int32, T> action, Action<(Int32 current, Exception ex)>? efunc)
    {
        Exception? exception = null;
        for (var i = 0; i < times; i++)
        {
            try
            {
                return action.Invoke(i + 1);
            }
            catch (Exception ex)
            {
                exception = ex;
                efunc?.Invoke((i + 1, ex));
            }
        }
        throw exception!;
    }

    /// <summary>重试指定次数，每次失败后执行回调</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="times"></param>
    /// <param name="action"></param>
    /// <param name="efunc"></param>
    /// <returns></returns>
    public static T RetryOnAny<T>(Int32 times, Func<T> action, Action<(Int32 current, Exception ex)>? efunc)
    {
        Exception? exception = null;
        for (var i = 0; i < times; i++)
        {
            try
            {
                return action.Invoke();
            }
            catch (Exception ex)
            {
                exception = ex;
                efunc?.Invoke((i + 1, ex));
            }
        }
        throw exception!;
    }
}
