using System;
using System.Runtime.Versioning;
using Avalonia;
using Serilog;
using Shiranuix.Logging;
using Shiranuix.Services.FileAssociations;

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
            // Asked only to be taken back out of the machine's own menus,
            // the editor takes itself back out and goes. It is done here,
            // before the engine is wound up at all: a window has nothing to
            // show for it, nothing is being opened, and the whole point is
            // to be quick about it.
            if (OperatingSystem.IsWindows() && AskedToUnregister(args)) {
                UnregisterFromWindows();
                return;
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        } finally {
            // The last lines matter as much as the first: a log left
            // unflushed is a log missing whatever the crash was about.
            Logs.CloseAndFlush();
        }
    }

    /// <summary>
    /// Whether this launch asked only for the editor to be taken back out of
    /// the machine's own menus, and for nothing else.
    /// </summary>
    /// <param name="args">What the editor was started with.</param>
    /// <returns>True when the flag is there, however it was said.</returns>
    /// <remarks>
    /// The whole reason this is a flag rather than a window: the editor's own
    /// menus were put there by the editor alone, so taking them back needs
    /// nobody's agreement, costs nothing, and has nothing to show for itself.
    /// A person asking for this wants the right-click menu they were given
    /// without being given it, and does not want an editor about it.
    /// </remarks>
    private static bool AskedToUnregister(string[] args) =>
        args.Any(arg => arg.Equals("--unregister", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Takes back everything the editor put into the current user's half of
    /// the registry.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than by the container, for the same reason the
    /// rest of this method is: there is no container yet, and there is
    /// nothing here that would want one. It is guarded rather than trusted,
    /// because a registry that refuses to be written is still a machine that
    /// has to run the editor.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static void UnregisterFromWindows() {
        try {
            new WindowsFileAssociationService().Unregister();
        } catch (Exception failure) {
            Log.Warning(failure, "Windows could not be let go of Shiranuix.");
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
