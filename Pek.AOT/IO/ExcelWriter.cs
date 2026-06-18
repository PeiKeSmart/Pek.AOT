using System.Globalization;

using Pek.Extension;
using System.IO.Compression;

using Pek.Extension;
using System.Security;

using Pek.Extension;
using System.Text;

using Pek.Extension;
using Pek.Collections;

namespace Pek.IO;

/// <summary>杞婚噺绾xcel鍐欏叆鍣紝鏀寔澶氫釜宸ヤ綔琛?/summary>
/// <remarks>
/// 鐩爣锛氬揩閫熷鍑虹畝鍗曟暟鎹紝鏀寔澶氬伐浣滆〃鐨勫垪澶翠笌澶氳鏁版嵁锛涜瘑鍒父瑙佹暟鎹被鍨嬪苟浣跨敤鍚堥€傛牱寮忥紝閬垮厤闀挎暟瀛楋紙濡傝韩浠借瘉銆侀暱鏁村瀷锛夎 Excel / WPS 鏄剧ず涓虹瀛﹁鏁般€?
/// 浠呯敓鎴愭渶灏忓繀瑕佺殑 xlsx 缁撴瀯锛欳ontentTypes / workbook / worksheets / styles / sharedStrings锛堜互鍙婅鑼冭姹傜殑鍏崇郴 _rels/.rels銆亁l/_rels/workbook.xml.rels锛夈€?
/// 涓嶆敮鎸侊細鍚堝苟鍗曞厓鏍笺€佸瘜鏂囨湰銆佽秴閾炬帴銆佸叕寮忕瓑楂樼骇鐗规€с€傝鍙栧彲浣跨敤 <see cref="ExcelReader"/>銆?
/// </remarks>
public class ExcelWriter : DisposeBase
{
    #region 鍐呴儴绫诲瀷
    /// <summary>鍗曞厓鏍兼牱寮忥紙鍊间负 Excel 鍐呯疆 numFmtId锛夈€?/summary>
    private enum ExcelCellStyle : Int32
    {
        General = 0,  // General
        Integer = 1,  // 0 锛堟暣鏁帮紝閬垮厤闀挎暣鍨嬩娇鐢ㄧ瀛﹁鏁帮級
        Decimal = 2,  // 0.00
        Percent = 10, // 0.00%
        Date = 14,    // mm-dd-yy
        Time = 21,    // h:mm:ss
        DateTime = 22 // m/d/yy h:mm
    }

    private static readonly ExcelCellStyle[] _cellStyles = (ExcelCellStyle[])Enum.GetValues(typeof(ExcelCellStyle));
    #endregion

    #region 灞炴€?
    /// <summary>鏂囦欢璺緞锛圫ave 鏃跺啓鍏ワ級</summary>
    public String? FileName { get; }

    /// <summary>鐩爣娴侊紙鑻ユ彁渚涘垯鍐欏叆璇ユ祦锛岃皟鐢ㄦ柟璐熻矗鐢熷懡鍛ㄦ湡锛?/summary>
    public Stream? Stream { get; }

    /// <summary>榛樿宸ヤ綔琛ㄥ悕绉帮紙褰撹皟鐢?API 鏈寚瀹?sheet 鏃朵娇鐢級</summary>
    public String SheetName { get; set; } = "Sheet1";

    /// <summary>鏂囨湰缂栫爜</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>瓒呰繃璇ユ暟瀛楁湁鏁堜綅鏁伴槇鍊硷紙鎴栨瀬灏忓€兼湁澶ч噺鍓嶅0灏忔暟锛夊垯鍐欎负鏂囨湰浠ラ伩鍏嶇瀛﹁鏁版硶銆傞粯璁?11銆?/summary>
    private const Int32 LongNumberAsTextThreshold = 11;

    /// <summary>鏄惁鑷姩鏍规嵁鏁版嵁鍐呭浼扮畻鍒楀锛屽苟鍐欏叆 <c>&lt;cols&gt;</c> 鏉ラ伩鍏?WPS/Excel 鍑虹幇########銆傞粯璁?true銆?/summary>
    public Boolean AutoFitColumnWidth { get; set; } = true;

    // 澶?sheet锛氫繚鎸佹彃鍏ラ『搴忥紝鍐?workbook.xml 鏃剁敤浜?sheetId 椤哄簭
    private readonly List<String> _sheetNames = [];
    private readonly Dictionary<String, List<String>> _sheetRows = new(StringComparer.OrdinalIgnoreCase); // sheet -> 琛孹ML闆嗗悎
    private readonly Dictionary<String, Int32> _sheetRowIndex = new(StringComparer.OrdinalIgnoreCase);     // sheet -> 褰撳墠琛屽彿锛?鍩猴級

