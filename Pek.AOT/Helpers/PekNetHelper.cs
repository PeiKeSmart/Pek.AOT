// AOT: skipped - PekNetHelper (DGNetHelper) is platform-specific network diagnostics
// Source: Pek.Common/Helpers/PekNetHelper.cs
// Reason: Uses IPGlobalProperties.GetActiveTcpListeners/UdpListeners/TcpConnections
// which are platform-specific network diagnostic APIs. Not core to AOT migration.
namespace Pek.Helpers;

/// <summary>AOT 占位：PekNetHelper 使用平台网络诊断 API，纳入平台特定分类</summary>
public static class PekNetHelperPlaceholder
{
    // AOT: skipped - platform-specific (network diagnostics)
}
