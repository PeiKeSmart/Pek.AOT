using System.Diagnostics.CodeAnalysis;

using Pek.Extension;
using System.Reflection;

using Pek.Extension;
using System.Runtime.CompilerServices;

using Pek.Extension;
using System.Text;

using Pek.Extension;
using Pek.Data;
using Pek.Log;
using Pek.Serialization;

namespace Pek.IO;

/// <summary>Csv鏂囦欢杞婚噺绾ф暟鎹簱</summary>
/// <remarks>
/// 鏂囨。 https://newlifex.com/core/csv_db
/// 閫傜敤浜庡ぇ閲忔暟鎹渶瑕佸揩閫熻拷鍔犮€佸揩閫熸煡鎵撅紝寰堝皯淇敼鍜屽垹闄ょ殑鍦烘櫙銆?
/// 鍦ㄦ闈㈠鎴风涓紝鍏崇郴鍨嬫暟鎹簱SQLite寰堝鏄撳洜闈炴硶鍏虫満鑰屾崯鍧忥紝鏈暟鎹簱鑳借烦杩囨崯鍧忚锛岃嚜鍔ㄦ仮澶嶃€?
/// 
/// 涓棿鎻掑叆鍜屽垹闄ゆ椂锛岄渶瑕佺Щ鍔ㄥ熬閮ㄦ暟鎹紝鎬ц兘杈冨樊銆?
/// 
/// 鏈璁′笉鏀寔绾跨▼瀹夊叏锛屽姟蹇呯‘淇濆崟绾跨▼鎿嶄綔銆?
/// </remarks>
/// <typeparam name="T">瀹炰綋绫诲瀷</typeparam>
public class CsvDb<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T> : DisposeBase where T : new()
{
    #region 闈欐€佺紦瀛橈紙鍙嶅皠寮€閿€浼樺寲锛?
    // 鍙弽灏勪竴娆★紝闄嶄綆璇诲啓棰戠巼杈冮珮鍦烘櫙鐨勬垚鏈?
    // 浣跨敤 GetProperties(true) 璧?DefaultReflect 缂撳瓨锛屽悓鏃惰繃婊?XmlIgnore/IgnoreDataMember 绛夌壒鎬?
    // AOT: 浣跨敤鏍囧噯鍙嶅皠 GetProperties锛岀敱 [DynamicallyAccessedMembers] 淇濊瘉 AOT 瀹夊叏
    private static readonly IList<PropertyInfo> _properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
    // 缁熶竴浣跨敤搴忓垪鍖栧悕锛堝彲鑳芥潵鑷壒鎬э級锛屼慨澶嶅師鍏堝ご閮ㄥ啓鍏ヤ娇鐢ㄥ睘鎬у悕瀵艰嚧涓庤鍙栦笉涓€鑷寸殑缂洪櫡
    private static readonly String[] _propertyNames = _properties.Select(SerialHelper.GetName).ToArray();
    // 灞炴€у悕鍒扮储寮曠殑鏄犲皠锛岀敤浜庡揩閫熸煡鎵?
    private static readonly Dictionary<String, Int32> _propertyIndexes = BuildPropertyIndexes();

