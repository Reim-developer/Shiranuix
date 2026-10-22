using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Shiranuix.Graphs;
using Shiranuix.Tests.Fakes;
using Shiranuix.Tests.Headless;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The canvas's own moves, headless: the right hand asking a tie what it
/// says, and the hand that takes the dot at a selected tie's middle and
/// leaves its line bent wherever it was let go — with the store hearing
/// the bend the moment the hand lets go, not at closing time.
/// </summary>
/// <remarks>
/// The canvas draws itself at the world's own measure through one zoom and
/// one offset, and stands still for a test: the world's middle in the
/// glass's middle, so a place in the world lands on the glass at that
/// place plus whatever half the glass has more of than the world.
/// </remarks>
[Collection("Headless UI")]
public sealed class GraphCanvasTests {
    [Fact]
    public void Right_Clicking_A_Tie_Asks_What_It_Says_Anew() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();

            map.Dialogs.Answers.Enqueue("the enemy of");

            // The middle of the line from Mara to Tomas, where no circle stands.
            var tie = ScreenOf(map.Canvas, 500, 300);
            map.Window.MouseDown(tie, MouseButton.Right);
            map.Window.MouseUp(tie, MouseButton.Right);

            Assert.Equal("the enemy of", map.State.Edges.Single().Label);
            Assert.Equal(
                "the enemy of",
                map.Store.LoadGraph(map.State.CurrentGraph!.Id).Edges.Single().Label);

            map.Window.Close();
        });

    [Fact]
    public void Dragging_The_Dot_Bends_The_Tie_And_The_Store_Keeps_The_Curve() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();

            // A first press takes the tie under the eye; the second, on the
            // dot it wears at its middle, takes hold of the curve itself.
            var middle = ScreenOf(map.Canvas, 500, 300);
            map.Window.MouseDown(middle, MouseButton.Left);
            map.Window.MouseUp(middle, MouseButton.Left);
            var edge = Assert.IsType<GraphEdgeViewModel>(map.State.SelectedEdge);

            map.Window.MouseDown(middle, MouseButton.Left);
            map.Window.MouseMove(ScreenOf(map.Canvas, 510, 350));
            map.Window.MouseUp(ScreenOf(map.Canvas, 510, 350), MouseButton.Left);

            // The hand holds the middle sixty world units below the line,
            // and a quadratic bows half as far as its control point bends.
            Assert.Equal(100, edge.Bend);
            Assert.Equal(
                100,
                map.Store.LoadGraph(map.State.CurrentGraph!.Id).Edges.Single().Bend);

            map.Window.Close();
        });

    /// <summary>
    /// A window over one graph whole: Mara and Tomas standing apart, one
    /// straight tie between them, and nothing standing in the way of the
    /// glass meeting the world measure for measure.
    /// </summary>
    private static (GraphState State, FakeGraphStore Store, FakeGraphDialogService Dialogs, Window Window, GraphCanvas Canvas)
        OpenedMap() {
        var store = new FakeGraphStore();
        var dialogs = new FakeGraphDialogService();
        var state = new GraphState(store, dialogs);

        var graph = store.CreateGraph("Witch And Magical");
        var mara = store.AddCharacter(graph, "Mara", "", 400, 300);
        var tomas = store.AddCharacter(graph, "Tomas", "", 600, 300);
        store.AddRelationship(graph, mara, tomas, "the teacher of");
        state.Load();

        var canvas = new GraphCanvas { State = state };
        var window = new Window {
            Width = 800,
            Height = 600,
            WindowDecorations = WindowDecorations.None,
            Content = canvas
        };
        window.Show();

        // The canvas stands the world up in the middle of the glass on its
        // first frame, and every coordinate here is asked of that frame —
        // so the frame is waited for, and the glass's own measure read back
        // rather than taken on faith.
        Pump();
        Assert.Equal(new Rect(0, 0, 800, 600), canvas.Bounds);

        return (state, store, dialogs, window, canvas);
    }

    /// <summary>Runs the dispatcher and the render timer until a frame stands drawn.</summary>
    private static void Pump() {
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
    private static Point ScreenOf(GraphCanvas canvas, double x, double y) =>
        new(
            x + canvas.Bounds.Width / 2 - GraphState.WorldWidth / 2,
            y + canvas.Bounds.Height / 2 - GraphState.WorldHeight / 2);
}
