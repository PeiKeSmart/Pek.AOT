using System.IO.Ports;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace Pek.Net;

public partial class SerialTransport
{
    [SupportedOSPlatform("windows")]
    static partial void TryFillPortDescriptions(Dictionary<String, String> result)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM", false);
            using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", false);
            if (key == null) return;

            foreach (var item in key.GetValueNames())
            {
                var name = key.GetValue(item)?.ToString() ?? String.Empty;
                if (String.IsNullOrWhiteSpace(name)) continue;

                var description = ResolveDescription(usb, name, item);
                result[name] = description;
            }
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static String ResolveDescription(RegistryKey? usb, String name, String fallback)
    {
        if (usb != null)
        {
            foreach (var vid in usb.GetSubKeyNames())
            {
                using var usbVid = usb.OpenSubKey(vid);
                if (usbVid == null) continue;

                foreach (var child in usbVid.GetSubKeyNames())
                {
                    using var sub = usbVid.OpenSubKey(child);
                    var friendlyName = sub?.GetValue("FriendlyName")?.ToString();
                    if (String.IsNullOrWhiteSpace(friendlyName)) continue;
                    if (!friendlyName.Contains($"({name})", StringComparison.OrdinalIgnoreCase)) continue;

                    return friendlyName.Replace($"({name})", String.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                }
            }
        }

        var index = fallback.LastIndexOf('\\');
        return index >= 0 ? fallback[(index + 1)..] : fallback;
    }
}