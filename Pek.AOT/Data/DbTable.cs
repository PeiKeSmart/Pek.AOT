using System.Buffers;
using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Pek.Buffers;
using Pek.Extension;
using Pek.IO;
using Pek.Reflection;
using Pek.Serialization;

namespace Pek.Data;

/// <summary>数据表</summary>
/// <remarks>
/// 文档 https://newlifex.com/core/dbtable
/// </remarks>
public class DbTable : IEnumerable<DbRow>, ICloneable, IAccessor, ISpanSerializable
{
    #region 属性
    /// <summary>数据列</summary>
    public String[] Columns { get; set; } = [];

    /// <summary>数据列类型</summary>
    [XmlIgnore, IgnoreDataMember]
    public Type[] Types { get; set; } = [];

    /// <summary>数据行</summary>
    public IList<Object?[]> Rows { get; set; } = [];

    /// <summary>总行数</summary>
    public Int32 Total { get; set; }
    #endregion

    #region 构造
    #endregion

    #region Span序列化
    private const Byte _Ver = 3;
    private const String MAGIC = "NewLifeDbTable";

    /// <summary>写入到Span写入器</summary>
    /// <param name="writer">Span写入器</param>
    public void Write(ref SpanWriter writer)
    {
        var cs = Columns ?? throw new ArgumentNullException(nameof(Columns));
        var ts = Types ?? throw new ArgumentNullException(nameof(Types));
        var rs = Rows;
        if (Total == 0 && rs != null) Total = rs.Count;

        // 头部，幻数、版本和标记
        writer.Write(MAGIC.GetBytes());
        writer.Write(_Ver);
        writer.Write((Byte)0);

        // 写入列数
        var count = cs.Length;
        writer.WriteEncodedInt(count);
        // 写入列名和类型
        for (var i = 0; i < count; i++)
        {
            writer.Write(cs[i], 0);

            // 复杂类型写入类型字符串
            var code = ts[i].GetTypeCode();
            writer.Write((Byte)code);
            if (code == TypeCode.Object)
                writer.Write(ts[i].FullName, 0);
        }

        // 数据行数
        writer.Write(Total);

        // 写入数据
        if (rs != null)
        {
            foreach (var row in rs)
            {
                for (var i = 0; i < row.Length; i++)
                {
                    SpanSerializer.WriteValue(ref writer, row[i], ts[i]);
                }
            }
        }
    }

    /// <summary>从Span读取器读取</summary>
    /// <param name="reader">Span读取器</param>
    public void Read(ref SpanReader reader)
    {
        // 头部，幻数、版本和标记
        var magicBytes = reader.ReadBytes(MAGIC.Length);
        if (!magicBytes.SequenceEqual(MAGIC.GetBytes()))
            throw new InvalidDataException();

        var ver = reader.ReadByte();
        _ = reader.ReadByte();

        // 版本兼容
        if (ver > _Ver) throw new InvalidDataException($"DbTable[ver={_Ver}] Unable to support newer versions [{ver}]");

        // 读取列数
        var count = reader.ReadEncodedInt();
        var cs = new String[count];
        var ts = new Type[count];

        // 读取列名和类型
        for (var i = 0; i < count; i++)
        {
            cs[i] = reader.ReadString() ?? "";

            // 复杂类型写入类型字符串
            var tc = (TypeCode)reader.ReadByte();
            if (tc != TypeCode.Object)
                ts[i] = Type.GetType("System." + tc) ?? typeof(Object);
            else if (ver >= 2)
                ts[i] = Type.GetType(reader.ReadString() ?? "") ?? typeof(Object);
        }
        Columns = cs;
        Types = ts;

        // 读取行数
        Total = reader.ReadInt32();

        // 读取数据
        var rs = new List<Object?[]>();
        for (var k = 0; k < Total; k++)
        {
            var row = new Object?[count];
            for (var i = 0; i < count; i++)
            {
                row[i] = SpanSerializer.ReadValue(ref reader, ts[i]);
            }
            rs.Add(row);
        }
        Rows = rs;
    }

    /// <summary>从数据流读取（Span序列化格式）</summary>
    /// <param name="stream"></param>
    public Int64 Read(Stream stream)
    {
        var buf = stream.ReadBytes();
        var reader = new SpanReader(buf);
        Read(ref reader);
        return buf.Length;
    }

    /// <summary>写入数据流（Span序列化格式）</summary>
    /// <param name="stream"></param>
    public Int64 Write(Stream stream)
    {
        var buffer = new Byte[8192];
        var writer = new SpanWriter(buffer, stream);
        Write(ref writer);
        writer.Flush();
        return writer.TotalWritten;
    }

