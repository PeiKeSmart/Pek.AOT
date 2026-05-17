using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Pek;

namespace NewLife.Reflection;

/// <summary>程序集辅助类。使用Create创建，保证每个程序集只有一个辅助类</summary>
public class AssemblyX
{
    private static readonly ConcurrentDictionary<Assembly, ConcurrentDictionary<Type, Byte>> _registeredTypes = new();

    #region 属性
    /// <summary>程序集</summary>
    public Assembly Asm { get; }

    private String? _name;
    /// <summary>名称</summary>
    public String Name => _name ??= Asm.GetName().Name ?? String.Empty;

    private String? _version;
    /// <summary>程序集版本</summary>
    public String Version => _version ??= Asm.GetName().Version + String.Empty;

    private String? _title;
    /// <summary>程序集标题</summary>
    public String Title => _title ??= Asm.GetCustomAttributeValue<AssemblyTitleAttribute, String>() ?? String.Empty;

    private String? _fileVersion;
    /// <summary>文件版本</summary>
    public String FileVersion
    {
        get
        {
            if (_fileVersion == null)
            {
                var ver = Asm.GetCustomAttributeValue<AssemblyInformationalVersionAttribute, String>();
                if (!String.IsNullOrEmpty(ver))
                {
                    var p = ver.IndexOf('+');
                    if (p > 0) ver = ver[..p];
                }

                _fileVersion = !String.IsNullOrEmpty(ver)
                    ? ver
                    : Asm.GetCustomAttributeValue<AssemblyFileVersionAttribute, String>() ?? String.Empty;
            }

            return _fileVersion;
        }
    }

    private DateTime? _compile;
    /// <summary>编译时间</summary>
    public DateTime Compile
    {
        get
        {
            if (_compile == null)
            {
                var time = GetCompileTime(Version);
                if (time == time.Date && FileVersion.Contains("-beta", StringComparison.OrdinalIgnoreCase)) time = GetCompileTime(FileVersion);

                _compile = time;
            }

            return _compile.Value;
        }
    }

    private String? _company;
    /// <summary>公司名称</summary>
    public String Company => _company ??= Asm.GetCustomAttributeValue<AssemblyCompanyAttribute, String>() ?? String.Empty;

    private String? _description;
    /// <summary>说明</summary>
    public String Description => _description ??= Asm.GetCustomAttributeValue<AssemblyDescriptionAttribute, String>() ?? String.Empty;

    /// <summary>获取包含清单的已加载文件的路径或 UNC 位置。</summary>
    public String? Location
    {
        get
        {
            return null;
        }
    }
    #endregion

    #region 构造
    private AssemblyX(Assembly asm) => Asm = asm;

    private static readonly ConcurrentDictionary<Assembly, AssemblyX> _cache = new();

    /// <summary>创建程序集辅助对象</summary>
    /// <param name="asm"></param>
    /// <returns></returns>
    public static AssemblyX? Create(Assembly? asm)
    {
        if (asm == null) return null;

        return _cache.GetOrAdd(asm, static key => new AssemblyX(key));
    }

