using System.Runtime.Versioning;
using Avalonia;

namespace Narika;

// The whole composition below is Windows' own — the launcher builds a
// Windows program and leaves Windows' own marks on it — which is also
// where the platform annotation stops: nothing above the entry point
// reaches back in.
[SupportedOSPlatform("windows")]
sealed class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things
    // aren't initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
