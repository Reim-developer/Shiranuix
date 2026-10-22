using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Shiranuix.Graphs;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// Where the relationship map's glass stands in a test: one frame waited
/// for, and a place in the world asked where it lands on the glass — the
/// same answer for every window the map is opened in, so a gesture written
/// against one is the same gesture in another.
/// </summary>
internal static class GraphGlass {
    /// <summary>Runs the dispatcher and the render timer until a frame stands drawn.</summary>
    public static void Pump() {
        for (var i = 0; i < 5; i++) {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Where a place in the world stands on the glass, at the one zoom and
    /// offset the canvas stands at until a hand says otherwise.
    /// </summary>
    public static Point ScreenOf(GraphCanvas canvas, double x, double y) =>
        new(
            x + canvas.Bounds.Width / 2 - GraphState.WorldWidth / 2,
            y + canvas.Bounds.Height / 2 - GraphState.WorldHeight / 2);
}
