using System.Collections.Concurrent;
using System.Diagnostics;
using Pek.Log;
using Pek.Security;
using Pek.Threading;

using Pek.Extension;

namespace Pek.Model;

/// <summary>寤惰繜闃熷垪銆傜紦鍐插悎骞跺璞★紝鎵归噺澶勭悊</summary>
/// <remarks>
/// 鏂囨。 https://newlifex.com/core/deferred_queue
/// 
/// 鍊熷姪瀹炰綋瀛楀吀锛岀紦鍐插疄浣撳璞★紝瀹氭湡缁欏瓧鍏告崲鏂帮紝瀹炵幇鎵归噺澶勭悊銆?
/// 
/// 鏈夊彲鑳藉閮ㄦ嬁鍒板璞″悗锛屾鍦ㄤ慨鏀癸紝鍐呴儴鎭板阀鎵ц鎵归噺澶勭悊锛屽鑷村閮ㄧ殑閮ㄥ垎淇敼鏈兘寰楀埌澶勭悊銆?
/// 瑙ｅ喅鍔炴硶鏄鍔犱竴涓彁浜ゆ満鍒讹紝澶栭儴鐢ㄥ畬鍚庢彁浜や慨鏀癸紝鍐呴儴闇€瑕佸鐞嗘椂锛岀瓑寰呬竴涓椂闂淬€?
/// </remarks>
public class DeferredQueue : DisposeBase
{
    #region 灞炴€?
    /// <summary>鍚嶇О銆傜敤浜庢棩蹇楀拰璋冭瘯</summary>
    public String Name { get; set; }

    private volatile ConcurrentDictionary<String, Object> _Entities = new();
    /// <summary>瀹炰綋瀛楀吀銆傚瓨鍌ㄥ緟澶勭悊鐨勫璞?/summary>
    public ConcurrentDictionary<String, Object> Entities => _Entities;

    /// <summary>璺熻釜鏁般€傝揪鍒拌鍊兼椂杈撳嚭璺熻釜鏃ュ織锛岄粯璁?000</summary>
    public Int32 TraceCount { get; set; } = 1000;

    /// <summary>鍛ㄦ湡銆傚畾鏃跺鐞嗛棿闅旓紝榛樿10_000姣</summary>
    public Int32 Period { get; set; } = 10_000;

    /// <summary>鏈€澶т釜鏁般€傝秴杩囪涓暟鏃讹紝杩涘叆闃熷垪灏嗕骇鐢熷牭濉炪€傞粯璁?0_000_000</summary>
    public Int32 MaxEntity { get; set; } = 10_000_000;

    /// <summary>鎵瑰ぇ灏忋€傛瘡鎵瑰鐞嗙殑鏈€澶у璞℃暟锛岄粯璁?_000</summary>
    public Int32 BatchSize { get; set; } = 5_000;

    /// <summary>绛夊緟鍊熷嚭瀵硅薄纭淇敼鐨勬椂闂淬€傞粯璁?000ms</summary>
    public Int32 WaitForBusy { get; set; } = 3_000;

    /// <summary>淇濆瓨閫熷害銆傛瘡绉掍繚瀛樺灏戜釜瀹炰綋</summary>
    public Int32 Speed { get; private set; }

    /// <summary>鏄惁寮傛澶勭悊銆傞粯璁rue琛ㄧず寮傛澶勭悊锛屽叡鐢―Q瀹氭椂璋冨害锛沠alse琛ㄧず鍚屾澶勭悊锛岀嫭绔嬬嚎绋?/summary>
    public Boolean Async { get; set; } = true;

    private volatile Int32 _Times;
    /// <summary>鍚堝苟淇濆瓨鐨勬€绘鏁?/summary>
    public Int32 Times => _Times;

    /// <summary>鎵规澶勭悊鎴愬姛鏃剁殑鍥炶皟</summary>
    public Action<IList<Object>>? Finish;

    /// <summary>鎵规澶勭悊澶辫触鏃剁殑鍥炶皟</summary>
    public Action<IList<Object>, Exception>? Error;

    /// <summary>闃熷垪婧㈠嚭閫氱煡銆傚弬鏁颁负褰撳墠缂撳瓨涓暟</summary>
    public Action<Int32>? Overflow;

    private volatile Int32 _count;
    /// <summary>褰撳墠缂撳瓨涓暟</summary>
    public Int32 Count => _count;

    /// <summary>绛夊緟纭淇敼鐨勫€熷嚭瀵硅薄鏁?/summary>
    private volatile Int32 _busy;

    private TimerX? _Timer;
    #endregion

    #region 鏋勯€?
    /// <summary>瀹炰緥鍖栧欢杩熼槦鍒?/summary>
    public DeferredQueue() => Name = GetType().Name.TrimSuffix("Queue", "Actor", "Cache");

