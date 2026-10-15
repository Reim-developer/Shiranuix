using System;
using Avalonia;
using Shiranuix.Logging;

namespace Shiranuix;

sealed class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
        // The engine is wound up first, so everything after this line —
        // the startup note below, the container, the first window, the
        // crash guard — has somewhere to be written. Nothing here leans
        // on Avalonia or a synchronization context; a file and a debugger
        // window are all that is touched.
        Logs.Configure();
        Logs.NoteStartup(args);

        // The nets go up before anything can fall into them: the window,
        // the container and the first keystroke all come after this line,
        // and all three can throw.
        CrashGuard.Install();

        try {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        } finally {
            // The last lines matter as much as the first: a log left
            // unflushed is a log missing whatever the crash was about.
            Logs.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
