using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

using Pek.Extension;

namespace Pek.Net;

/// <summary>Tcp连接信息</summary>
public class TcpConnectionInformation2 : TcpConnectionInformation
{
    /// <summary>本地结点</summary>
    public override IPEndPoint LocalEndPoint { get; }

    /// <summary>远程结点</summary>
    public override IPEndPoint RemoteEndPoint { get; }

    /// <summary>Tcp状态</summary>
    public override TcpState State { get; }

    /// <summary>进程标识</summary>
    public Int32 ProcessId { get; set; }

    /// <summary>inode标识</summary>
    public String? Node { get; set; }

    /// <summary>实例化Tcp连接信息</summary>
    /// <param name="local">本地结点</param>
    /// <param name="remote">远程结点</param>
    /// <param name="state">Tcp状态</param>
    /// <param name="processId">进程标识</param>
    public TcpConnectionInformation2(IPEndPoint local, IPEndPoint remote, TcpState state, Int32 processId)
    {
        LocalEndPoint = local;
        RemoteEndPoint = remote;
        State = state;
        ProcessId = processId;
    }

    private TcpConnectionInformation2(MibTcpRowOwnerPid row)
    {
        State = (TcpState)row.State;
        var port = (row.LocalPort1 << 8) | row.LocalPort2;
        var port2 = State != TcpState.Listen ? ((row.RemotePort1 << 8) | row.RemotePort2) : 0;
        LocalEndPoint = new IPEndPoint(row.LocalAddr, port);
        RemoteEndPoint = new IPEndPoint(row.RemoteAddr, port2);
        ProcessId = row.OwningPid;
    }

    /// <summary>已重载。</summary>
    /// <returns>连接信息文本</returns>
    public override String ToString() => $"{LocalEndPoint}<=>{RemoteEndPoint} {State} {ProcessId}";

    #region Windows连接信息
    private enum TcpTableClass : Int32
    {
        TcpTableBasicListener,
        TcpTableBasicConnections,
        TcpTableBasicAll,
        TcpTableOwnerPidListener,
        TcpTableOwnerPidConnections,
        TcpTableOwnerPidAll,
        TcpTableOwnerModuleListener,
        TcpTableOwnerModuleConnections,
        TcpTableOwnerModuleAll
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public UInt32 State;
        public UInt32 LocalAddr;
        public Byte LocalPort1;
        public Byte LocalPort2;
        public Byte LocalPort3;
        public Byte LocalPort4;
        public UInt32 RemoteAddr;
        public Byte RemotePort1;
        public Byte RemotePort2;
        public Byte RemotePort3;
        public Byte RemotePort4;
        public Int32 OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpTableOwnerPid
    {
        public UInt32 DwNumEntries;
        private MibTcpRowOwnerPid _table;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern UInt32 GetExtendedTcpTable(IntPtr tcpTable, ref Int32 outBufferLength, Boolean sort, Int32 ipVersion, TcpTableClass tableClass, Int32 reserved);

    /// <summary>获取所有Tcp连接</summary>
    /// <returns>Tcp连接数组</returns>
    [Obsolete("请使用 GetWindowsTcpConnections()")]
    public static TcpConnectionInformation2[] GetAllTcpConnections() => GetWindowsTcpConnections();

    /// <summary>获取Windows下所有Tcp连接</summary>
    /// <returns>Tcp连接数组</returns>
    public static TcpConnectionInformation2[] GetWindowsTcpConnections()
    {
        const Int32 afInet = 2;
        var bufferSize = 0;

        var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, afInet, TcpTableClass.TcpTableOwnerPidAll, 0);
        if (result is not 0 and not 122) throw new Exception("bad ret on check " + result);

        var bufferTable = Marshal.AllocHGlobal(bufferSize);
        var list = new List<TcpConnectionInformation2>();
        try
        {
            result = GetExtendedTcpTable(bufferTable, ref bufferSize, true, afInet, TcpTableClass.TcpTableOwnerPidAll, 0);
            if (result != 0) throw new Exception("bad ret " + result);

            var table = Marshal.PtrToStructure<MibTcpTableOwnerPid>(bufferTable);
            var rowPointer = (IntPtr)((Int64)bufferTable + Marshal.SizeOf(table.DwNumEntries));
            for (var i = 0; i < table.DwNumEntries; i++)
            {
                var tcpRow = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                list.Add(new TcpConnectionInformation2(tcpRow));
                rowPointer = (IntPtr)((Int64)rowPointer + Marshal.SizeOf(tcpRow));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(bufferTable);
        }

        return list.ToArray();
    }
    #endregion

    #region Linux连接信息
    /// <summary>获取指定进程的Tcp连接</summary>
    /// <param name="processId">目标进程。默认-1未指定，获取所有进程的Tcp连接</param>
    /// <returns>Tcp连接数组</returns>
    public static TcpConnectionInformation2[] GetLinuxTcpConnections(Int32 processId = -1)
    {
        var list = new List<TcpConnectionInformation2>();

        String[]? nodes = null;
        if (processId > 0)
        {
            nodes = GetNodes(processId);
            if (nodes == null || nodes.Length == 0) return list.ToArray();
        }

        var result = ParseTcpsFromFile(processId > 0 ? $"/proc/{processId}/net/tcp" : "/proc/net/tcp");
        if (result.Count > 0) list.AddRange(result);

        var result2 = ParseTcpsFromFile(processId > 0 ? $"/proc/{processId}/net/tcp6" : "/proc/net/tcp6");
        if (result2.Count > 0) list.AddRange(result2);

        if (processId > 0 && nodes != null)
        {
            var list2 = new List<TcpConnectionInformation2>();
            foreach (var item in list)
            {
                if (item.Node != null && nodes.Contains(item.Node))
                {
                    item.ProcessId = processId;
                    list2.Add(item);
                }
            }

            list = list2;
        }

        return list.ToArray();
    }

    private static IList<TcpConnectionInformation2> ParseTcpsFromFile(String file)
    {
        var text = File.ReadAllText(file);
        return ParseTcps(text);
    }

    /// <summary>分析Tcp连接信息</summary>
    /// <param name="text">文本内容</param>
    /// <returns>Tcp连接集合</returns>
    public static IList<TcpConnectionInformation2> ParseTcps(String text)
    {
        var list = new List<TcpConnectionInformation2>();
        if (text.IsNullOrEmpty()) return list;

        foreach (var line in text.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || parts[1].IndexOf(':') < 0) continue;

            var state = GetState(parts[3]);
            var local = ParseAddressAndPort(parts[1]);
            var remote = ParseAddressAndPort(parts[2]);
            var info = new TcpConnectionInformation2(local, remote, state, 0)
            {
                Node = parts.Length > 9 ? parts[9] : null,
            };

            list.Add(info);
        }

        return list;
    }

