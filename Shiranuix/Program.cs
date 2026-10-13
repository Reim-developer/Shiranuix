using Avalonia;
using Shiranuix.Fonts;
using System;

namespace Shiranuix;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Registers Assets/Fonts as the "fonts:Shiranuix" scheme so the
            // bundled Nunito family can be referenced from XAML.
            .ConfigureFonts(fonts => fonts.AddFontCollection(new ShiranuixFontCollection()))
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
