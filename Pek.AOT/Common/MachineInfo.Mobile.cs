namespace Pek;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

public partial class MachineInfo
{
    static partial void FillDeviceInfo(IDictionary<String, String?> dic)
    {
        dic["Platform"] = "Android";
        dic["Version"] = Build.VERSION.Release;
        dic["Product"] = Build.Product;

        var androidId = Settings.Secure.GetString(Application.Context?.ContentResolver, Settings.Secure.AndroidId);
        if (!String.IsNullOrEmpty(androidId)) dic["android_id"] = androidId;

        var serial = GetSerial();
        if (!String.IsNullOrEmpty(serial)) dic["Serial"] = serial;
    }

    static partial void FillDeviceBattery(IDictionary<String, Object?> dic)
    {
        var context = Application.Context;
        if (context == null) return;

        var intent = context.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
        if (intent == null) return;

        var level = intent.GetIntExtra(BatteryManager.ExtraLevel, -1);
        var scale = intent.GetIntExtra(BatteryManager.ExtraScale, -1);
        if (level < 0 || scale <= 0) return;

        dic["ChargeLevel"] = (Double)level / scale;
    }

    private static String? GetSerial()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                return Build.GetSerial();
        }
        catch { }

        try
        {
            return Build.Serial;
        }
        catch
        {
            return null;
        }
    }
}
#else
public partial class MachineInfo
{
    static partial void FillDeviceInfo(IDictionary<String, String?> dic) { }

    static partial void FillDeviceBattery(IDictionary<String, Object?> dic) { }
}
#endif