    /// <summary>閿€姣佽祫婧愩€傜粺璁￠槦鍒楅攢姣佹椂淇濆瓨鏁版嵁</summary>
    /// <param name="disposing">鏄惁閲婃斁鎵樼璧勬簮</param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        try
        {
            // 鍋滄璋冨害鍣紝灏介噺鍚屾娓呯┖缂撳瓨锛岄伩鍏嶉攢姣佹椂涓㈡暟鎹?
            _Timer?.Dispose();
            Flush();
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }

        _Entities?.Clear();
    }

    /// <summary>鍒濆鍖栧畾鏃跺櫒</summary>
    public void Init()
    {
        // 棣栨浣跨敤鏃跺垵濮嬪寲瀹氭椂鍣?
        if (_Timer == null)
        {
            lock (this)
            {
                _Timer ??= OnInit();
            }
        }
    }

    /// <summary>鍒濆鍖栧畾鏃跺櫒</summary>
    /// <returns>瀹氭椂鍣ㄥ疄渚?/returns>
    protected virtual TimerX OnInit()
    {
        // 涓轰簡閬垮厤澶氶槦鍒楀苟鍙戯紝棣栨鎵ц鏃堕棿闅忔満閿欏紑
        var p = Period;
        if (p > 1000) p = Rand.Next(1000, p);

        var name = Async ? "DQ" : Name;

        var timer = new TimerX(Work, null, p, Period, name) { Async = Async };

        // 鐙珛璋冨害鏃跺姞澶ф渶澶ц€楁椂鍛婅
        if (!Async) timer.Scheduler.MaxCost = 30_000;

        return timer;
    }
    #endregion

    #region 鏂规硶
    /// <summary>灏濊瘯娣诲姞瀵硅薄鍒伴槦鍒?/summary>
    /// <param name="key">瀵硅薄閿?/param>
    /// <param name="value">瀵硅薄鍊?/param>
    /// <returns>鏄惁娣诲姞鎴愬姛</returns>
    public virtual Boolean TryAdd(String key, Object value)
    {
        Interlocked.Increment(ref _Times);

        Init();

        if (!_Entities.TryAdd(key, value)) return false;

        Interlocked.Increment(ref _count);

        // 瓒呰繃鏈€澶у€兼椂锛屽牭濉炰竴娈垫椂闂达紝绛夊緟娑堣垂瀹屾垚
        CheckMax();

        return true;
    }

    /// <summary>鑾峰彇鎴栨坊鍔犲疄浣撳璞★紝鍦ㄥ閮ㄤ慨鏀瑰璞″€?/summary>
    /// <remarks>
    /// 澶栭儴姝ｅ湪淇敼瀵硅薄鏃讹紝鍐呴儴涓嶅厑璁告墽琛屾壒閲忓鐞嗐€?
    /// 浣跨敤瀹屾瘯鍚庨渶璋冪敤 <see cref="Commit"/> 鏂规硶鎻愪氦淇敼銆?
    /// </remarks>
    /// <typeparam name="T">瀵硅薄绫诲瀷</typeparam>
    /// <param name="key">瀵硅薄閿?/param>
    /// <param name="valueFactory">瀵硅薄宸ュ巶锛岀敤浜庡垱寤烘柊瀵硅薄</param>
    /// <returns>鑾峰彇鎴栧垱寤虹殑瀵硅薄瀹炰緥</returns>
    public virtual T? GetOrAdd<T>(String key, Func<String, T>? valueFactory = null) where T : class, new()
    {
        Interlocked.Increment(ref _Times);

        Init();

        Object? entity;
        while (!_Entities.TryGetValue(key, out entity))
        {
            if (entity == null)
            {
                if (valueFactory != null)
                    entity = valueFactory(key);
                else
                    entity = new T();
            }
            if (_Entities.TryAdd(key, entity))
            {
                Interlocked.Increment(ref _count);
                break;
            }
        }

        // 瓒呰繃鏈€澶у€兼椂锛屽牭濉炰竴娈垫椂闂达紝绛夊緟娑堣垂瀹屾垚
        CheckMax();

        // 澧炲姞绻佸繖鏁?
        Interlocked.Increment(ref _busy);

        return entity as T;
    }

    /// <summary>灏濊瘯绉婚櫎涓€涓敭</summary>
    /// <param name="key">瀵硅薄閿?/param>
    /// <returns>鏄惁绉婚櫎鎴愬姛</returns>
    public virtual Boolean TryRemove(String key)
    {
        if (_Entities.TryRemove(key, out _))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }
        return false;
    }

    /// <summary>绔嬪嵆瑙﹀彂涓€娆″鐞?/summary>
    public void Trigger()
    {
        Init();
        _Timer?.SetNext(0);
    }

    /// <summary>鍚屾娓呯┖骞跺鐞嗗綋鍓嶇紦瀛?/summary>
    public void Flush()
    {
        // 鐢变簬 Work 浼氫氦鎹?_Entities锛屽洜姝ゅ惊鐜洿鍒颁负绌?
        while (!_Entities.IsEmpty)
        {
            Work(null);
        }
    }

    private void CheckMax()
    {
        if (_count < MaxEntity) return;

        using var span = Tracer?.NewError("MaxQueueOverflow", $"寤惰繜闃熷垪[{Name}]瓒呰繃涓婇檺{MaxEntity:n0}");

        // 閫氱煡澶栭儴鍙戠敓婧㈠嚭
        try { Overflow?.Invoke(_count); } catch { /* 蹇界暐涓氬姟渚у紓甯?*/ }

        // 瓒呰繃鏈€澶у€兼椂锛屽牭濉炰竴娈垫椂闂达紝绛夊緟娑堣垂瀹屾垚
        var t = WaitForBusy * 5;
        while (t > 0)
        {
            if (_count < MaxEntity) return;

            Thread.Sleep(100);
            t -= 100;
        }

        throw new InvalidOperationException($"The existing data amount [{_count:n0}] exceeds the maximum data amount [{MaxEntity:n0}]");
    }

    /// <summary>鎻愪氦瀵硅薄鐨勪慨鏀癸紝澶栭儴涓嶅啀浣跨敤璇ュ璞?/summary>
    /// <param name="key">瀵硅薄閿?/param>
    public virtual void Commit(String key)
    {
        // 鍑忓皯绻佸繖鏁?
        if (_busy > 0) Interlocked.Decrement(ref _busy);
    }

    private void Work(Object? state)
    {
        var es = _Entities;
        if (es.IsEmpty) return;

        _Entities = new ConcurrentDictionary<String, Object>();
        var times = _Times;

        Interlocked.Add(ref _count, -es.Count);
        Interlocked.Add(ref _Times, -times);

        // 妫€鏌ョ箒蹇欐暟锛岀瓑寰呭閮ㄦ湭瀹屾垚鐨勪慨鏀?
        var t = WaitForBusy;
        while (_busy > 0 && t > 0)
        {
            Thread.Sleep(100);
            t -= 100;
        }
        //_busy = 0;

        // 鍏堝彇鍑烘潵
        var list = es.Values.ToList();

        if (list.Count == 0) return;

        using var span = Tracer?.NewSpan($"mq:{Name}:Process", null, list.Count);
        var sw = Stopwatch.StartNew();
        var total = ProcessAll(list);
        sw.Stop();

        var ms = sw.Elapsed.TotalMilliseconds;
        Speed = ms == 0 ? 0 : (Int32)(list.Count * 1000 / ms);
        if (list.Count >= TraceCount)
        {
            var sp = ms == 0 ? 0 : (Int32)(times * 1000 / ms);
            WriteLog($"淇濆瓨 {list.Count:n0}\t鑰楁椂 {ms:n0}ms\t閫熷害 {Speed:n0}tps\t娆℃暟 {times:n0}\t閫熷害 {sp:n0}tps\t鎴愬姛 {total:n0}");
        }

        // 鏇存柊瀹氭椂鍣ㄥ懆鏈?
        if (Period > 0 && _Timer != null) _Timer.Period = Period;
    }

    /// <summary>瀹氭椂澶勭悊鍏ㄩ儴鏁版嵁</summary>
    /// <param name="list">寰呭鐞嗗璞￠泦鍚?/param>
    /// <returns>鎴愬姛澶勭悊鐨勫璞℃暟</returns>
    protected virtual Int32 ProcessAll(ICollection<Object> list)
    {
        var total = 0;

        // 浣跨敤 List.GetRange 闄嶄綆鍒嗛厤
        var data = list as List<Object> ?? list.ToList();
        for (var i = 0; i < data.Count;)
        {
            var count = Math.Min(BatchSize, data.Count - i);
            var batch = data.GetRange(i, count);

            try
            {
                total += Process(batch);

                Finish?.Invoke(batch);
            }
            catch (Exception ex)
            {
                OnError(batch, ex);
            }

            i += count;
        }

        return total;
    }

    /// <summary>澶勭悊涓€鎵瑰璞?/summary>
    /// <param name="list">寰呭鐞嗗璞″垪琛?/param>
    /// <returns>鎴愬姛澶勭悊鐨勫璞℃暟</returns>
    public virtual Int32 Process(IList<Object> list) => 0;

    /// <summary>鍙戠敓閿欒鏃剁殑澶勭悊</summary>
    /// <param name="list">澶勭悊澶辫触鐨勫璞″垪琛?/param>
    /// <param name="ex">寮傚父淇℃伅</param>
    protected virtual void OnError(IList<Object> list, Exception ex)
    {
        if (Error != null)
            Error(list, ex);
        else
            XTrace.WriteException(ex);
    }
    #endregion

    #region 杈呭姪
    /// <summary>鏃ュ織</summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>閾捐矾杩借釜</summary>
    public ITracer? Tracer { get; set; }

    /// <summary>鍐欐棩蹇?/summary>
    /// <param name="format">鏍煎紡鍖栧瓧绗︿覆</param>
    /// <param name="args">鍙傛暟</param>
    protected void WriteLog(String format, params Object?[] args) => Log?.Info($"寤惰繜闃熷垪[{Name}]\t{format}", args);
    #endregion
}
