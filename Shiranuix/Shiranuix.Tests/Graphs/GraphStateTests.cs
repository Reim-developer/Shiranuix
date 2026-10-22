using Shiranuix.Graphs;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The relationship window's state, over a store and dialogs that only
/// remember what a test tells them: every command the toolbar and the
/// canvas reach for, and every way a writer may refuse to finish one.
/// </summary>
/// <remarks>
/// The state writes through the store before it moves its own collections,
/// so each test reads both: what the canvas would draw, and what the
/// database would hold if the window were closed right there. A state that
/// pleased the canvas while disagreeing with the store is the one failure
/// this file exists to make loud.
/// </remarks>
public sealed class GraphStateTests {
    private readonly FakeGraphStore _store = new();
    private readonly FakeGraphDialogService _dialogs = new();
    private readonly GraphState _state;

    public GraphStateTests() {
        _state = new GraphState(_store, _store.Wiki, _dialogs);
    }

    [Fact]
    public async Task Making_A_Graph_Through_The_Dialog_Stands_It_Open() {
        _dialogs.Answers.Enqueue("Witch And Magical");

        await _state.NewGraphCommand.ExecuteAsync(null);

        var graph = Assert.Single(_state.Graphs);
        Assert.Equal("Witch And Magical", graph.Name);
        Assert.Equal(graph, _state.CurrentGraph);
        Assert.True(_state.HasCurrentGraph);
    }

