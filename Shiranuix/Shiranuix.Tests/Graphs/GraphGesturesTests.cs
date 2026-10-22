using Avalonia;
using Avalonia.Headless;
using Shiranuix.Graphs;
using Shiranuix.Tests.Fakes;
using Shiranuix.Tests.Headless;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The map's own vocabulary of presses, asked without a window: a node
/// taken in its middle, a tie taken along its line, a tie dragged out of a
/// rim, the words of a tie opened where they stand, and the paper swept
/// clean when a hand lets go of it without having moved it.
/// </summary>
/// <remarks>
/// The gestures live apart from the canvas so that they can be asked about
/// like this: a test says where the hand stands and what the hand means,
/// and reads back what the map did about it.
/// </remarks>
[Collection("Headless UI")]
public sealed class GraphGesturesTests {
    private readonly FakeGraphStore _store = new();
    private readonly FakeGraphDialogService _dialogs = new();
    private readonly FakeWikiWindowService _wikiWindow = new();
    private readonly GraphState _state;
    private readonly GraphViewport _viewport = new();
    private readonly GraphGestures _gestures;

    private readonly int _mara;
    private readonly int _tomas;

    public GraphGesturesTests() {
        _state = new GraphState(_store, _store.Wiki, _dialogs, _wikiWindow);

        var graph = _store.CreateGraph("Witch And Magical");
        _mara = _store.AddWikiCharacter(graph, "Mara", "", 400, 300);
        _tomas = _store.AddWikiCharacter(graph, "Tomas", "", 600, 300);
        _store.AddRelationship(graph, _mara, _tomas, "the teacher of");
        _state.Load();

        var geometry = new GraphEdgeGeometry(_viewport);
        _gestures = new GraphGestures(
            new GraphHitTester(_viewport, geometry, new GraphTextCache()),
            geometry,
            _viewport);
    }

    [Fact]
    public void A_Node_Taken_In_Its_Middle_Goes_Where_The_Hand_Goes() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(400, 300), true, false, false, 1);

            Assert.Equal(GraphGesture.Drag, press.Gesture);

            _gestures.Move(_state, new Point(450, 350));

            var mara = Mara();
            Assert.Equal(450, mara.X);
            Assert.Equal(350, mara.Y);
        });

    [Fact]
    public void A_Node_Left_Where_It_Was_Let_Go_Of_Is_Kept_By_The_Store() =>
        HeadlessUi.On(() => {
            _gestures.Press(_state, new Point(400, 300), true, false, false, 1);
            _gestures.Move(_state, new Point(450, 350));
            _gestures.Release(_state);

            var stored = _store.LoadGraph(_state.CurrentGraph!.Id).Nodes.Single(c => c.Name == "Mara");
            Assert.Equal(450, stored.X);
            Assert.Equal(350, stored.Y);
        });

    [Fact]
    public void A_Node_Only_Clicked_Is_Selected_Rather_Than_Moved() =>
        HeadlessUi.On(() => {
            _gestures.Press(_state, new Point(400, 300), true, false, false, 1);
            _gestures.Release(_state);

            Assert.Same(Mara(), _state.SelectedNode);
            Assert.Equal(400, Mara().X);
            Assert.Equal(300, Mara().Y);
        });

    [Fact]
    public void A_Tie_Taken_Along_Its_Line_Bows_And_The_Store_Keeps_The_Curve() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(500, 300), true, false, false, 1);

            Assert.Equal(GraphGesture.Bend, press.Gesture);

            _gestures.Move(_state, new Point(510, 350));
            _gestures.Release(_state);

            Assert.Equal(100, _state.Edges.Single().Bend);
            Assert.Equal(100, _store.LoadGraph(_state.CurrentGraph!.Id).Edges.Single().Bend);
        });

    [Fact]
    public void A_Tie_Dragged_Out_Of_One_Rim_And_Let_Go_On_Another_Asks_For_The_Tie() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(425, 300), true, false, false, 1);

            Assert.Equal(GraphGesture.Link, press.Gesture);

            _gestures.Move(_state, new Point(600, 300));
            _gestures.Release(_state);

            Assert.Contains(
                _dialogs.Asked,
                question => question.StartsWith($"relationship:{_mara}:{_tomas}:"));
        });

    [Fact]
    public void A_Tie_Dragged_Out_Of_A_Rim_And_Let_Go_On_Empty_Paper_Asks_Nobody() =>
        HeadlessUi.On(() => {
            _gestures.Press(_state, new Point(425, 300), true, false, false, 1);
            _gestures.Move(_state, new Point(900, 500));
            _gestures.Release(_state);

            Assert.Empty(_dialogs.Asked);
        });

    [Fact]
    public void The_Words_Of_A_Tie_Open_Their_Box_Where_They_Stand() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(498, 320), true, false, false, 1);

            Assert.Equal(GraphGesture.OpenWords, press.Gesture);
            Assert.Same(_state.Edges.Single(), _state.EditingLabel);
        });

    [Fact]
    public void A_Double_Press_On_A_Ties_Dot_Asks_About_The_Tie_And_Takes_No_Hold() =>
        HeadlessUi.On(() => {
            // The dot stands off the line, twenty world units above it.
            var press = _gestures.Press(_state, new Point(498, 280), true, false, false, 2);

            Assert.Equal(GraphGesture.None, press.Gesture);
        });

    [Fact]
    public void The_Right_Hand_Asks_The_Node_For_Its_Small_Vocabulary() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(400, 300), false, false, true, 1);

            Assert.Equal(GraphGesture.NodeMenu, press.Gesture);
            Assert.Same(Mara(), press.Node);
        });

    [Fact]
    public void A_Double_Press_On_A_Node_Asks_What_It_Says() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(400, 300), true, false, false, 2);

            Assert.Equal(GraphGesture.EditNode, press.Gesture);
            Assert.Contains(_dialogs.Asked, question => question.StartsWith("character:Mara:"));
        });

    [Fact]
    public void A_Double_Press_On_A_Rim_Is_Still_The_Start_Of_A_Tie() =>
        HeadlessUi.On(() => {
            var press = _gestures.Press(_state, new Point(425, 300), true, false, false, 2);

            Assert.Equal(GraphGesture.Link, press.Gesture);
        });

    [Fact]
    public void The_Paper_Let_Go_Of_Without_A_Drag_Sweeps_The_Selection_Away() =>
        HeadlessUi.On(() => {
            _state.SelectNode(Mara());

            _gestures.Press(_state, new Point(900, 500), true, false, false, 1);
            _gestures.Release(_state);

            Assert.Null(_state.SelectedNode);
        });

    /// <summary>Mara, as the canvas carries her.</summary>
    private GraphNodeViewModel Mara() => _state.Nodes.First(node => node.Id == _mara);
}
