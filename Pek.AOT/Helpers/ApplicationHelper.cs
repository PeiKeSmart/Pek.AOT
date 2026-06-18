// AOT: skipped - ApplicationHelper is platform-specific (ASP.NET/IIS context)
// Source: Pek.Common/Helpers/ApplicationHelper.cs
// Reason: Uses AppDomain.CurrentDomain (BaseDirectory, FriendlyName) and Assembly.GetEntryAssembly()
// which are .NET Framework / ASP.NET centric APIs. Not portable for NativeAOT scenarios.
namespace Pek.Helpers;

/// <summary>AOT 占位：ApplicationHelper 使用 AppDomain/Assembly 等 .NET Framework 平台 API，不适配 NativeAOT</summary>
public static class ApplicationHelperPlaceholder
{
    // AOT: skipped - platform-specific
}