    [Fact]
    public async Task A_Refused_Name_Is_Asked_For_Again_With_The_Reason() {
        _dialogs.Answers.Enqueue("   ");
        _dialogs.Answers.Enqueue("Witch And Magical");

        await _state.NewGraphCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.Asked.Count);
        Assert.Contains(_dialogs.Asked, question => question.Contains("needs a name"));
        Assert.Equal("Witch And Magical", Assert.Single(_state.Graphs).Name);
    }

    [Fact]
    public async Task A_Name_That_Already_Stands_Is_Asked_For_Again() {
        _store.CreateGraph("Witch And Magical");
        _dialogs.Answers.Enqueue("Witch And Magical");
        _dialogs.Answers.Enqueue("The Other Story");

        await _state.NewGraphCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.Asked.Count);
        Assert.Contains(_dialogs.Asked, question => question.Contains("already stands"));
        Assert.Equal(
            new[] { "The Other Story", "Witch And Magical" },
            _state.Graphs.Select(graph => graph.Name));
    }

    [Fact]
    public async Task Walking_Away_From_The_Question_Makes_Nothing() {
        await _state.NewGraphCommand.ExecuteAsync(null);

        Assert.Empty(_state.Graphs);
        Assert.Null(_state.CurrentGraph);
    }

    [Fact]
    public async Task Adding_A_Character_Puts_Them_On_The_Paper_And_Under_The_Eye() {
        StandOpen("Witch And Magical");
        _dialogs.Answers.Enqueue(new CharacterDraft("Mara", "the witch of the fen"));

        await _state.AddCharacterCommand.ExecuteAsync(null);

        var node = Assert.Single(_state.Nodes);
        Assert.Equal("Mara", node.Name);
        Assert.Equal("the witch of the fen", node.Note);
        Assert.Same(node, _state.SelectedNode);

        var stored = Assert.Single(_store.LoadGraph(_state.CurrentGraph!.Id).Nodes);
        Assert.Equal("Mara", stored.Name);
    }

    [Fact]
    public async Task A_Character_Without_A_Name_Is_Asked_For_Again() {
        StandOpen("Witch And Magical");
        _dialogs.Answers.Enqueue(new CharacterDraft("   ", ""));
        _dialogs.Answers.Enqueue(new CharacterDraft("Mara", ""));

        await _state.AddCharacterCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.Asked.Count);
        Assert.Contains(_dialogs.Asked, question => question.Contains("needs a name"));
        Assert.Equal("Mara", Assert.Single(_state.Nodes).Name);
    }

    [Fact]
    public async Task Walking_Away_From_A_Character_Adds_Nobody() {
        StandOpen("Witch And Magical");

        await _state.AddCharacterCommand.ExecuteAsync(null);

        Assert.Empty(_state.Nodes);
        Assert.Empty(_store.LoadGraph(_state.CurrentGraph!.Id).Nodes);
    }

    [Fact]
    public async Task A_Tie_Is_Drawn_Between_The_Two_Characters_The_Dialog_Picked() {
        var (mara, tomas) = CastOfTwo();
        _dialogs.Answers.Enqueue(new RelationshipDraft(mara, tomas, "the teacher of"));

        await _state.AddRelationshipCommand.ExecuteAsync(null);

        var edge = Assert.Single(_state.Edges);
        Assert.Equal("the teacher of", edge.Label);
        Assert.Equal("Mara", edge.From.Name);
        Assert.Equal("Tomas", edge.To.Name);
        Assert.Same(edge, _state.SelectedEdge);

        var stored = Assert.Single(_store.LoadGraph(_state.CurrentGraph!.Id).Edges);
        Assert.Equal(mara, stored.FromId);
        Assert.Equal(tomas, stored.ToId);
    }

    [Fact]
    public async Task A_Tie_From_A_Character_To_Themselves_Is_Asked_For_Again() {
        var (mara, tomas) = CastOfTwo();
        _dialogs.Answers.Enqueue(new RelationshipDraft(mara, mara, ""));
        _dialogs.Answers.Enqueue(new RelationshipDraft(mara, tomas, "owed"));

        await _state.AddRelationshipCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.Asked.Count);
        Assert.Contains(_dialogs.Asked, question => question.Contains("two different characters"));
        Assert.Single(_state.Edges);
    }

    [Fact]
    public async Task Deleting_A_Character_Takes_Every_Tie_Standing_On_Them() {
        var (mara, tomas) = CastOfTwo();
        _store.AddRelationship(_state.CurrentGraph!.Id, mara, tomas, "the teacher of");

        _state.Load();
        _state.SelectNode(_state.Nodes.First(node => node.Id == mara));

        await _state.DeleteSelectionCommand.ExecuteAsync(null);

        var survivor = Assert.Single(_state.Nodes);
        Assert.Equal("Tomas", survivor.Name);
        Assert.Empty(_state.Edges);
        Assert.Empty(_store.LoadGraph(_state.CurrentGraph!.Id).Edges);
        Assert.Contains(_dialogs.Asked, question => question.StartsWith("confirm:"));
    }

    [Fact]
    public async Task A_Refused_Deletion_Strikes_Nothing_Out() {
        var (mara, _) = CastOfTwo();
        _dialogs.Confirm = false;

        _state.Load();
        _state.SelectNode(_state.Nodes.First(node => node.Id == mara));

        await _state.DeleteSelectionCommand.ExecuteAsync(null);

        Assert.Equal(2, _state.Nodes.Count);
        Assert.Equal(2, _store.LoadGraph(_state.CurrentGraph!.Id).Nodes.Count);
    }

    [Fact]
    public async Task Editing_A_Character_Changes_The_Name_Wherever_It_Is_Read() {
        var (mara, _) = CastOfTwo();
        _dialogs.Answers.Enqueue(new CharacterDraft("Mara of the Fen", "still the witch"));

        _state.Load();
        _state.SelectNode(_state.Nodes.First(node => node.Id == mara));

        await _state.EditCommand.ExecuteAsync(null);

        Assert.Equal("Mara of the Fen", _state.SelectedNode!.Name);
        Assert.Equal(
            "Mara of the Fen",
            _store
                .LoadGraph(_state.CurrentGraph!.Id)
                .Nodes
                .First(character => character.Id == mara)
                .Name);
    }

    [Fact]
    public async Task Editing_A_Tie_Changes_What_It_Says() {
        var (mara, tomas) = CastOfTwo();
        _store.AddRelationship(_state.CurrentGraph!.Id, mara, tomas, "the teacher of");

        _state.Load();
        _state.SelectEdge(_state.Edges.Single());
        _dialogs.Answers.Enqueue("the enemy of");

        await _state.EditCommand.ExecuteAsync(null);

        Assert.Equal("the enemy of", _state.Edges.Single().Label);
        Assert.Equal(
            "the enemy of",
            _store.LoadGraph(_state.CurrentGraph!.Id).Edges.Single().Label);
    }

    [Fact]
    public void Bending_A_Tie_Keeps_The_Curve_Where_The_Hand_Left_It() {
        var (mara, tomas) = CastOfTwo();
        _store.AddRelationship(_state.CurrentGraph!.Id, mara, tomas, "the teacher of");

        _state.Load();
        _state.Edges.Single().Bend = 42;
        _state.PersistBend(_state.Edges.Single());

        Assert.Equal(42, _store.LoadGraph(_state.CurrentGraph!.Id).Edges.Single().Bend);

        // The map remembers the curve and not just the moment: opening the
        // graph again draws the tie bent exactly where the hand let go.
        _state.Load();
        Assert.Equal(42, _state.Edges.Single().Bend);
    }

    [Fact]
    public async Task A_Tie_Started_From_A_Right_Clicked_Node_Already_Knows_Its_First_End() {
        var (mara, tomas) = CastOfTwo();
        _dialogs.Answers.Enqueue(new RelationshipDraft(mara, tomas, "the teacher of"));

        _state.Load();
        await _state.ConnectFromAsync(_state.Nodes.First(node => node.Id == mara));

        Assert.Contains(
            _dialogs.Asked,
            question => question.StartsWith($"relationship:{mara}::"));

        var edge = Assert.Single(_state.Edges);
        Assert.Equal(mara, edge.From.Id);
        Assert.Equal(tomas, edge.To.Id);
    }

    [Fact]
    public async Task Right_Clicking_To_Connect_A_Lonely_Character_Asked_Nobody() {
        var id = StandOpen("Witch And Magical");
        _store.AddWikiCharacter(id, "Mara", "", 10, 10);
        _state.Load();

        await _state.ConnectFromAsync(_state.Nodes.Single());

        Assert.Empty(_dialogs.Asked);
        Assert.Empty(_state.Edges);
    }

    [Fact]
    public void Arranging_The_Graph_Moves_The_Nodes_And_Keeps_Where_They_Went() {
        CastOfTwo();
        _store.AddWikiCharacter(_state.CurrentGraph!.Id, "Ivo", "", 0, 0);

        _state.Load();
        var before = _state.Nodes.ToDictionary(node => node.Id, node => (node.X, node.Y));

        _state.RelayoutCommand.Execute(null);

        Assert.All(_state.Nodes, node => Assert.NotEqual(before[node.Id], (node.X, node.Y)));

        var stored = _store.LoadGraph(_state.CurrentGraph!.Id).Nodes;
        foreach (var node in _state.Nodes) {
            var saved = stored.Single(character => character.Id == node.Id);
            Assert.Equal(node.X, saved.X);
            Assert.Equal(node.Y, saved.Y);
        }
    }

    [Fact]
    public void Arranging_The_Graph_Keeps_Every_Character_Off_The_Paper_S_Edge() {
        CastOfTwo();

        _state.RelayoutCommand.Execute(null);

        // A node that stands against the edge of the paper stands half off
        // it: its circle is cut in half and its name has nowhere to go.
        Assert.All(_state.Nodes, node => {
            Assert.InRange(node.X, GraphMetrics.NodeMargin, GraphState.WorldWidth - GraphMetrics.NodeMargin);
            Assert.InRange(node.Y, GraphMetrics.NodeMargin, GraphState.WorldHeight - GraphMetrics.NodeMargin);
        });
    }

    [Fact]
    public async Task A_Character_Added_Without_A_Place_Lands_Inside_The_Paper() {
        StandOpen("Witch And Magical");

        // A cast big enough that the spiral the new characters stand on
        // would carry them off the paper, were it not kept inside it.
        for (var i = 0; i < 30; i++) {
            _dialogs.Answers.Enqueue(new CharacterDraft($"Character {i}", ""));
            await _state.AddCharacterCommand.ExecuteAsync(null);
        }

        Assert.Equal(30, _state.Nodes.Count);
        Assert.All(_state.Nodes, node => {
            Assert.InRange(node.X, GraphMetrics.NodeMargin, GraphState.WorldWidth - GraphMetrics.NodeMargin);
            Assert.InRange(node.Y, GraphMetrics.NodeMargin, GraphState.WorldHeight - GraphMetrics.NodeMargin);
        });
    }

    [Fact]
    public void Turning_To_Another_Graph_Shows_Its_Own_Cast() {
        var first = StandOpen("First");
        var second = _store.CreateGraph("Second");
        _store.AddWikiCharacter(second, "Nobody", "", 10, 10);
        _state.Load();

        // Turning the picker by hand: the canvas follows it, cast and all.
        _state.CurrentGraph = _state.Graphs.First(graph => graph.Id == second);
        Assert.Equal("Nobody", Assert.Single(_state.Nodes).Name);

        _state.CurrentGraph = _state.Graphs.First(graph => graph.Id == first);
        Assert.Empty(_state.Nodes);
    }

    [Fact]
    public void A_Tie_Cannot_Be_Thought_Up_While_One_Character_Is_All_There_Is() {
        StandOpen("Witch And Magical");
        _store.AddWikiCharacter(_state.CurrentGraph!.Id, "Mara", "", 10, 10);
        _state.Load();

        Assert.False(_state.CanAddRelationship);
        Assert.False(_state.AddRelationshipCommand.CanExecute(null));
    }

    [Fact]
    public void The_Picker_Says_How_Much_Stands_In_Each_Graph() {
        var id = StandOpen("Witch And Magical");
        var mara = _store.AddWikiCharacter(id, "Mara", "", 10, 10);
        var tomas = _store.AddWikiCharacter(id, "Tomas", "", 20, 20);
        _store.AddRelationship(id, mara, tomas, "the teacher of");

        _state.Load();

        var graph = Assert.Single(_state.Graphs);
        Assert.Equal(2, graph.CharacterCount);
        Assert.Equal(1, graph.RelationshipCount);
    }

    [Fact]
    public void Rewriting_A_Ties_Words_In_Place_Keeps_Them_In_The_Store() {
        var (mara, tomas) = CastOfTwo();
        _store.AddRelationship(_state.CurrentGraph!.Id, mara, tomas, "the teacher of");
        _state.Load();

        _state.BeginLabelEdit(_state.Edges.Single());

        Assert.Same(_state.Edges.Single(), _state.EditingLabel);
        Assert.Same(_state.Edges.Single(), _state.SelectedEdge);

        _state.CommitLabelEdit("   the enemy of   ");

        Assert.Null(_state.EditingLabel);
        Assert.Equal("the enemy of", _state.Edges.Single().Label);
        Assert.Equal(
            "the enemy of",
            _store.LoadGraph(_state.CurrentGraph!.Id).Edges.Single().Label);
        Assert.Empty(_dialogs.Asked);
    }

    [Fact]
    public void Throwing_The_Words_Box_Away_Changes_Nothing() {
        var (mara, tomas) = CastOfTwo();
        _store.AddRelationship(_state.CurrentGraph!.Id, mara, tomas, "the teacher of");
        _state.Load();

        _state.BeginLabelEdit(_state.Edges.Single());
        _state.CancelLabelEdit();

        Assert.Null(_state.EditingLabel);
        Assert.Equal("the teacher of", _state.Edges.Single().Label);
        Assert.Equal(
            "the teacher of",
            _store.LoadGraph(_state.CurrentGraph!.Id).Edges.Single().Label);
    }

    [Fact]
    public async Task Dragging_A_Tie_Between_Two_Rims_Arrives_With_Both_Ends_Standing() {
        var (mara, tomas) = CastOfTwo();
        _state.Load();

        await _state.ConnectNodesAsync(
            _state.Nodes.First(node => node.Id == mara),
            _state.Nodes.First(node => node.Id == tomas));

        Assert.Contains(
            _dialogs.Asked,
            question => question.StartsWith($"relationship:{mara}:{tomas}:"));
    }

    /// <summary>Opens a graph of no particular cast and answers with its id.</summary>
    private int StandOpen(string name) {
        var id = _store.CreateGraph(name);
        _state.Load();
        return id;
    }

    /// <summary>Two characters standing in the open graph: their row ids.</summary>
    private (int Mara, int Tomas) CastOfTwo() {
        var graph = _state.CurrentGraph?.Id ?? StandOpen("Witch And Magical");
        var mara = _store.AddWikiCharacter(graph, "Mara", "", 10, 10);
        var tomas = _store.AddWikiCharacter(graph, "Tomas", "", 20, 20);
        _state.Load();
        return (mara, tomas);
    }
}
