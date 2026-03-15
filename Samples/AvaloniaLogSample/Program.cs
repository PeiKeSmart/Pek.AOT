using Avalonia;

namespace AvaloniaLogSample;

internal sealed class Program
{
    [STAThread]
    public static void Main(String[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}