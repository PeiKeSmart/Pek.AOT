using System.Collections;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using Pek.Logging;

namespace Pek;

/// <summary>具有是否已释放和释放后事件的接口</summary>
public interface IDisposable2 : IDisposable
{
    /// <summary>是否已经释放</summary>
    [XmlIgnore, IgnoreDataMember]
    Boolean Disposed { get; }

    /// <summary>被销毁时触发事件</summary>
    event EventHandler? OnDisposed;
}

/// <summary>具有销毁资源处理的抽象基类</summary>
public abstract class DisposeBase : IDisposable2
{
    [NonSerialized]
    private Int32 _disposed;

    /// <summary>是否已经释放</summary>
    [XmlIgnore, IgnoreDataMember]
    public Boolean Disposed => _disposed > 0;

    /// <summary>被销毁时触发事件</summary>
    [field: NonSerialized]
    public event EventHandler? OnDisposed;

    /// <summary>释放资源</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>释放资源，参数表示是否由 Dispose 调用</summary>
    /// <param name="disposing">是否由 Dispose 调用</param>
    protected virtual void Dispose(Boolean disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        try
        {
            OnDisposed?.Invoke(this, EventArgs.Empty);
        }
        catch { }
    }

    /// <summary>如果对象已释放则抛出异常</summary>
    protected void ThrowIfDisposed()
    {
        if (Disposed) throw new ObjectDisposedException(GetType().FullName);
    }

    /// <summary>析构函数</summary>
    ~DisposeBase()
    {
        try
        {
            Dispose(false);
        }
        catch (Exception ex)
        {
            XXTrace.WriteException(ex);
        }
    }
}

/// <summary>销毁助手</summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class DisposeHelper
{
    /// <summary>尝试销毁对象</summary>
    /// <param name="obj">目标对象</param>
    /// <returns>原对象</returns>
    public static Object? TryDispose(this Object? obj)
    {
        if (obj == null) return obj;

        if (obj is IEnumerable ems)
        {
            if (obj is not IList list)
            {
                list = new List<Object>();
                foreach (var item in ems)
                {
                    if (item is IDisposable) list.Add(item);
                }
            }

            foreach (var item in list)
            {
                if (item is IDisposable disp)
                {
                    try
                    {
                        disp.Dispose();
                    }
                    catch { }
                }
            }
        }

        if (obj is IDisposable disp2)
        {
            try
            {
                disp2.Dispose();
            }
            catch { }
        }

        return obj;
    }
}