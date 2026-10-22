using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shiranuix.Graphs;
using Shiranuix.Graphs.Views;
using Shiranuix.Tests.Fakes;
using Shiranuix.Tests.Headless;
using static Shiranuix.Tests.Graphs.GraphGlass;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The words box in the real window: pointed at on the paper, it stands
/// over the very pill it means to rewrite — and whatever is typed into it
/// reaches the store by every road out — Enter hurrying it through, a hand
/// walking away taking it along, and Escape throwing it away whole.
/// </summary>
/// <remarks>
/// The box lives in the window rather than the canvas, so the canvas's own
/// coordinates are walked to the window's before the press: the glass
/// stands wherever the bands above leave it, and its own place on the
/// paper is read back rather than counted on.
/// </remarks>
[Collection("Headless UI")]
public sealed class GraphWindowTests {
    private readonly FakeWikiWindowService _wikiWindow = new();

    [Fact]
    public void Pointing_At_A_Ties_Words_Stands_The_Box_Over_Them() =>
        HeadlessUi.On(() => {
            var (state, store, window, canvas, box) = OpenedWindow();

            Press(window, canvas, 500, 320, MouseButton.Left);

            Assert.True(box.IsVisible);
            Assert.Equal("the teacher of", box.Text);
            Assert.True(box.IsFocused);

            box.Text = "the old rival of";
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

            Assert.False(box.IsVisible);
            Assert.Equal(
                "the old rival of",
                store.LoadGraph(state.CurrentGraph!.Id).Edges.Single().Label);

            window.Close();
        });

    [Fact]
    public void Walking_Away_Takes_The_Words_And_Escape_Throws_Them_Away() =>
        HeadlessUi.On(() => {
            var (state, store, window, canvas, box) = OpenedWindow();

            // Typed and then walked away from: the ordinary way an editor
            // agrees with the writer.
            Press(window, canvas, 500, 320, MouseButton.Left);
            box.Text = "a wandering thought";
            Press(window, canvas, 850, 480, MouseButton.Left);

            // The hand went to the paper: focus went with it, the box shut,
            // and the words were taken the moment the hand left.
            Assert.True(canvas.IsFocused);
            Assert.False(box.IsVisible);
            Assert.Equal(
                "a wandering thought",
                store.LoadGraph(state.CurrentGraph!.Id).Edges.Single().Label);

            // Opened again, changed again, and this time refused: the tie
            // keeps what the walking away had already accepted.
            Press(window, canvas, 500, 320, MouseButton.Left);
            Assert.Equal("a wandering thought", box.Text);

            box.Text = "thrown away on purpose";
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

            Assert.False(box.IsVisible);
            Assert.Equal(
                "a wandering thought",
                store.LoadGraph(state.CurrentGraph!.Id).Edges.Single().Label);

            window.Close();
        });

    /// <summary>
    /// A whole window over one graph: Mara and Tomas standing apart, one
    /// straight tie between them, the words box found by the name it wears
    /// in the XAML, and a press at a place in the world walked from the
    /// glass's own frame to the window's.
    /// </summary>
    private (GraphState State, FakeGraphStore Store, Window Window, GraphCanvas Canvas, TextBox Box)
        OpenedWindow() {
        var store = new FakeGraphStore();
        var dialogs = new FakeGraphDialogService();
        var state = new GraphState(store, store.Wiki, dialogs, _wikiWindow);

        var graph = store.CreateGraph("Witch And Magical");
        var mara = store.AddWikiCharacter(graph, "Mara", "", 400, 300);
        var tomas = store.AddWikiCharacter(graph, "Tomas", "", 600, 300);
        store.AddRelationship(graph, mara, tomas, "the teacher of");
        state.Load();

        var window = new GraphWindow(state);
        window.Show();
        Pump();

        var canvas = window.FindControl<GraphCanvas>("GraphPane")!;
        var box = window.FindControl<TextBox>("WordsEdit")!;
        Assert.Equal(new Rect(0, 0, 980, 720), window.Bounds);

        return (state, store, window, canvas, box);
    }

    /// <summary>Presses the glass at a place in the world, whole from down to up.</summary>
    private static void Press(Window window, GraphCanvas canvas, double x, double y, MouseButton button) {
        // The glass knows only its own bounds; the frame it hangs in —
        // the bands above it and all — is walked up to, so the press
        // lands where the writer means it in the window's own coordinates.
        var at = ScreenOf(canvas, x, y);
        for (var visual = (Visual?)canvas; visual is not null && visual is not Window; visual = visual.GetVisualParent()) {
            at = new Point(at.X + visual.Bounds.Position.X, at.Y + visual.Bounds.Position.Y);
        }

        window.MouseDown(at, button);
        window.MouseUp(at, button);
    }
}