    // 姣忎釜 sheet 鐨勫垪鏈€澶ф樉绀哄搴︼紙瀛楃鏁颁及绠楋級锛屼笅鏍?0 鍩猴紝瀵瑰簲 Excel 鍒?1 鍩?
    private readonly Dictionary<String, List<Double>> _sheetColWidths = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<String, Int32> _shared = new(StringComparer.Ordinal); // 鍏变韩瀛楃涓插幓閲?
    private Int32 _sharedCount; // 鎬诲紩鐢ㄦ鏁帮紙鍚噸澶嶏級
    #endregion

    #region 鏋勯€?
    /// <summary>浣跨敤鏂囦欢璺緞瀹炰緥鍖栧啓鍏ュ櫒</summary>
    /// <param name="fileName">鐩爣 xlsx 鏂囦欢</param>
    public ExcelWriter(String fileName) => FileName = fileName.GetFullPath();

    /// <summary>浣跨敤澶栭儴娴佸疄渚嬪寲鍐欏叆鍣?/summary>
    /// <param name="stream">鐩爣鍙啓娴?/param>
    public ExcelWriter(Stream stream) => Stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>閿€姣侀噴鏀?/summary>
    /// <param name="disposing"></param>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        if (Stream == null) Save();
    }
    #endregion

    #region 鍐欏叆鎺ュ彛
    /// <summary>鍐欏叆鍒楀ご鍒版寚瀹氬伐浣滆〃</summary>
    /// <param name="sheet">宸ヤ綔琛ㄥ悕绉帮紙鍙┖锛岀┖鏃朵娇鐢?<see cref="SheetName"/>锛?/param>
    /// <param name="headers">鍒楀ご鏂囨湰闆嗗悎</param>
    public void WriteHeader(String sheet, IEnumerable<String> headers)
    {
        if (sheet.IsNullOrEmpty()) sheet = SheetName;
        if (headers == null) throw new ArgumentNullException(nameof(headers));

        EnsureSheet(sheet);

        var arr = headers as String[] ?? headers.ToArray();
        AddRow(sheet, arr.Select(e => (Object?)e).ToArray());
    }

    /// <summary>鍐欏叆澶氳鏁版嵁鍒版寚瀹氬伐浣滆〃</summary>
    /// <param name="sheet">宸ヤ綔琛ㄥ悕绉帮紙鍙┖锛岀┖鏃朵娇鐢?<see cref="SheetName"/>锛?/param>
    /// <param name="data">鏁版嵁闆嗗悎锛屾瘡琛屼竴涓璞℃暟缁?/param>
    public void WriteRows(String? sheet, IEnumerable<Object?[]> data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        if (sheet.IsNullOrEmpty())
            sheet = SheetName;
        else
            SheetName = sheet; // 鍚屾榛樿鍊间负鏈€杩戜娇鐢?

        EnsureSheet(sheet);

        foreach (var row in data)
        {
            AddRow(sheet, row);
        }
    }

    /// <summary>鎵嬪伐璁剧疆鍒楀锛堝瓧绗﹀搴︼紝杩戜技锛夛紝0 鍩哄垪搴忓彿銆傞渶鍦?Save 涔嬪墠璋冪敤銆?/summary>
    public void SetColumnWidth(String? sheet, Int32 columnIndex, Double width)
    {
        if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));
        if (sheet.IsNullOrEmpty()) sheet = SheetName;
        EnsureSheet(sheet!);

        var list = _sheetColWidths[sheet!];
        while (list.Count <= columnIndex) list.Add(0);
        if (width > list[columnIndex]) list[columnIndex] = width;
    }
    #endregion

    #region 鍐呴儴鍐欏叆
    private void EnsureSheet(String sheet)
    {
        if (!_sheetRows.ContainsKey(sheet))
        {
            _sheetRows[sheet] = [];
            _sheetRowIndex[sheet] = 0;
            _sheetNames.Add(sheet);
            _sheetColWidths[sheet] = [];
        }
    }

    private void AddRow(String sheet, Object?[]? values)
    {
        EnsureSheet(sheet);

        var rowIndex = ++_sheetRowIndex[sheet];
        values ??= [];

        var sb = Pool.StringBuilder.Get();
        sb.Append("<row r=\"").Append(rowIndex).Append("\">");

        for (var i = 0; i < values.Length; i++)
        {
            var val = values[i];
            if (val == null) continue; // 缂哄け鍒楋細瑙ｆ瀽鏃惰嚜鍔ㄨˉ null

            var cellRef = GetColumnName(i) + rowIndex; // A1 / B2 ...

            // 璇嗗埆绫诲瀷
            var style = ExcelCellStyle.General;
            String? tAttr = null; // t="s" / "b"
            String? inner = null; // <v>鍊?/v>
            var displayLen = 0;   // 浼扮畻鏄剧ず闀垮害鐢ㄤ簬鍒楀

            switch (val)
            {
                case String str:
                    {
                        // 鐧惧垎姣旓細褰㈠ "12.3%" / "45%"
                        if (str.Length > 0 && str.EndsWith("%") && TryParsePercent(str, out var pct))
                        {
                            style = ExcelCellStyle.Percent;
                            inner = (pct / 100).ToString("0.##########", CultureInfo.InvariantCulture);
                            //displayLen = inner.Length + 1;
                            break;
                        }
                        else
                        {
                            // 鏅€氬瓧绗︿覆璧板叡浜瓧绗︿覆锛屽噺灏戜綋绉?& 閬垮厤琚帹鏂?
                            tAttr = "s";
                            inner = GetSharedStringIndex(str).ToString();
                        }
                        break;
                    }
                case Boolean b:
                    {
                        tAttr = "b";
                        inner = b ? "1" : "0";
                        //displayLen = 5;
                        break;
                    }
                case DateTime dt:
                    {
                        var baseDate = new DateTime(1900, 1, 1);
                        if (dt < baseDate)
                        {
                            // Excel 鏃犳硶琛ㄧず 1900-01-01 涔嬪墠锛堟垨鏃犳晥锛夋棩鏈燂紝杩欓噷鍐欏叆绌哄瓧绗︿覆
                            tAttr = "s";
                            inner = GetSharedStringIndex(String.Empty).ToString();
                            break;
                        }
                        // Excel 搴忓垪鍊硷細1=1900/1/1锛堝惈闂板勾Bug锛夛紝璇诲彇鏃跺噺2锛岃繖閲屽啓鍏ラ渶琛?
                        var serial = (dt - baseDate).TotalDays + 2; // 鍖呭惈鏃堕棿灏忔暟
                        var hasTime = dt.TimeOfDay.Ticks != 0;
                        style = hasTime ? ExcelCellStyle.DateTime : ExcelCellStyle.Date;
                        inner = serial.ToString("0.###############", CultureInfo.InvariantCulture);
                        // 涓洪伩鍏?WPS 鏄剧ず ########锛岃繖閲屾寜甯歌瀹屾暣鏍煎紡闀垮害浼扮畻锛歽yyy-MM-dd 鎴?yyyy-MM-dd HH:mm:ss
                        //displayLen = hasTime ? 16 - 1 : 10 - 1;
                        displayLen = hasTime ? 14 : 0;
                        break;
                    }
                case TimeSpan ts:
                    style = ExcelCellStyle.Time;
                    inner = ts.TotalDays.ToString("0.###############", CultureInfo.InvariantCulture);
                    //displayLen = inner.Length;
                    break;
                case Int16 or Int32 or Int64 or Byte or SByte or UInt16 or UInt32 or UInt64:
                    {
                        // 濡傛灉澶暱锛屼负浜嗛伩鍏嶅嚭鐜扮瀛﹁鏁版硶锛屾敼鐢ㄥ瓧绗︿覆琛ㄧず
                        var numStr = Convert.ToString(val, CultureInfo.InvariantCulture)!;
                        if (ShouldWriteAsText(numStr, 15))
                        {
                            tAttr = "s";
                            inner = GetSharedStringIndex(numStr).ToString();
                        }
                        else
                        {
                            style = ExcelCellStyle.Integer;
                            inner = numStr; // 浣跨敤 General锛岄伩鍏嶄袱浣嶆埅鏂?
                        }
                        displayLen = numStr.Length < 8 ? 0 : numStr.Length;
                        break;
                    }
                case Decimal dec:
                    {
                        var numStr = dec.ToString(CultureInfo.InvariantCulture);
                        if (ShouldWriteAsText(numStr, LongNumberAsTextThreshold))
                        {
                            tAttr = "s";
                            inner = GetSharedStringIndex(numStr).ToString();
                        }
                        else
                        {
                            inner = numStr; // 浣跨敤 General锛岄伩鍏嶄袱浣嶆埅鏂?
                        }
                        displayLen = numStr.Length < 8 ? 0 : numStr.Length;
                        break;
                    }
                case Double d:
                    {
                        var numStr = d.ToString("0.###############", CultureInfo.InvariantCulture);
                        if (ShouldWriteAsText(numStr, LongNumberAsTextThreshold))
                        {
                            tAttr = "s";
                            inner = GetSharedStringIndex(numStr).ToString();
                        }
                        else
                        {
                            inner = numStr; // General
                        }
                        displayLen = numStr.Length < 8 ? 0 : numStr.Length;
                        break;
                    }
                case Single f:
                    {
                        var numStr = f.ToString("0.###############", CultureInfo.InvariantCulture);
                        if (ShouldWriteAsText(numStr, LongNumberAsTextThreshold))
                        {
                            tAttr = "s";
                            inner = GetSharedStringIndex(numStr).ToString();
                        }
                        else
                        {
                            inner = numStr; // General
                        }
                        displayLen = numStr.Length < 8 ? 0 : numStr.Length;
                        break;
                    }
                default:
                    {
                        // 鍏跺畠绫诲瀷璋冪敤 ToString() 鍚庢寜瀛楃涓插鐞?
                        var str = val + "";
                        tAttr = "s";
                        inner = GetSharedStringIndex(str).ToString();
                        break;
                    }
            }

            sb.Append("<c r=\"").Append(cellRef).Append('"');
            if (tAttr != null) sb.Append(' ').Append("t=\"").Append(tAttr).Append('"');

            // 鑻ユ槸闈炲叡浜瓧绗︿覆/甯冨皵锛堝嵆 tAttr==null锛夛紝缁熶竴鍐欏叆鏍峰紡灞炴€э紙General / 鏃ユ湡/鏃堕棿绛夛級
            if (tAttr == null)
            {
                // 渚濇嵁鏋氫妇鏁板€煎崌搴忕‘瀹氱储寮曪紙鍙嶅皠鐢熸垚 styles.xml 鏃朵娇鐢ㄧ浉鍚岄『搴忥級
                var index = Array.IndexOf(_cellStyles, style);
                sb.Append(' ').Append("s=\"").Append(index).Append('"');
            }
            sb.Append("><v>").Append(inner).Append("</v></c>");

            // 鑷姩鍒楀
            if (AutoFitColumnWidth && displayLen > 0)
            {
                var list = _sheetColWidths[sheet];
                while (list.Count <= i) list.Add(0);
                // Excel 鍒楀锛氬瓧绗︽暟 + 2 杈硅窛锛堢矖鐣ワ級锛岄檺鍒舵渶澶у€奸€傚害锛堝 80锛?
                var w = displayLen + 2; // 缁忛獙鍊?
                if (w > 80) w = 80;
                if (w > list[i]) list[i] = w;
            }
        }

        sb.Append("</row>");
        _sheetRows[sheet].Add(sb.Return(true));
    }

    /// <summary>鍒ゆ柇涓€涓暟鍊煎瓧绗︿覆鏄惁搴旇浆涓烘枃鏈互閬垮厤琚?Excel 鑷姩鏄剧ず涓虹瀛﹁鏁版硶銆?/summary>
    private static Boolean ShouldWriteAsText(String numStr, Int32 maxLength)
    {
        if (numStr.IsNullOrEmpty()) return false;

        var digits = 0;
        for (var i = 0; i < numStr.Length; i++)
        {
            var ch = numStr[i];
            if (ch >= '0' && ch <= '9') digits++;
        }
        if (digits > maxLength) return true;         // 鏈夋晥鏁板瓧杩囬暱锛?11锛?
        if (numStr.StartsWith("0.0000000")) return true;            // 寰堝皬鐨勬暟鍊硷紙澶ч噺鍓嶅0锛?
        return false;
    }

    private static Boolean TryParsePercent(String str, out Decimal value)
    {
        value = 0m;
        var txt = str.Trim().TrimEnd('%');
        if (Decimal.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
        return false;
    }

    private Int32 GetSharedStringIndex(String str)
    {
        _sharedCount++;
        if (_shared.TryGetValue(str, out var idx)) return idx;
        idx = _shared.Count;
        _shared[str] = idx;
        return idx;
    }

    private static String GetColumnName(Int32 index)
    {
        // 0 -> A
        index++; // 杞负 1 鍩?
        var sb = Pool.StringBuilder.Get();
        while (index > 0)
        {
            var mod = (index - 1) % 26;
            sb.Insert(0, (Char)('A' + mod));
            index = (index - 1) / 26;
        }
        return sb.Return(true);
    }
    #endregion

    #region 淇濆瓨
    /// <summary>淇濆瓨鍒版枃浠舵垨鐩爣娴?/summary>
    public void Save()
    {
        // 鑻ユ湭鍐欎换浣?sheet锛屽垱寤轰竴涓┖鐨勯粯璁ゅ伐浣滆〃锛岄伩鍏嶇敓鎴愰潪娉?workbook
        if (_sheetNames.Count == 0) EnsureSheet(SheetName);

        var target = Stream;
        if (target == null)
        {
            if (FileName.IsNullOrEmpty()) throw new InvalidOperationException("未指定输出位置");

            var file = FileName.EnsureDirectory(true).GetFullPath();
            target = new FileStream(file, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        using var za = new ZipArchive(target, ZipArchiveMode.Create, leaveOpen: Stream != null, entryNameEncoding: Encoding);

        // _rels/.rels
        using (var sw = new StreamWriter(za.CreateEntry("_rels/.rels").Open(), Encoding))
        {
            sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        }

        // [Content_Types].xml
        using (var sw = new StreamWriter(za.CreateEntry("[Content_Types].xml").Open(), Encoding))
        {
            sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sw.Write("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            for (var i = 0; i < _sheetNames.Count; i++)
            {
                sw.Write("<Override PartName=\"/xl/worksheets/sheet");
                sw.Write(i + 1);
                sw.Write(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            if (_shared.Count > 0)
            {
                sw.Write("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            }
            sw.Write("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            sw.Write("</Types>");
        }

        // workbook.xml
        using (var sw = new StreamWriter(za.CreateEntry("xl/workbook.xml").Open(), Encoding))
        {
            sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (var i = 0; i < _sheetNames.Count; i++)
            {
                var name = SecurityElement.Escape(_sheetNames[i]) ?? _sheetNames[i];
                sw.Write($"<sheet name=\"{name}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            sw.Write("</sheets></workbook>");
        }

        // workbook 鍏崇郴
        using (var sw = new StreamWriter(za.CreateEntry("xl/_rels/workbook.xml.rels").Open(), Encoding))
        {
            sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 0; i < _sheetNames.Count; i++) sw.Write($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
            var nextId = _sheetNames.Count + 1;
            sw.Write($"<Relationship Id=\"rId{nextId++}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            if (_shared.Count > 0) sw.Write($"<Relationship Id=\"rId{nextId}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            sw.Write("</Relationships>");
        }

        // styles.xml 锛堟寜鏋氫妇鏁板€煎崌搴忥級
        using (var sw = new StreamWriter(za.CreateEntry("xl/styles.xml").Open(), Encoding))
        {
            sw.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cellXfs count=\"{_cellStyles.Length}\">");
            foreach (var st in _cellStyles)
            {
                sw.Write($"<xf numFmtId=\"{(Int32)st}\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>");
            }
            sw.Write("</cellXfs></styleSheet>");
        }

        // sharedStrings.xml
        if (_shared.Count > 0)
        {
            using var sw = new StreamWriter(za.CreateEntry("xl/sharedStrings.xml").Open(), Encoding);
            sw.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"{_sharedCount}\" uniqueCount=\"{_shared.Count}\">");
            foreach (var kv in _shared.OrderBy(e => e.Value))
            {
                var txt = SecurityElement.Escape(kv.Key) ?? String.Empty;
                sw.Write("<si><t>");
                sw.Write(txt);
                sw.Write("</t></si>");
            }
            sw.Write("</sst>");
        }

        // worksheets
        for (var i = 0; i < _sheetNames.Count; i++)
        {
            var entry = za.CreateEntry($"xl/worksheets/sheet{i + 1}.xml");
            using var sw = new StreamWriter(entry.Open(), Encoding);
            sw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:xdr=\"http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing\" xmlns:x14=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/main\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" xmlns:etc=\"http://www.wps.cn/officeDocument/2017/etCustomData\">");
            var sheet = _sheetNames[i];
            if (AutoFitColumnWidth && _sheetColWidths.TryGetValue(sheet, out var widths) && widths.Count > 0)
            {
                // 浠呭啓鍏ユ湁鍊肩殑鍒楋紙>0锛?
                if (widths.Any(e => e > 0))
                {
                    sw.Write("<cols>");
                    for (var c = 0; c < widths.Count; c++)
                    {
                        var w = widths[c];
                        if (w <= 0) continue;
                        // Excel 鍒楀鏁板€间负瀛楃瀹藉害杩戜技锛屽彲淇濈暀 2 浣嶅皬鏁?
                        sw.Write($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{w:0.##}\" customWidth=\"1\"/>");
                    }
                    sw.Write("</cols>");
                }
            }
            sw.Write("<sheetData>");
            if (_sheetRows.TryGetValue(sheet, out var list))
            {
                foreach (var r in list) sw.Write(r);
            }
            sw.Write("</sheetData></worksheet>");
        }

        target.Flush();
    }
    #endregion
}
