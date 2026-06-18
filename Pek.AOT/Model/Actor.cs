using System.Collections.Concurrent;
using Pek.Log;

using Pek.Extension;

namespace Pek.Model;

/// <summary>鏃犻攣骞惰缂栫▼妯″瀷</summary>
/// <remarks>
/// 鏂囨。 https://newlifex.com/core/actor
/// 
/// 鐙珛绾跨▼杞娑堟伅闃熷垪锛岀畝鍗曡璁￠伩鍏嶅奖鍝嶉粯璁ょ嚎绋嬫睜銆?
/// 閫傜敤浜庝换鍔￠绮掕緝澶х殑鍦哄悎锛屼緥濡侷O鎿嶄綔銆?
/// </remarks>
public interface IActor
{
    /// <summary>娣诲姞娑堟伅锛岄┍鍔ㄥ唴閮ㄥ鐞?/summary>
    /// <param name="message">娑堟伅瀵硅薄</param>
    /// <param name="sender">鍙戦€佽€匒ctor</param>
    /// <returns>杩斿洖寰呭鐞嗘秷鎭暟</returns>
    Int32 Tell(Object message, IActor? sender = null);
}

/// <summary>Actor涓婁笅鏂?/summary>
public class ActorContext
{
    /// <summary>鍙戦€佽€呫€傚彂閫佹秷鎭殑Actor瀵硅薄</summary>
    public IActor? Sender { get; set; }

    /// <summary>娑堟伅銆傚緟澶勭悊鐨勬秷鎭璞?/summary>
    public Object? Message { get; set; }
}

/// <summary>鏃犻攣骞惰缂栫▼妯″瀷</summary>
/// <remarks>
/// 鐙珛绾跨▼杞娑堟伅闃熷垪锛岀畝鍗曡璁￠伩鍏嶅奖鍝嶉粯璁ょ嚎绋嬫睜銆?
/// </remarks>
public abstract class Actor : DisposeBase, IActor
{
    #region 灞炴€?
    /// <summary>鍚嶇О銆傜敤浜庢棩蹇楀拰杩借釜</summary>
    public String Name { get; set; }

    /// <summary>鏄惁鍚敤銆傝〃绀篈ctor鏄惁姝ｅ湪杩愯</summary>
    public Boolean Active { get; private set; }

    /// <summary>鍙楅檺瀹归噺銆傛渶澶у彲鍫嗙Н鐨勬秷鎭暟锛岄粯璁nt32.MaxValue</summary>
    public Int32 BoundedCapacity { get; set; } = Int32.MaxValue;

    /// <summary>鎵瑰ぇ灏忋€傛瘡娆″鐞嗘秷鎭暟锛岄粯璁?锛屽ぇ浜?琛ㄧず鍚敤鎵归噺澶勭悊妯″紡</summary>
    public Int32 BatchSize { get; set; } = 1;

    /// <summary>鏄惁闀挎椂闂磋繍琛屻€傞暱鏃堕棿杩愯浠诲姟浣跨敤鐙珛绾跨▼锛岄粯璁rue</summary>
    public Boolean LongRunning { get; set; } = true;

    /// <summary>瀛樻斁娑堟伅鐨勯偖绠便€傞粯璁IFO瀹炵幇锛屽閮ㄥ彲瑕嗙洊</summary>
    protected BlockingCollection<ActorContext>? MailBox { get; set; }

    /// <summary>鎬ц兘杩借釜鍣?/summary>
    public ITracer? Tracer { get; set; }

    /// <summary>鐖剁骇鎬ц兘杩借釜鍣ㄣ€傜敤浜庢妸鍐呭璋冪敤閾惧叧鑱旇捣鏉?/summary>
    public ISpan? TracerParent { get; set; }

    private Task? _task;
    private Exception? _error;
    private CancellationTokenSource? _source;
    private Int32 _queueLength;
    private Int32 _starting;

    /// <summary>褰撳墠闃熷垪闀垮害銆傚緟澶勭悊娑堟伅鏁?/summary>
    public Int32 QueueLength => _queueLength;
    #endregion

    #region 鏋勯€?
    /// <summary>瀹炰緥鍖朅ctor</summary>
    public Actor() => Name = GetType().Name.TrimSuffix("Actor");