    private static String[] GetNodes(Int32 processId)
    {
        var path = $"/proc/{processId}/fd".AsDirectory();
        if (!path.Exists) return [];

        var files = new List<String>();
        foreach (var file in path.GetFiles())
        {
            var name = file.Name;
#if NET6_0_OR_GREATER
            if (file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                name = file.ResolveLinkTarget(true)?.Name ?? name;
#endif

            if (!name.IsNullOrEmpty()) files.Add(name);
        }

        return ParseNodes(files);
    }

    /// <summary>分析Socket的inode</summary>
    /// <param name="files">文件名集合</param>
    /// <returns>inode数组</returns>
    public static String[] ParseNodes(IList<String> files)
    {
        var list = new List<String>();
        foreach (var item in files)
        {
            var node = item.Substring("socket:[", "]");
            if (!node.IsNullOrEmpty()) list.Add(node);
        }

        return list.ToArray();
    }

    private static IPEndPoint ParseAddressAndPort(String colonSeparatedAddress)
    {
        var index = colonSeparatedAddress.IndexOf(':');
        if (index == -1) throw new NetworkInformationException();

        var address = ParseHexIPAddress(colonSeparatedAddress[..index]);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var portText = colonSeparatedAddress[(index + 1)..];
        return !Int32.TryParse(portText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var port)
            ? throw new NetworkInformationException()
            : new IPEndPoint(address, port);
    }

    internal static IPAddress ParseHexIPAddress(String remoteAddressString)
    {
        if (remoteAddressString.Length <= 8) return ParseIPv4HexString(remoteAddressString);
        if (remoteAddressString.Length == 32) return ParseIPv6HexString(remoteAddressString);

        throw new NetworkInformationException();
    }

    private static IPAddress ParseIPv4HexString(String hexAddress)
    {
        return !Int64.TryParse(hexAddress, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result)
            ? throw new NetworkInformationException()
            : new IPAddress(result);
    }

    private static IPAddress ParseIPv6HexString(String hexAddress, Boolean isNetworkOrder = false)
    {
        var span = Convert.FromHexString(hexAddress);
        if (!isNetworkOrder && BitConverter.IsLittleEndian)
        {
            for (var index = 0; index < 4; index++)
            {
                Array.Reverse(span, index * 4, 4);
            }
        }

        return new IPAddress(span);
    }

    private static TcpState GetState(String hexState)
    {
        return hexState switch
        {
            "01" => TcpState.Established,
            "02" => TcpState.SynSent,
            "03" => TcpState.SynReceived,
            "04" => TcpState.FinWait1,
            "05" => TcpState.FinWait2,
            "06" => TcpState.TimeWait,
            "07" => TcpState.Closed,
            "08" => TcpState.CloseWait,
            "09" => TcpState.LastAck,
            "0A" => TcpState.Listen,
            "0B" => TcpState.Closing,
            _ => TcpState.Unknown,
        };
    }
    #endregion
}