    /// <summary>注册类型，使其可被 AssemblyX 类型搜索与插件扫描发现</summary>
    /// <param name="type">类型</param>
    public static void Register([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)] Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        var assembly = type.Assembly;
        var map = _registeredTypes.GetOrAdd(assembly, static _ => new ConcurrentDictionary<Type, Byte>());
        map.TryAdd(type, 0);

        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            map.TryAdd(nestedType, 0);
        }
    }

    /// <summary>注册类型，使其可被 AssemblyX 类型搜索与插件扫描发现</summary>
    /// <typeparam name="T"></typeparam>
    public static void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.NonPublicNestedTypes)] T>() => Register(typeof(T));
    #endregion

    #region 扩展属性
    /// <summary>类型集合，当前程序集的所有类型，包括私有和内嵌，非内嵌请直接调用Asm.GetTypes()</summary>
    public IEnumerable<Type> Types
    {
        get
        {
            if (!_registeredTypes.TryGetValue(Asm, out var types)) yield break;

            foreach (var item in types.Keys)
            {
                yield return item;
            }
        }
    }

    /// <summary>是否系统程序集</summary>
    public Boolean IsSystemAssembly => CheckSystem(Asm);

    private static Boolean CheckSystem(Assembly asm)
    {
        var name = asm.FullName;
        if (String.IsNullOrEmpty(name)) return false;

        return name.EndsWith("PublicKeyToken=b77a5c561934e089", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("PublicKeyToken=b03f5f7f11d50a3a", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("PublicKeyToken=89845dcd8080cc91", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("PublicKeyToken=31bf3856ad364e35", StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region 静态属性
    /// <summary>入口程序集</summary>
    public static AssemblyX? Entry { get; set; } = Create(Assembly.GetEntryAssembly());

    private static ICollection<String>? _assemblyPaths;

    /// <summary>程序集目录集合</summary>
    public static ICollection<String> AssemblyPaths
    {
        [return: NotNull]
        get
        {
            if (_assemblyPaths == null)
            {
                HashSet<String> set = new(StringComparer.OrdinalIgnoreCase);

                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                if (!String.IsNullOrEmpty(baseDirectory)) set.Add(baseDirectory);

                var currentDirectory = Environment.CurrentDirectory;
                if (!String.IsNullOrEmpty(currentDirectory)) set.Add(currentDirectory);

                var pluginPath = Setting.Current.GetPluginPath();
                if (!String.IsNullOrEmpty(pluginPath)) set.Add(pluginPath);

                _assemblyPaths = set;
            }

            return _assemblyPaths;
        }
        set => _assemblyPaths = value;
    }
    #endregion

    #region 方法
    private readonly ConcurrentDictionary<String, Type?> _typeCache = new(StringComparer.Ordinal);

    /// <summary>从程序集中查找指定名称的类型</summary>
    /// <param name="typeName"></param>
    /// <returns></returns>
    public Type? GetType(String typeName)
    {
        if (String.IsNullOrEmpty(typeName)) throw new ArgumentNullException(nameof(typeName));

        return _typeCache.GetOrAdd(typeName, GetTypeInternal);
    }

    private Type? GetTypeInternal(String typeName)
    {
        foreach (var item in Types)
        {
            if (String.Equals(item.FullName, typeName, StringComparison.Ordinal)) return item;
            if (String.Equals(item.Name, typeName, StringComparison.Ordinal)) return item;
        }

        return null;
    }

    private readonly ConcurrentDictionary<Type, List<Type>> _plugins = new();

    /// <summary>查找插件，带缓存</summary>
    /// <param name="baseType">类型</param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IList<Type> FindPlugins(Type baseType)
    {
        return _plugins.GetOrAdd(baseType, key =>
        {
            List<Type> list = [];
            foreach (var item in Types)
            {
                if (item == null || item == key) continue;
                if (item.IsInterface || item.IsAbstract || item.IsGenericType) continue;
                if (!key.IsAssignableFrom(item)) continue;

                list.Add(item);
            }

            return list;
        });
    }

    /// <summary>查找所有非系统程序集中的所有插件</summary>
    /// <param name="baseType"></param>
    /// <param name="isLoadAssembly">是否尝试从未加载程序集获取类型。AOT版本仅搜索已加载程序集</param>
    /// <param name="excludeGlobalTypes">是否排除系统程序集</param>
    /// <returns></returns>
    public static IEnumerable<Type> FindAllPlugins(Type baseType, Boolean isLoadAssembly = false, Boolean excludeGlobalTypes = true)
    {
        HashSet<Type> set = [];

        foreach (var item in GetAssemblies())
        {
            if (excludeGlobalTypes && item.IsSystemAssembly) continue;

            foreach (var plugin in item.FindPlugins(baseType))
            {
                if (set.Add(plugin)) yield return plugin;
            }
        }
    }

    /// <summary>根据名称获取类型</summary>
    /// <param name="typeName">类型名</param>
    /// <param name="isLoadAssembly">是否尝试从未加载程序集获取类型。AOT版本仅搜索已加载程序集</param>
    /// <returns></returns>
    public static Type? GetType(String typeName, Boolean isLoadAssembly)
    {
        if (String.IsNullOrEmpty(typeName)) return null;

        if (TryGetKnownType(typeName, out var type)) return type;

        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            var elementType = GetType(typeName[..^2], isLoadAssembly);
            return elementType == null ? null : GetKnownArrayType(elementType);
        }

        List<AssemblyX> loads = [];
        AssemblyX?[] asms =
        [
            Create(Assembly.GetExecutingAssembly()),
            Create(Assembly.GetCallingAssembly()),
            Create(Assembly.GetEntryAssembly())
        ];
        foreach (var asm in asms)
        {
            if (asm == null || loads.Contains(asm)) continue;
            loads.Add(asm);

            type = asm.GetType(typeName);
            if (type != null) return type;
        }

        foreach (var asm in GetAssemblies())
        {
            if (loads.Contains(asm)) continue;

            type = asm.GetType(typeName);
            if (type != null) return type;
        }

        return null;
    }
    #endregion

    #region 静态加载
    /// <summary>获取指定程序域所有程序集</summary>
    /// <param name="domain"></param>
    /// <returns></returns>
    public static IEnumerable<AssemblyX> GetAssemblies(AppDomain? domain = null)
    {
        domain ??= AppDomain.CurrentDomain;

        foreach (var item in domain.GetAssemblies())
        {
            var asm = Create(item);
            if (asm != null) yield return asm;
        }
    }

    /// <summary>获取当前应用程序的所有程序集，不包括系统程序集，仅限本目录</summary>
    /// <returns></returns>
    public static List<AssemblyX> GetMyAssemblies()
    {
        List<AssemblyX> list = [];
        HashSet<Assembly> seen = [];

        foreach (var item in GetAssemblies())
        {
            if (item.IsSystemAssembly) continue;
            if (!seen.Add(item.Asm)) continue;

            list.Add(item);
        }

        return list;
    }
    #endregion

    #region 重载
    /// <summary>已重载。</summary>
    /// <returns></returns>
    public override String ToString() => !String.IsNullOrEmpty(Title) ? Title : Name;
    #endregion

    #region 辅助
    /// <summary>根据版本号计算得到编译时间</summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public static DateTime GetCompileTime(String version)
    {
        var ss = version?.Split('.');
        if (ss == null || ss.Length < 4) return DateTime.MinValue;

        var d = ss[2].ToInt();
        var s = ss[3].ToInt();
        var y = DateTime.Today.Year;

        if (d <= y && d >= y - 10)
        {
            var dt = new DateTime(d, 1, 1);
            if (s > 0)
            {
                if (s >= 200) dt = dt.AddMonths(s / 100 - 1);
                s %= 100;
                if (s > 1) dt = dt.AddDays(s - 1);
            }
            else
            {
                var str = ss[3];
                var p = str.IndexOf('-');
                if (p > 0)
                {
                    s = str[..p].ToInt();
                    if (s > 0)
                    {
                        if (s >= 200) dt = dt.AddMonths(s / 100 - 1);
                        s %= 100;
                        if (s > 1) dt = dt.AddDays(s - 1);
                    }

                    if (str.Length >= 9)
                    {
                        s = str[^4..].ToInt();
                        if (s > 0) dt = dt.AddHours(s / 100).AddMinutes(s % 100).ToLocalTime();
                    }
                }
            }

            return dt;
        }

        return new DateTime(2000, 1, 1).AddDays(d).AddSeconds(s * 2);
    }

    private static Boolean TryGetKnownType(String typeName, out Type? type)
    {
        type = typeName.ToLowerInvariant() switch
        {
            "bool" or "boolean" or "system.boolean" => typeof(Boolean),
            "byte" or "system.byte" => typeof(Byte),
            "sbyte" or "system.sbyte" => typeof(SByte),
            "short" or "int16" or "system.int16" => typeof(Int16),
            "ushort" or "uint16" or "system.uint16" => typeof(UInt16),
            "int" or "int32" or "system.int32" => typeof(Int32),
            "uint" or "uint32" or "system.uint32" => typeof(UInt32),
            "long" or "int64" or "system.int64" => typeof(Int64),
            "ulong" or "uint64" or "system.uint64" => typeof(UInt64),
            "float" or "single" or "system.single" => typeof(Single),
            "double" or "system.double" => typeof(Double),
            "decimal" or "system.decimal" => typeof(Decimal),
            "char" or "system.char" => typeof(Char),
            "string" or "system.string" => typeof(String),
            "object" or "system.object" => typeof(Object),
            "guid" or "system.guid" => typeof(Guid),
            "datetime" or "system.datetime" => typeof(DateTime),
            "datetimeoffset" or "system.datetimeoffset" => typeof(DateTimeOffset),
            "timespan" or "system.timespan" => typeof(TimeSpan),
            _ => null,
        };

        return type != null;
    }

    private static Type? GetKnownArrayType(Type elementType)
    {
        if (elementType == typeof(Boolean)) return typeof(Boolean[]);
        if (elementType == typeof(Byte)) return typeof(Byte[]);
        if (elementType == typeof(SByte)) return typeof(SByte[]);
        if (elementType == typeof(Int16)) return typeof(Int16[]);
        if (elementType == typeof(UInt16)) return typeof(UInt16[]);
        if (elementType == typeof(Int32)) return typeof(Int32[]);
        if (elementType == typeof(UInt32)) return typeof(UInt32[]);
        if (elementType == typeof(Int64)) return typeof(Int64[]);
        if (elementType == typeof(UInt64)) return typeof(UInt64[]);
        if (elementType == typeof(Single)) return typeof(Single[]);
        if (elementType == typeof(Double)) return typeof(Double[]);
        if (elementType == typeof(Decimal)) return typeof(Decimal[]);
        if (elementType == typeof(Char)) return typeof(Char[]);
        if (elementType == typeof(String)) return typeof(String[]);
        if (elementType == typeof(Object)) return typeof(Object[]);
        if (elementType == typeof(Guid)) return typeof(Guid[]);
        if (elementType == typeof(DateTime)) return typeof(DateTime[]);
        if (elementType == typeof(DateTimeOffset)) return typeof(DateTimeOffset[]);
        if (elementType == typeof(TimeSpan)) return typeof(TimeSpan[]);

        return null;
    }
    #endregion
}