    /// <summary>从数据包读取</summary>
    /// <param name="pk"></param>
    /// <returns></returns>
    public Boolean Read(IPacket pk)
    {
        if (pk == null || pk.Length == 0) return false;

        var reader = new SpanReader(pk);
        Read(ref reader);

        return true;
    }

    /// <summary>从字节数组读取</summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public Boolean Read(Byte[] buffer, Int32 offset = 0, Int32 count = -1)
    {
        if (count < 0) count = buffer.Length - offset;

        var reader = new SpanReader(buffer, offset, count);
        Read(ref reader);

        return true;
    }

    /// <summary>从文件加载（Span序列化格式）</summary>
    /// <param name="file">文件路径</param>
    /// <param name="compressed">是否压缩</param>
    /// <returns></returns>
    public Int64 LoadFile(String file, Boolean compressed = false)
    {
        if (compressed)
        {
            using var fs = file.AsFile().OpenRead();
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            var buf = gz.ReadBytes();
            var reader = new SpanReader(buf);
            Read(ref reader);
            return buf.Length;
        }
        else
        {
            var buf = file.AsFile().ReadBytes();
            var reader = new SpanReader(buf);
            Read(ref reader);
            return buf.Length;
        }
    }

    /// <summary>使用迭代器模式加载文件数据。调用者可以一边读取一边处理数据（Span序列化格式）</summary>
    /// <param name="file">文件路径。gz文件自动使用压缩</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<Object?[]> LoadRows(String file)
    {
        if (file.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));

        var buf = file.AsFile().ReadBytes();

        // 解压缩
        if (file.EndsWithIgnoreCase(".gz"))
        {
            using var ms = new MemoryStream(buf);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            buf = gz.ReadBytes();
        }

        var reader = new SpanReader(buf);

        // 读取头部
        var magicBytes = reader.ReadBytes(MAGIC.Length);
        if (!magicBytes.SequenceEqual(MAGIC.GetBytes()))
            throw new InvalidDataException();

        var ver = reader.ReadByte();
        _ = reader.ReadByte();

        if (ver > _Ver) throw new InvalidDataException($"DbTable[ver={_Ver}] Unable to support newer versions [{ver}]");

        var count = reader.ReadEncodedInt();
        var cs = new String[count];
        var ts = new Type[count];

        for (var i = 0; i < count; i++)
        {
            cs[i] = reader.ReadString() ?? "";

            var tc = (TypeCode)reader.ReadByte();
            if (tc != TypeCode.Object)
                ts[i] = Type.GetType("System." + tc) ?? typeof(Object);
            else if (ver >= 2)
                ts[i] = Type.GetType(reader.ReadString() ?? "") ?? typeof(Object);
        }
        Columns = cs;
        Types = ts;

        Total = reader.ReadInt32();

        // 有些场景生成db文件时，无法在开始写入长度。
        var rows = Total;
        if (rows == 0 && buf.Length > 0) rows = -1;

