using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Shiranuix.Graphs;
using Shiranuix.Tests.Fakes;
using Shiranuix.Tests.Headless;
using static Shiranuix.Tests.Graphs.GraphGlass;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The canvas's own moves, headless: the hand pointing at a tie's words
/// and opening them where they stand, the hand taking a tie along its
/// line and leaving it bowed wherever it was let go, the hand dragging a
/// tie out of a node's rim to the node it should join, and the empty
/// paper asked twice and answering nothing — with the store hearing every
/// answer the moment the hand lets go, not at closing time.
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

            // The dot is there for every tie, standing clear of the line on
            // the far side from its words: a press on it puts the tie under
            // the eye in blue and takes hold of the curve in one touch.
            var dot = ScreenOf(map.Canvas, 500, 280);
            map.Window.MouseDown(dot, MouseButton.Left);
            var edge = Assert.IsType<GraphEdgeViewModel>(map.State.SelectedEdge);

            map.Window.MouseMove(ScreenOf(map.Canvas, 510, 350));
            map.Window.MouseUp(ScreenOf(map.Canvas, 510, 350), MouseButton.Left);

            // The hand holds the middle fifty world units below the line,
            // and a quadratic bows half as far as its control point bends.
            Assert.Equal(100, edge.Bend);
            Assert.Equal(
                100,
                map.Store.LoadGraph(map.State.CurrentGraph!.Id).Edges.Single().Bend);

            map.Window.Close();
        });

    [Fact]
    public void Clicking_A_Ties_Words_Opens_The_Box_Instead_Of_The_Dialog() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();

            // The words stand twenty world units off the line, on a pill of
            // their own over everything — clear of the line they name.
            var words = ScreenOf(map.Canvas, 500, 320);
            map.Window.MouseDown(words, MouseButton.Left);
            map.Window.MouseUp(words, MouseButton.Left);

            var edge = Assert.IsType<GraphEdgeViewModel>(map.State.Edges.Single());
            Assert.Same(edge, map.State.EditingLabel);
            Assert.Same(edge, map.State.SelectedEdge);
            Assert.Empty(map.Dialogs.Asked);

            // The pill stands wholly below the line it names: pressed
            // towards, it never touches the tie it is talking about.
            var line = ScreenOf(map.Canvas, 500, 300);
            Assert.True(map.Canvas.TryGetLabelBox(edge, out var pill));
            Assert.True(pill.Y > line.Y);

            // Thrown away: the tie says what it always said.
            map.State.CancelLabelEdit();
            Assert.Null(map.State.EditingLabel);

            // The right hand asks for the same box at the same place.
            map.Window.MouseDown(words, MouseButton.Right);
            map.Window.MouseUp(words, MouseButton.Right);
            Assert.Same(edge, map.State.EditingLabel);

            map.State.CommitLabelEdit("the old rival of");

            Assert.Null(map.State.EditingLabel);
            Assert.Equal(
                "the old rival of",
                map.Store.LoadGraph(map.State.CurrentGraph!.Id).Edges.Single().Label);

            map.Window.Close();
        });

    [Fact]
    public void Taking_A_Tie_Along_Its_Line_Bends_It_Without_Being_Chosen_First() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();

            var line = ScreenOf(map.Canvas, 500, 300);
            map.Window.MouseDown(line, MouseButton.Left);
            map.Window.MouseMove(ScreenOf(map.Canvas, 510, 350));
            map.Window.MouseUp(ScreenOf(map.Canvas, 510, 350), MouseButton.Left);

            var edge = Assert.IsType<GraphEdgeViewModel>(map.State.SelectedEdge);
            Assert.Equal(100, edge.Bend);
            Assert.Equal(
                100,
                map.Store.LoadGraph(map.State.CurrentGraph!.Id).Edges.Single().Bend);

            map.Window.Close();
        });

    [Fact]
    public void Dragging_Out_Of_A_Nodes_Rim_Joins_It_To_Where_The_Hand_Lets_Go() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();
            var mara = map.State.Nodes.First(node => node.Name == "Mara");
            var tomas = map.State.Nodes.First(node => node.Name == "Tomas");

            // Out of Mara's rim and onto empty paper: nothing asked, and
            // the node exactly where it stood.
            var rim = ScreenOf(map.Canvas, 420, 300);
            map.Window.MouseDown(rim, MouseButton.Left);
            map.Window.MouseMove(ScreenOf(map.Canvas, 500, 150));
            map.Window.MouseUp(ScreenOf(map.Canvas, 500, 150), MouseButton.Left);

            Assert.Empty(map.Dialogs.Asked);
            Assert.Equal((400, 300), (mara.X, mara.Y));

            // Again from the same rim — pressed over the same place twice,
            // which must still be a tie dragged out rather than a question
            // about the node standing there.
            map.Window.MouseDown(rim, MouseButton.Left);
            map.Window.MouseMove(ScreenOf(map.Canvas, 600, 300));
            map.Window.MouseUp(ScreenOf(map.Canvas, 600, 300), MouseButton.Left);

            Assert.Contains(
                map.Dialogs.Asked,
                question => question.StartsWith($"relationship:{mara.Id}:{tomas.Id}:"));

            // The dialog went unanswered, so nothing new stands in the
            // store — only the tie that was already there from the start.
            Assert.Single(map.Store.LoadGraph(map.State.CurrentGraph!.Id).Edges);

            map.Window.Close();
        });

    [Fact]
    public void Pressing_Empty_Paper_Twice_Adds_Nobody() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();

            var empty = ScreenOf(map.Canvas, 800, 550);
            map.Window.MouseDown(empty, MouseButton.Left);
            map.Window.MouseUp(empty, MouseButton.Left);
            map.Window.MouseDown(empty, MouseButton.Left);
            map.Window.MouseUp(empty, MouseButton.Left);

            Assert.Equal(2, map.State.Nodes.Count);
            Assert.Empty(map.Dialogs.Asked);

            map.Window.Close();
        });

    [Fact]
    public void Arranging_The_Graph_Brings_The_Whole_Of_It_Into_The_Glass() =>
        HeadlessUi.On(() => {
            var map = OpenedMap();

            // The arrangement is the writer's own asking for the graph to
            // be laid out afresh — and the paper answers by bringing the
            // whole of it into the glass, so that no part of the graph
            // stands off the paper and has to be looked for.
            map.State.RelayoutCommand.Execute(null);
            Pump();

            foreach (var node in map.State.Nodes) {
                var at = map.Canvas.Viewport.ToScreen(new Point(node.X, node.Y));

                Assert.InRange(at.X, 0, map.Canvas.Bounds.Width);
                Assert.InRange(at.Y, 0, map.Canvas.Bounds.Height);
            }

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
        var state = new GraphState(store, store.Wiki, dialogs);

        var graph = store.CreateGraph("Witch And Magical");
        var mara = store.AddWikiCharacter(graph, "Mara", "", 400, 300);
        var tomas = store.AddWikiCharacter(graph, "Tomas", "", 600, 300);
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
}
