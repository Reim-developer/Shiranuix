using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Serilog;
using Shiranuix.Views;

namespace Shiranuix.Logging;

/// <summary>
/// What the application does when something throws that nobody caught:
/// tell the person at the screen — a code they can quote and the file the
/// whole story went into — and then carry on where carrying on is safe,
/// or leave where it is not.
/// </summary>
/// <remarks>
/// <para>
/// Three nets, from the most survivable to the least. The dispatcher's
/// catches what the interface throws while drawing it, marks it handled,
/// and the window keeps running: a keystroke the input method choked on
/// should not take the document with it. The task scheduler catches what
/// fire-and-forget work dropped and observes it the same way — left
/// unobserved, it would arrive at the finalizer instead and take the
/// process down at some arbitrary later moment. Only what reaches the
/// application domain is past helping: the process is already on its way
/// out, so the dialog is shown there and then, and the exit waits for the
/// reader rather than racing them.
/// </para>
/// <para>
/// <see cref="Install"/> is called from the very start of Main — the nets
/// have to be up before the window, the container or the first keystroke
/// can fall into them. The dialog itself is built with <c>new</c> rather
/// than through the container, on purpose: at crash time the machinery
/// worth trusting least is the machinery.
/// </para>
/// </remarks>
public static class CrashGuard {
    // One dialog at a time. A second incident while the first is on
    // screen goes to the log alone: stacked windows tell nobody anything.
    private static int _showing;

    /// <summary>
    /// Puts the three nets in place. Main calls it once, before anything
    /// exists that could throw.
    /// </summary>
    public static void Install() {
        Dispatcher.UIThread.UnhandledException += OnInterface;
        TaskScheduler.UnobservedTaskException += OnBackground;
        AppDomain.CurrentDomain.UnhandledException += OnFatal;
    }

    // The interface threw while drawing itself. Report it, take it — the
    // dispatcher's Handled flag is what keeps the window alive — and ask
    // for the dialog without holding anything up: the exception unwinds
    // first, the dialog is presented on the next turn of the pump.
    private static void OnInterface(
        object? sender, DispatcherUnhandledExceptionEventArgs e) {
        var code = Report(e.Exception, "the interface");
        e.Handled = true;
        Offer(code, e.Exception, closing: false);
    }

    // Work thrown into the void came down with something. Observe it —
    // the finalizer must not find it later — and say so the same way.
    private static void OnBackground(
        object? sender, UnobservedTaskExceptionEventArgs e) {
        e.SetObserved();

        var error = e.Exception?.GetBaseException()
            ?? new Exception("Background work failed without saying why.");
        var code = Report(error, "background work");
        Offer(code, error, closing: false);
    }

    // Past everything else: the process is on its way out. Report, show,
    // and hold this handler open until the dialog is closed — the exit
    // happens when it returns, so everything between here and there is
    // the reader's to spend.
    private static void OnFatal(object? sender, UnhandledExceptionEventArgs e) {
        var error = e.ExceptionObject as Exception
            ?? new Exception($"Something threw without an exception: {e.ExceptionObject}");
        var code = Report(error, "a place the application cannot come back from");
        Farewell(code, error);
    }

    // One line in the log with the whole exception beneath it, carrying
    // the same code the dialog shows — so a code read out loud finds its
    // own line and the stack underneath it.
    private static string Report(Exception error, string where) {
        var code = NextCode();
        Log.Error(error, "Unhandled exception in {Where} — incident {Incident}", where, code);
        return code;
    }

    // A code a person can read aloud and a log can be searched by: the
    // prefix, the day, and four hex digits — nothing to remember in
    // order, only to match.
    private static string NextCode() =>
        $"SX-{DateTime.Now:yyMMdd}-{Guid.NewGuid().ToString("N")[..4]}";

    // The survived path: the application keeps running, so the dialog is
    // asked for politely — posted, so the exception unwinds first — and
    // only one is ever up at a time.
    private static void Offer(string code, Exception error, bool closing) {
        if (Interlocked.Exchange(ref _showing, 1) == 1) {
            return;
        }

        Dispatcher.UIThread.Post(async () => {
            try {
                await Present(code, error, closing);
            } catch (Exception failure) {
                Log.Warning(failure, "The crash dialog could not be shown — incident {Incident}", code);
            } finally {
                Interlocked.Exchange(ref _showing, 0);
            }
        });
    }

    private static async Task Present(string code, Exception error, bool closing) {
        var dialog = new CrashDialog(error, code, Logs.CurrentFile, closing);
        var owner = MainWindow;

        if (owner is { IsVisible: true }) {
            // Held up as a modal against the window it is about: the
            // reader decides before touching the document again.
            await dialog.ShowDialog<object?>(owner);
        } else {
            // No window to be a modal against — a modeless one on the
            // desk will do. The guard is held until it closes, so the
            // next incident waits its turn like the first did.
            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.Show();
            await closed.Task;
        }
    }

    // The terminal path: show the dialog and keep this handler — and so
    // the process — alive until it has been read.
    private static void Farewell(string code, Exception error) {
        try {
            if (Dispatcher.UIThread.CheckAccess()) {
                // Already on the interface's thread: show and pump right
                // here — the stack has unwound to this point, and there
                // will be no other turn of the pump after it.
                PumpUntilRead(code, error);
            } else {
                // A background thread took the process down: the pump
                // still runs, so hand the showing over to it and wait.
                Dispatcher.UIThread.Invoke(() => PumpUntilRead(code, error));
            }
        } catch (Exception failure) {
            // Past remedy either way: the code is in the log already and
            // the process is leaving regardless — nothing here can make
            // the exit more abrupt than the crash already has.
            Log.Warning(failure, "The crash dialog could not be shown — incident {Incident}", code);
        }
    }

    private static void PumpUntilRead(string code, Exception error) {
        var dialog = new CrashDialog(error, code, Logs.CurrentFile, closing: true);
        dialog.Show();

        // A dialog on a thread that will not loop again after this keeps
        // itself up by running the pump until it closes. The half minute
        // is not for the reader — it is for the process: if the dialog is
        // itself what went wrong, nothing hangs the exit on it.
        var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        dialog.Closed += (_, _) => giveUp.Cancel();
        Dispatcher.UIThread.MainLoop(giveUp.Token);
    }

    // The window to hold a dialog up as a modal, when there is one — the
    // lifetime knows it, and nobody has to pass it around.
    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