    private static Dictionary<String, Int32> BuildPropertyIndexes()
    {
        var dic = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _propertyNames.Length; i++)
        {
            dic[_propertyNames[i]] = i;
        }
        return dic;
    }
    #endregion

    #region 灞炴€?
    /// <summary>鏂囦欢鍚嶃€侰sv鏁版嵁鏂囦欢璺緞</summary>
    public String? FileName { get; set; }

    /// <summary>鏂囦欢缂栫爜銆傞粯璁TF8</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>瀹炰綋姣旇緝鍣ㄣ€傜敤浜庡垽鏂袱涓疄浣撴槸鍚︾浉绛?/summary>
    public IEqualityComparer<T> Comparer { get; set; }
    #endregion

    #region 鏋勯€?
    /// <summary>瀹炰緥鍖朇sv鏂囦欢鏁版嵁搴?/summary>
    public CsvDb() => Comparer = EqualityComparer<T>.Default;

    /// <summary>浣跨敤鑷畾涔夋瘮杈冨櫒瀹炰緥鍖朇sv鏂囦欢鏁版嵁搴?/summary>
    /// <param name="comparer">鑷畾涔夋瘮杈冨櫒濮旀墭锛岀敤浜庡垽鏂袱瀹炰綋鏄惁鐩哥瓑</param>
    public CsvDb(Func<T?, T?, Boolean> comparer) => Comparer = new MyComparer(comparer);

    /// <summary>閿€姣佹椂鑷姩鎻愪氦鏈彁浜や簨鍔?/summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);

        // 鑻ヤ粛鏈夌紦瀛樹簨鍔★紝鑷姩鎻愪氦锛堜繚鎸佸巻鍙插吋瀹硅涓猴級
        Commit();
    }
    #endregion

    #region 鍩虹鏂规硶锛堜簨鍔★級
    private List<T>? _cache;
    /// <summary>寮€鍚簨鍔★紝渚夸簬鎵归噺澶勭悊鏁版嵁銆傝鍙栧叏閮ㄦ暟鎹繘鍏ュ唴瀛橈紝鍚庣画 Add/Remove/Set 浠呮搷浣滅紦瀛樸€?/summary>
    public void BeginTransaction() => _cache ??= FindAll().ToList();

    /// <summary>鎻愪氦浜嬪姟锛屾妸缂撳瓨鏁版嵁鍐欏叆纾佺洏锛堣鐩栧師鏂囦欢锛夈€傛彁浜ゅ悗娓呯┖缂撳瓨銆?/summary>
    public void Commit()
    {
        if (_cache == null) return;

        Write(_cache, false);
        _cache = null;
    }

    /// <summary>鍥炴粴浜嬪姟锛屾斁寮冪紦瀛樼殑鍏ㄩ儴淇敼锛屼笉鍐欏洖纾佺洏銆?/summary>
    public void Rollback() => _cache = null;
    #endregion

    #region 娣诲垹鏀规煡
    /// <summary>鎵归噺鍐欏叆鏁版嵁锛堥珮鎬ц兘锛?/summary>
    /// <param name="models">瑕佸啓鍏ョ殑鏁版嵁</param>
    /// <param name="append">鏄惁闄勫姞鍦ㄥ熬閮ㄣ€備负 false 鏃朵粠澶村啓鍏ワ紝瑕嗙洊宸叉湁鏁版嵁</param>
    public void Write(IEnumerable<T> models, Boolean append)
    {
        if (append && (models is ICollection<T> collection && collection.Count == 0)) return;

        var file = GetFile();
        file.EnsureDirectory(true);

        using var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        if (append) fs.Position = fs.Length;

        using var csv = new CsvFile(fs, true) { Encoding = Encoding };

        // 棣栨鍐欏叆鏂囦欢澶淬€傞渶瑕佹纭鐞嗗崗鍙橀€嗗彉闂锛屽吋瀹?NET2.0
        if (fs.Position == 0) csv.WriteLine(_propertyNames);

        // 鍐欏叆鏁版嵁
        foreach (var item in models)
        {
            if (item is IModel src)
                csv.WriteLine(_properties.Select(e => src[e.Name]));
            else if (item != null)
                csv.WriteLine(_properties.Select(e => e.GetValue(item)));
        }

        // 鎴柇鍘熸湁澶氫綑鍐呭锛堣鐩栧啓鍦烘櫙锛?
        fs.SetLength(fs.Position);
        fs.Flush();
    }

    /// <summary>灏鹃儴鎻掑叆鏁版嵁锛屾€ц兘鏋佸ソ</summary>
    /// <param name="model"></param>
    public void Add(T model)
    {
        if (_cache != null)
            _cache.Add(model);
        else
            Write([model], true);
    }

    /// <summary>灏鹃儴鎻掑叆鏁版嵁锛屾€ц兘鏋佸ソ</summary>
    /// <param name="models"></param>
    public void Add(IEnumerable<T> models)
    {
        if (_cache != null)
            _cache.AddRange(models);
        else
            Write(models, true);
    }

    /// <summary>鍒犻櫎鏁版嵁锛屾€ц兘寰堝樊锛屽叏閮ㄨ鍙栧墧闄ゅ悗淇濆瓨</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public Int32 Remove(T model) => Remove([model]);

    /// <summary>鍒犻櫎鏁版嵁锛屾€ц兘寰堝樊锛屽叏閮ㄨ鍙栧墧闄ゅ悗淇濆瓨</summary>
    /// <param name="models"></param>
    /// <returns></returns>
    public Int32 Remove(IEnumerable<T> models)
    {
        if (models == null) return 0;
        if (Comparer == null) throw new ArgumentNullException(nameof(Comparer));

        var arr = models as ICollection<T> ?? models.ToList();
        if (arr.Count == 0) return 0;

        return Remove(x => arr.Any(y => Comparer.Equals(x, y)));
    }

    /// <summary>鍒犻櫎婊¤冻鏉′欢鐨勬暟鎹紝鎬ц兘寰堝樊锛屽叏閮ㄨ鍙栧墧闄ゅ悗淇濆瓨</summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public Int32 Remove(Func<T, Boolean> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        lock (this)
        {
            if (_cache != null) return _cache.RemoveAll(x => predicate(x));

            var list = FindAll();
            if (list.Count == 0) return 0;

            var count = list.Count;
            list = list.Where(x => !predicate(x)).ToList();

            // 鍒犻櫎鏂囦欢锛岄噸鏂板啓鍥炲幓
            if (list.Count < count)
            {
                // 濡傛灉娌℃湁浜嗘暟鎹紝鍙啓澶撮儴
                Write(list, false);
            }

            return count - list.Count;
        }
    }

    /// <summary>娓呯┖鏁版嵁銆傚彧鍐欏ご閮?/summary>
    public void Clear()
    {
        if (_cache != null)
            _cache.Clear();
        else
            Write([], false);
    }

    /// <summary>鏇存柊鎸囧畾鏁版嵁琛岋紝鎬ц兘寰堝樊锛屽叏閮ㄨ鍙栨浛鎹㈠悗淇濆瓨</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public Boolean Update(T model) => Set(model, false);

    /// <summary>璁剧疆锛堟坊鍔犳垨鏇存柊锛夋寚瀹氭暟鎹锛屾€ц兘寰堝樊锛屽叏閮ㄨ鍙栨浛鎹㈠悗淇濆瓨</summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public Boolean Set(T model) => Set(model, true);

    private Boolean Set(T model, Boolean add)
    {
        if (Comparer == null) throw new ArgumentNullException(nameof(Comparer));

        lock (this)
        {
            var list = _cache ?? FindAll();
            if (!add && list.Count == 0) return false;

            // 鎵惧埌鐩爣鏁版嵁琛岋紝骞舵浛鎹?
            var flag = false;
            for (var i = 0; i < list.Count; i++)
            {
                if (Comparer.Equals(model, list[i]))
                {
                    list[i] = model;
                    flag = true;
                    break;
                }
            }
            if (!flag)
            {
                if (!add) return false;

                list.Add(model);
            }

            // 閲嶆柊鍐欏洖鍘?
            if (_cache == null)
            {
                Write(list, false);
            }

            return true;
        }
    }

    /// <summary>鏌ユ壘鎸囧畾鏁版嵁琛?/summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public T? Find(T model)
    {
        if (Comparer == null) throw new ArgumentNullException(nameof(Comparer));

        return Query(e => Comparer.Equals(model, e), 1).FirstOrDefault();
    }

    /// <summary>鑾峰彇婊¤冻鏉′欢鐨勭涓€琛屾暟鎹?/summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public T? Find(Func<T, Boolean>? predicate) => Query(predicate, 1).FirstOrDefault();

    /// <summary>鑾峰彇鎵€鏈夋暟鎹</summary>
    /// <returns></returns>
    public IList<T> FindAll() => _cache?.ToList() ?? Query(null).ToList();

    /// <summary>鑾峰彇婊¤冻鏉′欢鐨勬暟鎹锛屾€ц兘濂斤紝椤哄簭鏌ユ壘</summary>
    /// <param name="predicate"></param>
    /// <param name="count">鏈€澶氳繑鍥炶鏁般€傞粯璁?-1 琛ㄧず涓嶉檺鍒?/param>
    /// <returns></returns>
    public IEnumerable<T> Query(Func<T, Boolean>? predicate, Int32 count = -1)
    {
        // 寮€鍚簨鍔℃椂锛岀洿鎺ヨ繑鍥炵紦瀛樻暟鎹?
        if (_cache != null)
        {
            foreach (var item in _cache)
            {
                if (predicate == null || predicate(item))
                {
                    yield return item;

                    if (--count == 0) break;
                }
            }
            yield break;
        }

        var file = GetFile();
        if (!File.Exists(file)) yield break;

        lock (this)
        {
            using var csv = new CsvFile(file, false) { Encoding = Encoding };

            // 鏂囦欢鍒楀悕鍒扮储寮曠殑鏄犲皠
            var headers = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
            // 鏂囦欢鍒楃储寮曞埌灞炴€х储寮曠殑鏄犲皠锛岄伩鍏嶆瘡琛岄兘鏌ュ瓧鍏?
            var columnToProperty = (Int32[]?)null;

            while (true)
            {
                var ss = csv.ReadLine();
                if (ss == null) break;

                // 澶撮儴锛屽悕绉颁笌搴忓彿瀵瑰簲
                if (headers.Count == 0)
                {
                    for (var i = 0; i < ss.Length; i++)
                    {
                        // 閬垮厤閲嶅閿紓甯革紱蹇界暐閲嶅鍒?
                        if (!headers.ContainsKey(ss[i])) headers[ss[i]] = i;
                    }
                    // 鏃犳硶璇嗗埆鎵€鏈夊瓧娈?
                    if (headers.Count == 0) break;

                    // 寤虹珛鏂囦欢鍒楀埌灞炴€х殑鏄犲皠
                    columnToProperty = new Int32[ss.Length];
                    for (var i = 0; i < ss.Length; i++)
                    {
                        columnToProperty[i] = _propertyIndexes.TryGetValue(ss[i], out var idx) ? idx : -1;
                    }
                }
                else
                {
                    var flag = false;
                    var model = new T();
                    try
                    {
                        // 鍙嶅皠鏋勫缓瀵硅薄
                        var success = 0;
                        for (var i = 0; i < ss.Length && i < columnToProperty!.Length; i++)
                        {
                            var propIdx = columnToProperty[i];
                            if (propIdx < 0) continue;

                            var pi = _properties[propIdx];
                            if (!pi.CanWrite) continue;

                            var raw = ss[i];
                            if (raw == null) continue;

                            // 閮ㄥ垎鍩虹绫诲瀷鍒ゆ柇鏁版嵁鏈夋晥鎬?
                            if (pi.PropertyType.IsInt() && !Int64.TryParse(raw, out _)) continue;
                            var code = Type.GetTypeCode(pi.PropertyType);
                            switch (code)
                            {
                                case TypeCode.Single:
                                case TypeCode.Double:
                                    if (!Double.TryParse(raw, out _)) continue;
                                    break;
                                case TypeCode.Decimal:
                                    if (!Decimal.TryParse(raw, out _)) continue;
                                    break;
                                case TypeCode.DateTime:
                                    if (!DateTime.TryParse(raw, out _)) continue;
                                    break;
                                default:
                                    break;
                            }

                            var value = Convert.ChangeType(raw, pi.PropertyType);

                            if (model is IModel dst)
                                dst[pi.Name] = value;
                            else
                                pi.SetValue(model, value);

                            success++;
                        }

                        // 娌℃湁浠讳綍瀛楁鎴愬姛鍖归厤锛岃涓烘崯鍧忚
                        if (success == 0) continue;

                        if (predicate == null || predicate(model))
                        {
                            flag = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 璇诲彇鏌愪竴琛屽嚭閿欙紝鏀惧純璇ヨ
                        XTrace.WriteException(ex);
                        continue;
                    }

                    if (!flag) continue;

                    yield return model;

                    if (--count == 0) break;
                }
            }
        }
    }

    /// <summary>鑾峰彇鏁版嵁琛屾暟锛屾€ц兘杈冨ソ锛岀粺璁℃枃浠惰鏁帮紙闄ゅご閮級</summary>
    /// <returns></returns>
    public Int32 FindCount()
    {
        if (_cache != null) return _cache.Count;

        lock (this)
        {
            var file = GetFile();
            if (!File.Exists(file)) return 0;

            // 閫愯璇诲彇缁熻锛岄伩鍏嶄竴娆℃€у姞杞藉叏閮ㄥ唴瀹瑰埌鍐呭瓨
            var line = 0;
            using var sr = new StreamReader(file, Encoding);

            if (sr.ReadLine() == null) return 0; // 璺宠繃澶撮儴锛屼笉瀛樺湪鍒欒繑鍥?0

            while (sr.ReadLine() != null) line++;

            return line;
        }
    }
    #endregion

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    #region 寮傛鏂规硶
    /// <summary>寮傛鑾峰彇婊¤冻鏉′欢鐨勬暟鎹</summary>
    /// <param name="predicate">杩囨护鏉′欢</param>
    /// <param name="count">鏈€澶氳繑鍥炶鏁般€傞粯璁?-1 琛ㄧず涓嶉檺鍒?/param>
    /// <returns></returns>
    public async IAsyncEnumerable<T> QueryAsync(Func<T, Boolean>? predicate, Int32 count = -1)
    {
        // 寮€鍚簨鍔℃椂锛岀洿鎺ヨ繑鍥炵紦瀛樻暟鎹?
        if (_cache != null)
        {
            foreach (var item in _cache)
            {
                if (predicate == null || predicate(item))
                {
                    yield return item;

                    if (--count == 0) break;
                }
            }
            yield break;
        }

        var file = GetFile();
        if (!File.Exists(file)) yield break;

        var csv = new CsvFile(file, false) { Encoding = Encoding };
        await using (csv.ConfigureAwait(false))
        {
            // 鏂囦欢鍒楀悕鍒扮储寮曠殑鏄犲皠
            var headers = new Dictionary<String, Int32>(StringComparer.OrdinalIgnoreCase);
            // 鏂囦欢鍒楃储寮曞埌灞炴€х储寮曠殑鏄犲皠
            var columnToProperty = (Int32[]?)null;

            await foreach (var ss in csv.ReadAllAsync().ConfigureAwait(false))
            {
                // 澶撮儴锛屽悕绉颁笌搴忓彿瀵瑰簲
                if (headers.Count == 0)
                {
                    for (var i = 0; i < ss.Length; i++)
                    {
                        if (!headers.ContainsKey(ss[i])) headers[ss[i]] = i;
                    }
                    if (headers.Count == 0) break;

                    columnToProperty = new Int32[ss.Length];
                    for (var i = 0; i < ss.Length; i++)
                    {
                        columnToProperty[i] = _propertyIndexes.TryGetValue(ss[i], out var idx) ? idx : -1;
                    }
                    continue;
                }

                var flag = false;
                var model = new T();
                try
                {
                    var success = 0;
                    for (var i = 0; i < ss.Length && i < columnToProperty!.Length; i++)
                    {
                        var propIdx = columnToProperty[i];
                        if (propIdx < 0) continue;

                        var pi = _properties[propIdx];
                        if (!pi.CanWrite) continue;

                        var raw = ss[i];
                        if (raw == null) continue;

                        if (pi.PropertyType.IsInt() && !Int64.TryParse(raw, out _)) continue;
                        var code = Type.GetTypeCode(pi.PropertyType);
                        switch (code)
                        {
                            case TypeCode.Single:
                            case TypeCode.Double:
                                if (!Double.TryParse(raw, out _)) continue;
                                break;
                            case TypeCode.Decimal:
                                if (!Decimal.TryParse(raw, out _)) continue;
                                break;
                            case TypeCode.DateTime:
                                if (!DateTime.TryParse(raw, out _)) continue;
                                break;
                            default:
                                break;
                        }

                        var value = Convert.ChangeType(raw, pi.PropertyType);

                        if (model is IModel dst)
                            dst[pi.Name] = value;
                        else
                            pi.SetValue(model, value);

                        success++;
                    }

                    if (success == 0) continue;

                    if (predicate == null || predicate(model))
                    {
                        flag = true;
                    }
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                    continue;
                }

                if (!flag) continue;

                yield return model;

                if (--count == 0) break;
            }
        }
    }

    /// <summary>寮傛鑾峰彇鎵€鏈夋暟鎹</summary>
    /// <returns></returns>
    public async Task<IList<T>> FindAllAsync()
    {
        if (_cache != null) return _cache.ToList();

        var list = new List<T>();
        await foreach (var item in QueryAsync(null).ConfigureAwait(false))
        {
            list.Add(item);
        }
        return list;
    }
    #endregion
#endif

    #region 杈呭姪
    private String GetFile()
    {
        if (FileName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(FileName));

        return FileName.GetFullPath();
    }

    private class MyComparer(Func<T?, T?, Boolean> comparer) : IEqualityComparer<T>
    {
        public Func<T?, T?, Boolean> Comparer = comparer;

        public Boolean Equals(T? x, T? y) => Comparer(x, y);

        public Int32 GetHashCode(T? obj) => obj?.GetHashCode() ?? 0;
    }
    #endregion
}
