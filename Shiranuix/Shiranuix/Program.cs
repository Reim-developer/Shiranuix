using System;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.TextFormatting;
using Serilog;
using Shiranuix.Logging;
using Shiranuix.Services.FileAssociations;
using Shiranuix.TextRender;

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

        // Asked to prove the build rather than to be used — a runner at
        // the door, not a person — the editor stands itself up without a
        // screen, says one line, and leaves. It happens before the nets go
        // up on purpose: there is nobody here to show a dialog to, and a
        // dialog with nobody to close it is a runner that waits until it
        // is killed. A failure is left to take the process down with it,
        // which is exactly the answer the runner asked for.
        if (Smoke.IsAsked(args)) {
            // Flushed on the way out like every other run: the smoke's
            // line goes to the file as well as the console, and a process
            // that lives shorter than the sink's flush interval would
            // otherwise leave without it — and that line is the one a
            // reader goes to the log to find.
            try {
                Smoke.Run(args);
            } finally {
                Logs.CloseAndFlush();
            }
            return;
        }

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
    public static AppBuilder BuildAvaloniaApp() {
        // A page's lines each stand at their own edge, and the framework
        // asks for a text formatter once, by name, and keeps the answer
        // for the life of the process — so the one that can answer per
        // line is stood in here, before anything is laid out. The
        // framework's own formatter is captured first and handed back for
        // every question outside a layout that asked for per-line edges,
        // so everything else in the app is answered as it always was.
        // Typed as the framework's own so that it is that name the answer
        // is registered under.
        TextFormatter fallback = TextFormatter.Current;
        TextFormatter perLine = new LineAlignedFormatter(fallback);

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .With(perLine)
            .LogToTrace();
    }

    /// <summary>
    /// The same builder the real start takes, standing on the headless
    /// platform rather than the machine's: every window, every layout and
    /// the frame the smoke draws are real — through the same Skia — and no
    /// screen is asked for anything.
    /// </summary>
    /// <remarks>
    /// Only <see cref="Smoke"/> walks this way in. The formatter stand-in
    /// is the same one the desktop start takes, because a line laid out
    /// for the smoke should stand the way it would on a screen.
    /// </remarks>
    public static AppBuilder BuildAvaloniaAppHeadless() {
        TextFormatter fallback = TextFormatter.Current;
        TextFormatter perLine = new LineAlignedFormatter(fallback);

        return AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions {
                // Real drawing rather than the stub: the point of the
                // smoke is that the shipped drawing pieces work on this
                // system, and a stub would answer for them.
                UseHeadlessDrawing = false
            })
            .WithInterFont()
            .With(perLine)
            .UseSkia()
            .LogToTrace();
    }
}