        if (rows > 0)
        {
            for (var k = 0; k < rows; k++)
            {
                var row = new Object?[ts.Length];
                for (var i = 0; i < ts.Length; i++)
                {
                    row[i] = SpanSerializer.ReadValue(ref reader, ts[i]);
                }
                yield return row;
            }
        }
        else
        {
            while (reader.Available > 0)
            {
                var row = new Object?[ts.Length];
                for (var i = 0; i < ts.Length; i++)
                {
                    row[i] = SpanSerializer.ReadValue(ref reader, ts[i]);
                }
                yield return row;
            }
        }
    }

    Boolean IAccessor.Read(Stream stream, Object? context)
    {
        Read(stream);
        return true;
    }

    /// <summary>保存到文件（Span序列化格式）</summary>
    /// <param name="file"></param>
    /// <param name="compressed">是否压缩</param>
    /// <returns></returns>
    public Int64 SaveFile(String file, Boolean compressed = false)
    {
        file = file.GetFullPath().EnsureDirectory(true);

        if (compressed)
        {
            using var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            using var gz = new GZipStream(fs, CompressionMode.Compress);
            var buffer = new Byte[8192];
            var writer = new SpanWriter(buffer, gz);
            Write(ref writer);
            writer.Flush();
            return writer.TotalWritten;
        }
        else
        {
            using var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            var buffer = new Byte[8192];
            var writer = new SpanWriter(buffer, fs);
            Write(ref writer);
            writer.Flush();
            fs.SetLength(fs.Position);
            return writer.TotalWritten;
        }
    }

    /// <summary>使用迭代器模式写入多行数据到文件。调用者可以一边处理数据一边写入（Span序列化格式）</summary>
    /// <param name="file">文件路径。gz文件自动使用压缩</param>
    /// <param name="rows">数据源</param>
    /// <param name="fields">要写入的字段序列</param>
    /// <exception cref="ArgumentNullException"></exception>
    public Int32 SaveRows(String file, IEnumerable<Object?[]> rows, Int32[]? fields = null)
    {
        if (file.IsNullOrEmpty()) throw new ArgumentNullException(nameof(file));
        if (rows == null) throw new ArgumentNullException(nameof(rows));

        var ts = Types ?? throw new ArgumentNullException(nameof(Types));

        file = file.GetFullPath().EnsureDirectory(true);

        // 写入头部
        Stream? outStream = null;
        GZipStream? gz = null;
        try
        {
            var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            outStream = fs;

            if (file.EndsWithIgnoreCase(".gz"))
            {
                gz = new GZipStream(fs, CompressionMode.Compress);
                outStream = gz;
            }

            var headBuf = new Byte[8192];
            var headWriter = new SpanWriter(headBuf, outStream);

            // 写入幻数、版本
            headWriter.Write(MAGIC.GetBytes());
            headWriter.Write(_Ver);
            headWriter.Write((Byte)0);

            // 写入列信息
            var cs = Columns ?? throw new ArgumentNullException(nameof(Columns));
            headWriter.WriteEncodedInt(cs.Length);
            for (var i = 0; i < cs.Length; i++)
            {
                headWriter.Write(cs[i], 0);
                var code = ts[i].GetTypeCode();
                headWriter.Write((Byte)code);
                if (code == TypeCode.Object)
                    headWriter.Write(ts[i].FullName, 0);
            }

            // 暂写行数0，后续不回填（迭代模式下行数未知）
            headWriter.Write(0);
            headWriter.Flush();

            // 写入数据行
            var dataBuf = new Byte[8192];
            var dataWriter = new SpanWriter(dataBuf, outStream);
            var count = 0;
            foreach (var row in rows)
            {
                if (fields == null)
                {
                    for (var i = 0; i < row.Length; i++)
                    {
                        SpanSerializer.WriteValue(ref dataWriter, row[i], ts[i]);
                    }
                }
                else
                {
                    for (var i = 0; i < fields.Length; i++)
                    {
                        var idx = fields[i];
                        if (idx >= 0)
                            SpanSerializer.WriteValue(ref dataWriter, row[idx], ts[idx]);
                        else
                            SpanSerializer.WriteValue(ref dataWriter, null, ts[i]);
                    }
                }
                count++;
            }
            dataWriter.Flush();

            // 截断多余部分
            if (outStream is FileStream fss) fss.SetLength(fss.Position);

            return count;
        }
        finally
        {
            gz?.Dispose();
            outStream?.Dispose();
        }
    }

    Boolean IAccessor.Write(Stream stream, Object? context)
    {
        Write(stream);
        return true;
    }
    #endregion

    #region Json序列化
    /// <summary>转Json字符串</summary>
    /// <param name="indented">是否缩进。默认false</param>
    /// <param name="nullValue">是否写空值。默认true</param>
    /// <param name="camelCase">是否驼峰命名。默认false</param>
    /// <returns></returns>
    public String ToJson(Boolean indented = false, Boolean nullValue = true, Boolean camelCase = false)
    {
        // 先转为名值对象的数组，再进行序列化
        var list = ToDictionary();
        return list.ToJson(indented, nullValue, camelCase);
    }

    /// <summary>转为字典数组形式</summary>
    /// <returns></returns>
    public IList<IDictionary<String, Object?>> ToDictionary()
    {
        var list = new List<IDictionary<String, Object?>>();
        var cs = Columns ?? throw new ArgumentNullException(nameof(Columns));
        var rows = Rows;

        if (rows != null)
        {
            foreach (var row in rows)
            {
                var dic = new Dictionary<String, Object?>();
                for (var i = 0; i < cs.Length; i++)
                {
                    dic[cs[i]] = row[i];
                }
                list.Add(dic);
            }
        }

        return list;
    }
    #endregion

    #region Xml序列化
    /// <summary>转Xml字符串</summary>
    /// <returns></returns>
    public String GetXml()
    {
        var ms = new MemoryStream();
        WriteXml(ms).Wait(15_000);

        return ms.ToArray().ToStr();
    }

    /// <summary>以Xml格式写入数据流中</summary>
    /// <param name="stream"></param>
    public async Task WriteXml(Stream stream)
    {
        var set = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Auto,
            Indent = true,
            Async = true,
        };
        using var writer = XmlWriter.Create(stream, set);

        await writer.WriteStartDocumentAsync().ConfigureAwait(false);
        await writer.WriteStartElementAsync(null, "DbTable", null).ConfigureAwait(false);

        var cs = Columns ?? throw new ArgumentNullException(nameof(Columns));
        var ts = Types ?? throw new ArgumentNullException(nameof(Types));
        var rows = Rows;

        if (rows != null)
        {
            foreach (var row in rows)
            {
                await writer.WriteStartElementAsync(null, "Table", null).ConfigureAwait(false);
                for (var i = 0; i < cs.Length; i++)
                {
                    await writer.WriteStartElementAsync(null, cs[i], null).ConfigureAwait(false);

                    if (ts[i] == typeof(Boolean))
                        writer.WriteValue(row[i].ToBoolean());
                    else if (ts[i] == typeof(DateTime))
                        writer.WriteValue(new DateTimeOffset(row[i].ChangeType<DateTime>()));
                    else if (ts[i] == typeof(DateTimeOffset))
                        writer.WriteValue(row[i].ChangeType<DateTimeOffset>());
                    else if (row[i] is IFormattable ft)
                        await writer.WriteStringAsync(ft + "").ConfigureAwait(false);
                    else
                        await writer.WriteStringAsync(row[i] + "").ConfigureAwait(false);

                    await writer.WriteEndElementAsync().ConfigureAwait(false);
                }
                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteEndDocumentAsync().ConfigureAwait(false);
    }
    #endregion

    #region Csv序列化
    /// <summary>保存到Csv文件</summary>
    /// <param name="file"></param>
    public void SaveCsv(String file)
    {
        var cs = Columns ?? throw new ArgumentNullException(nameof(Columns));
        var rows = Rows;

        using var csv = new CsvFile(file, true);
        csv.WriteLine(cs);
        if (rows != null) csv.WriteAll(rows);
    }

    /// <summary>从Csv文件加载</summary>
    /// <param name="file"></param>
    public void LoadCsv(String file)
    {
        using var csv = new CsvFile(file, false);
        var cs = csv.ReadLine();
        if (cs != null) Columns = cs;
        Rows = csv.ReadAll().Cast<Object?[]>().ToList();
    }
    #endregion

    #region 读写模型
    /// <summary>写入模型列表</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="models"></param>
    public void WriteModels<T>(IEnumerable<T> models)
    {
        // 可用属性
        var pis = typeof(T).GetProperties(true);
        pis = pis.Where(e => e.PropertyType.IsBaseType()).ToArray();

        // 头部
        if (Columns == null || Columns.Length == 0)
        {
            Columns = pis.Select(e => SerialHelper.GetName(e)).ToArray();
            Types = pis.Select(e => e.PropertyType).ToArray();
        }

        Rows = Cast<T>(models).ToList();
    }

    /// <summary>模型列表转为对象数组行。支持WriteRows/SaveRows实现一边处理一边写入</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="models"></param>
    /// <returns></returns>
    public IEnumerable<Object?[]> Cast<T>(IEnumerable<T> models)
    {
        // 可用属性
        var pis = typeof(T).GetProperties(true);
        pis = pis.Where(e => e.PropertyType.IsBaseType()).ToArray();

        foreach (var item in models)
        {
            var row = new Object?[Columns.Length];
            for (var i = 0; i < row.Length; i++)
            {
                // 反射取值
                if (pis[i].CanRead)
                {
                    if (item is IModel ext)
                        row[i] = ext[pis[i].Name];
                    else if (item != null)
                        row[i] = item.GetValue(pis[i]);
                }
            }
            yield return row;
        }
    }

    /// <summary>数据表转模型列表。普通反射，便于DAL查询后转任意模型列表</summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<T> ReadModels<T>()
    {
        foreach (var model in ReadModels(typeof(T)))
        {
            yield return (T)model;
        }
    }

    /// <summary>数据表转模型列表。普通反射，便于DAL查询后转任意模型列表</summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public IEnumerable<Object> ReadModels(Type type)
    {
        var cs = Columns ?? throw new ArgumentNullException(nameof(Columns));
        var rows = Rows;
        if (rows == null) yield break;

        // 可用属性（通过 DefaultReflect 缓存，避免重复反射扫描）
        var pis = type.GetProperties(true);
        var dic = pis.ToDictionary(e => SerialHelper.GetName(e), e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var model = type.CreateInstance();
            if (model == null) continue;

            for (var i = 0; i < row.Length; i++)
            {
                // 扩展赋值，或 反射赋值
                if (dic.TryGetValue(cs[i], out var pi) && pi.CanWrite)
                {
                    var val = row[i].ChangeType(pi.PropertyType);
                    if (model is IModel ext)
                        ext[pi.Name] = val;
                    else
                        model.SetValue(pi, val);
                }
            }

            yield return model;
        }
    }
    #endregion

    #region 获取
    /// <summary>读取指定行的字段值</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="row"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public T? Get<T>(Int32 row, String name) => !TryGet<T>(row, name, out var value) ? default : value;

    /// <summary>尝试读取指定行的字段值</summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="row"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public Boolean TryGet<T>(Int32 row, String name, out T? value)
    {
        value = default;
        var rs = Rows;
        if (rs == null) return false;

        if (row < 0 || row >= rs.Count || name.IsNullOrEmpty()) return false;

        var col = GetColumn(name);
        if (col < 0) return false;

        value = rs[row][col].ChangeType<T>();

        return true;
    }

    /// <summary>根据名称找字段序号</summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public Int32 GetColumn(String name)
    {
        var cs = Columns;
        if (cs == null) return -1;

        for (var i = 0; i < cs.Length; i++)
        {
            if (cs[i].EqualIgnoreCase(name)) return i;
        }

        return -1;
    }
    #endregion

    #region 辅助
    /// <summary>数据集</summary>
    /// <returns></returns>
    public override String ToString() => $"DbTable[{Columns?.Length}][{Rows?.Count}]";

    private static IDictionary<TypeCode, Object?>? _Defs;
    private static Object? GetDefault(TypeCode tc)
    {
        if (_Defs == null)
        {
            var dic = new Dictionary<TypeCode, Object?>();
            foreach (var item in Enum.GetValues(typeof(TypeCode)))
            {
                if (item is not TypeCode tc2) continue;

                Object? val = null;
                val = tc2 switch
                {
                    TypeCode.Boolean => false,
                    TypeCode.Char => (Char)0,
                    TypeCode.SByte => (SByte)0,
                    TypeCode.Byte => (Byte)0,
                    TypeCode.Int16 => (Int16)0,
                    TypeCode.UInt16 => (UInt16)0,
                    TypeCode.Int32 => 0,
                    TypeCode.UInt32 => (UInt32)0,
                    TypeCode.Int64 => (Int64)0,
                    TypeCode.UInt64 => (UInt64)0,
                    TypeCode.Single => (Single)0,
                    TypeCode.Double => (Double)0,
                    TypeCode.Decimal => (Decimal)0,
                    TypeCode.DateTime => DateTime.MinValue,
                    _ => null,
                };
                dic[tc2] = val;
            }
            _Defs = dic;
        }

        return _Defs.TryGetValue(tc, out var obj) ? obj : null;
    }

    Object ICloneable.Clone() => Clone();

    /// <summary>克隆</summary>
    /// <returns></returns>
    public DbTable Clone()
    {
        var dt = new DbTable
        {
            Columns = Columns.ToArray(),
            Types = Types.ToArray(),
            Rows = Rows.ToList(),
            Total = Total
        };

        return dt;
    }

    /// <summary>获取数据行</summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public DbRow GetRow(Int32 index) => new(this, index);
    #endregion

    #region 枚举
    /// <summary>获取枚举</summary>
    /// <returns></returns>
    public IEnumerator<DbRow> GetEnumerator() => new DbEnumerator { Table = this };

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private struct DbEnumerator : IEnumerator<DbRow>
    {
        public DbTable Table { get; set; }

        private Int32 _row;
        private DbRow _Current;
        public readonly DbRow Current => _Current;

        readonly Object IEnumerator.Current => _Current;

        public Boolean MoveNext()
        {
            var rs = Table.Rows;
            if (rs == null || rs.Count == 0) return false;

            // 首次或 Reset 后的第一次枚举，_row 可能是 -1（Reset 设置）或默认 0。
            if (_row < 0) _row = 0;

            // 已到结尾
            if (_row >= rs.Count)
            {
                _Current = default;
                return false;
            }

            // 先构建当前行，再为下一次枚举准备索引，避免第一次返回索引1
            _Current = new DbRow(Table, _row);
            _row++;

            return true;
        }

        public void Reset()
        {
            _Current = default;
            _row = -1;
        }

        public void Dispose() { }
    }
    #endregion

    #region 日志
    #endregion
}
