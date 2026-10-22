using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Shiranuix.Tests.Headless;

/// <summary>
/// The one thread a headless Avalonia stands on, and the way a test sends
/// its whole body there: the platform is built once per process, and every
/// window, key and press happens on the thread the dispatcher was born on,
/// because a dispatcher that has been asked from somewhere else answers
/// nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// The xunit integration for headless Avalonia now speaks xunit v3, and
/// this suite speaks v2 — so the small part of it that matters is written
/// out here instead: build the app once, run each test's body on the UI
/// thread, and hand back whatever went wrong where the test can see it.
/// </para>
/// <para>
/// The building is set running at module load, so that the first thread to
/// touch the dispatcher is this one: the tests elsewhere in this assembly
/// that merely construct controls would otherwise have claimed it first,
/// leaving the platform's own setup to fail the moment it asked for what
/// they had taken. The module initializer itself does not wait — it runs
/// inside the assembly's own loading, and a thread started from inside
/// that has loading of its own to do — so it starts the platform and lets
/// the first test wait for it, long after loading has ended.
/// </para>
/// </remarks>
internal static class HeadlessUi {
    private static readonly BlockingCollection<Action> Work = new();
    private static readonly ManualResetEventSlim Ready = new();
    private static Exception? _startupFailure;
    private static int _started;

    /// <summary>
    /// Stands the platform up before any test can claim the dispatcher for
    /// itself — the assembly's very first act, and only a beginning: this
    /// does not wait for the standing up to finish, and does not need to.
    /// </summary>
    [ModuleInitializer]
    internal static void Begin() => Start(waitForPlatform: false);

    /// <summary>
    /// Runs a test's whole body on the headless platform's own thread,
    /// once the platform has been built, and rethrows there whatever the
    /// body threw here.
    /// </summary>
    /// <param name="body">Everything the test means to do with its windows.</param>
    public static void On(Action body) {
        Start();

        if (_startupFailure is not null) {
            ExceptionDispatchInfo.Capture(_startupFailure).Throw();
        }

        using var done = new ManualResetEventSlim();
        Exception? failure = null;

        Work.Add(() => {
            try {
                body();
            } catch (Exception e) {
                failure = e;
            } finally {
                done.Set();
            }
        });

        done.Wait();

        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void Start(bool waitForPlatform = true) {
        if (Interlocked.Exchange(ref _started, 1) == 0) {
            var thread = new Thread(() => {
                try {
                    // Skia rather than the headless drawing stand-in: a
                    // canvas that draws itself in Render only draws at all
                    // when there is a renderer to draw for, and the map's
                    // tests read the frame that drawing stands the world
                    // up in.
                    AppBuilder.Configure<HeadlessApp>()
                        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                        .UseSkia()
                        .SetupWithoutStarting();
                } catch (Exception e) {
                    _startupFailure = e;
                } finally {
                    Ready.Set();
                }

                foreach (var work in Work.GetConsumingEnumerable()) {
                    work();
                }
            }) {
                IsBackground = true,
                Name = "Headless Avalonia tests"
            };

            thread.Start();
        }

        if (waitForPlatform) {
            Ready.Wait();
        }
    }
}

/// <summary>The application the headless tests stand in: Fluent themes, and nothing else.</summary>
internal sealed class HeadlessApp : Application {
    /// <inheritdoc />
    public override void Initialize() => Styles.Add(new FluentTheme());
}

/// <summary>
/// The collection the headless tests share: one UI thread, so they take
/// turns rather than standing in each other's windows.
/// </summary>
[CollectionDefinition("Headless UI", DisableParallelization = true)]
public sealed class HeadlessUiCollection;
