using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using PdfSharp.Pdf;
using Serilog;
using Shiranuix.Views;

// The paper engine rather than PDFsharp's page collection of the same
// name, which the using above would otherwise leave ambiguous.
using PdfPages = Shiranuix.Services.Pdf.PdfPages;

namespace Shiranuix;

/// <summary>
/// The editor's answer to a runner's question: started with <c>--smoke</c>,
/// the build stands itself up without a screen, draws one real frame, reads
/// one paper, says so on the console, and leaves.
/// </summary>
/// <remarks>
/// <para>
/// This is the published file answering, not the tests: the same single
/// executable a release ships, on the very system it ships to, with every
/// native piece it carries — drawing, shaping, the paper engine — asked to
/// actually work there. The tests prove the code; only this proves the
/// artifact.
/// </para>
/// <para>
/// Headless rather than hidden: no window server is asked for anything, so
/// the same line runs where no display exists at all. The windows are real
/// and the layout is real, and the frame drawn below goes through the same
/// Skia a screen would.
/// </para>
/// </remarks>
public static class Smoke {
    // Read once, at the flag being noticed: the milliseconds reported at
    // the end count from there, which is as close to the process starting
    // as this class is allowed to stand.
    private static readonly Stopwatch Watched = Stopwatch.StartNew();

    /// <summary>
    /// Whether this launch asked the build to prove itself rather than to
    /// be used — a runner asking, not a person.
    /// </summary>
    /// <param name="args">What the editor was started with.</param>
    /// <returns>True when the flag is there, however it was said.</returns>
    public static bool IsAsked(string[]? args) =>
        args is { } started && started.Any(arg =>
            arg.Equals("--smoke", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Brings the editor up on the headless platform and runs until it
    /// leaves — which is arranged by <see cref="Arrive"/>, the moment the
    /// window has stood up.
    /// </summary>
    /// <param name="args">What the editor was started with.</param>
    public static void Run(string[] args) {
        // Said before anything can hang: a runner whose last line is this
        // one knows the smoke is still on its way up rather than stuck
        // somewhere quieter.
        Console.WriteLine("smoke: standing the editor up without a screen...");

        Program.BuildAvaloniaAppHeadless().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Called once the startup has built the window: draws the frame,
    /// reads the paper, tells the console, and leaves with the code 0 a
    /// clean departure carries.
    /// </summary>
    /// <param name="desktop">The lifetime the window stands in.</param>
    /// <param name="window">The window the startup just built.</param>
    /// <remarks>
    /// The work is posted rather than done here: framework initialization
    /// is still finishing, and the window is shown by the lifetime on its
    /// way out of it. Posted at the back of the queue, everything above has
    /// run for real before the first question below is asked.
    /// </remarks>
    public static void Arrive(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window) {
        Dispatcher.UIThread.Post(() => {
            if (!window.IsVisible) {
                throw new InvalidOperationException("The smoke's window never came up.");
            }

            // The frame a screen would show, drawn by hand: a headless
            // runner schedules no frames of its own, so the compositor is
            // pushed once under a laid-out window. A build whose drawing
            // pieces are missing fails here rather than standing a window
            // that never painted.
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            ReadAPaper();

            var line =
                $"smoke: the editor stood up in {Watched.ElapsedMilliseconds} ms — " +
                "this system can run it.";
            Log.Information("{Smoke}", line);
            Console.WriteLine(line);

            desktop.Shutdown(0);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Reads one page of one paper through the engine the reading window
    /// uses: PDFium is native code that has to be aboard this build for
    /// this system, and this is where the runner finds out that it is.
    /// </summary>
    private static void ReadAPaper() {
        var home = Path.Combine(Path.GetTempPath(), $"shiranuix-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(home);
        var paper = Path.Combine(home, "paper.pdf");

        try {
            // An empty page is whole paper for this question: written by
            // the same library that reads the sizes back, carrying no
            // typeface, so nothing here asks the machine what fonts it
            // keeps. The drawing is PDFium's own either way, and that is
            // the piece this is here to find.
            using (var written = new PdfDocument()) {
                written.AddPage();
                written.Save(paper);
            }

            var read = new PdfPages(paper);

            if (read.Count != 1) {
                throw new InvalidDataException($"The smoke's paper carries {read.Count} pages.");
            }

            using var drawn = read.Draw(0, 64);

            if (drawn.Width != 64 || drawn.Height <= 0) {
                throw new InvalidDataException(
                    $"The smoke's page drew at {drawn.Width} by {drawn.Height}.");
            }
        } finally {
            Directory.Delete(home, recursive: true);
        }
    }
}