    /// <summary>閿€姣佽祫婧?/summary>
    /// <param name="disposing">鏄惁閲婃斁鎵樼璧勬簮</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        _error = null;
        Stop(0);

        if (_source != null)
        {
            _source.Cancel();
            _source.Dispose();
        }

        //_task?.Dispose();

        MailBox?.Dispose();
    }

    /// <summary>宸查噸杞姐€傛樉绀哄悕绉?/summary>
    /// <returns>Actor鍚嶇О</returns>
    public override String ToString() => Name;
    #endregion

    #region 鏂规硶
    /// <summary>閫氱煡寮€濮嬪鐞?/summary>
    /// <remarks>娣诲姞娑堟伅鏃惰嚜鍔ㄨЕ鍙?/remarks>
    /// <returns>鎵ц浠诲姟</returns>
    public virtual Task? Start() => Start(default);

    /// <summary>閫氱煡寮€濮嬪鐞?/summary>
    /// <remarks>娣诲姞娑堟伅鏃惰嚜鍔ㄨЕ鍙?/remarks>
    /// <param name="cancellationToken">鍙栨秷浠ょ墝銆傚彲鐢ㄤ簬閫氱煡鍐呴儴鍙栨秷宸ヤ綔</param>
    /// <returns>鎵ц浠诲姟</returns>
    public virtual Task? Start(CancellationToken cancellationToken = default)
    {
        if (Active) return _task;

        // 浣跨敤鍘熷瓙鎿嶄綔闃叉骞跺彂鍚姩
        if (Interlocked.CompareExchange(ref _starting, 1, 0) != 0)
        {
            // 绛夊緟鍚姩瀹屾垚
            SpinWait.SpinUntil(() => Active || _starting == 0, 1000);
            return _task;
        }

        try
        {
            if (Active) return _task;

            if (Tracer == null && TracerParent is DefaultSpan ds) Tracer = ds.Tracer;

            using var span = Tracer?.NewSpan("actor:Start", Name);

            _source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            MailBox ??= new BlockingCollection<ActorContext>(BoundedCapacity);

            // 鍚姩寮傛浠诲姟
            _task ??= OnStart(_source.Token);

            Active = true;

            WriteLog("Actor鍚姩 BoundedCapacity={0} BatchSize={1} LongRunning={2}", BoundedCapacity, BatchSize, LongRunning);

            return _task;
        }
        finally
        {
            Interlocked.Exchange(ref _starting, 0);
        }
    }

    /// <summary>寮€濮嬫椂锛岃繑鍥炴墽琛岀嚎绋嬪寘瑁呬换鍔?/summary>
    /// <param name="cancellationToken">鍙栨秷浠ょ墝</param>
    /// <returns>鎵ц浠诲姟</returns>
    protected virtual Task OnStart(CancellationToken cancellationToken)
    {
        var creationOptions = LongRunning ? TaskCreationOptions.LongRunning : TaskCreationOptions.None;
        // 浣跨敤榛樿璋冨害鍣紝閬垮厤褰卞搷UI璋冨害鍣?
        var scheduler = TaskScheduler.Default;
        return Task.Factory.StartNew(DoActorWork, cancellationToken, creationOptions, scheduler);
    }

    /// <summary>閫氱煡鍋滄娣诲姞娑堟伅锛屽苟绛夊緟澶勭悊瀹屾垚</summary>
    /// <param name="msTimeout">绛夊緟鐨勬绉掓暟銆?琛ㄧず涓嶇瓑寰咃紝-1琛ㄧず鏃犻檺绛夊緟</param>
    /// <returns>鏄惁鍦ㄨ秴鏃跺墠瀹屾垚澶勭悊</returns>
    public virtual Boolean Stop(Int32 msTimeout = 0)
    {
        using var span = Tracer?.NewSpan("actor:Stop", $"{Name} msTimeout={msTimeout}");
        try
        {
            WriteLog("Actor鍋滄 QueueLength={0} msTimeout={1}", _queueLength, msTimeout);

            MailBox?.CompleteAdding();

            if (msTimeout > 0 && _source != null && !_source.IsCancellationRequested)
                _source.CancelAfter(msTimeout);

            if (_error != null) throw _error;
            if (msTimeout == 0 || _task == null) return true;
            if (_task.IsCompleted) return true;

            try { return _task.Wait(msTimeout); }
            catch (AggregateException) { return false; }
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);
            throw;
        }
    }

    /// <summary>娣诲姞娑堟伅锛岄┍鍔ㄥ唴閮ㄥ鐞?/summary>
    /// <param name="message">娑堟伅瀵硅薄</param>
    /// <param name="sender">鍙戦€佽€匒ctor</param>
    /// <returns>杩斿洖寰呭鐞嗘秷鎭暟</returns>
    public virtual Int32 Tell(Object message, IActor? sender = null)
    {
        //using var span = Tracer?.NewSpan("actor:Tell", Name);
        if (!Active)
        {
            if (_error != null) throw _error;

            // 鑷姩寮€濮?
            Start();

            if (!Active) throw new ObjectDisposedException(nameof(Actor));
        }

        var box = MailBox ?? throw new ArgumentNullException(nameof(MailBox));
        box.Add(new ActorContext { Sender = sender, Message = message });

        return Interlocked.Increment(ref _queueLength);
    }

    /// <summary>寰幆娑堣垂娑堟伅</summary>
    private void DoActorWork()
    {
        DefaultSpan.Current = TracerParent;

        using var span = Tracer?.NewSpan("actor:Loop", Name);
        try
        {
            Loop();
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { /*CompleteAdding鍚嶵ake浼氭姏鍑篒OE寮傚父*/}
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            _error = ex;
            WriteLog("Actor寮傚父 {0}", ex.Message);
            XTrace.WriteException(ex);
        }

        Active = false;
    }

    /// <summary>寰幆娑堣垂娑堟伅</summary>
    protected virtual void Loop()
    {
        var box = MailBox;
        if (box == null || _source == null) return;

        var span = DefaultSpan.Current;
        var token = _source.Token;
        while (!_source.IsCancellationRequested && !box.IsCompleted)
        {
            if (BatchSize <= 1)
            {
                var ctx = box.Take(token);
                var task = ReceiveAsync(ctx, token);
                task?.Wait(token);

                if (span != null) span.Value++;
                Interlocked.Decrement(ref _queueLength);
            }
            else
            {
                var list = new List<ActorContext>();

                // 闃诲鍙栦竴涓?
                var ctx = box.Take(token);
                list.Add(ctx);

                // 涓嶉樆濉炲彇涓€鎵?
                for (var i = 1; i < BatchSize; i++)
                {
                    if (!box.TryTake(out ctx)) break;

                    list.Add(ctx);
                }
                if (span != null) span.Value += list.Count;

                var task = ReceiveAsync(list.ToArray(), token);
                task?.Wait(token);

                Interlocked.Add(ref _queueLength, -list.Count);
            }
        }
    }

    /// <summary>澶勭悊娑堟伅銆傛壒澶у皬涓?鏃朵娇鐢ㄨ鏂规硶</summary>
    /// <param name="context">娑堟伅涓婁笅鏂?/param>
    /// <param name="cancellationToken">鍙栨秷浠ょ墝</param>
    /// <returns>寮傛浠诲姟</returns>
    protected virtual Task ReceiveAsync(ActorContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>鎵归噺澶勭悊娑堟伅銆傛壒澶у皬澶т簬1鏃朵娇鐢ㄨ鏂规硶</summary>
    /// <param name="contexts">娑堟伅涓婁笅鏂囬泦鍚?/param>
    /// <param name="cancellationToken">鍙栨秷浠ょ墝</param>
    /// <returns>寮傛浠诲姟</returns>
    protected virtual Task ReceiveAsync(ActorContext[] contexts, CancellationToken cancellationToken) => Task.CompletedTask;
    #endregion

    #region 杈呭姪
    /// <summary>鏃ュ織</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>鍐欐棩蹇?/summary>
    /// <param name="format">鏍煎紡鍖栧瓧绗︿覆</param>
    /// <param name="args">鍙傛暟</param>
    protected void WriteLog(String format, params Object?[] args) => Log?.Info($"[{Name}]{format}", args);
    #endregion
}
