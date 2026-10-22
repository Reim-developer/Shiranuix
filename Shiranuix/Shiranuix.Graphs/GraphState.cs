using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Graphs.Persistence;

namespace Shiranuix.Graphs;

/// <summary>
/// The relationship window's own state: the graphs the writer keeps, the
/// one standing open, the characters and ties it holds, and what is
/// selected of them.
/// </summary>
/// <remarks>
/// <para>
/// The state is the whole window's logic and the canvas's too: every
/// command writes through the store first and the collections here second,
/// so what the canvas draws and what the database holds cannot drift — and
/// the canvas hears one event when everything it draws has changed,
/// rather than being told about each node and tie one at a time.
/// </para>
/// <para>
/// The dialogs are asked through the service, and a refusal is asked
/// again — with the reason standing on the dialog — rather than thrown
/// away. The only thing that ends the asking is the writer cancelling.
/// </para>
/// </remarks>
public sealed partial class GraphState : ObservableObject {
    /// <summary>
    /// The world the layout arranges in, in the canvas's own units. The
    /// canvas shows it at whatever the writer has zoomed to — this is where
    /// the graph lives, not where the window happens to be.
    /// </summary>
    public const double WorldWidth = 1200;

    /// <summary>The world's other measure; the same reasoning as above.</summary>
    public const double WorldHeight = 800;

    private readonly IGraphStore _store;
    private readonly IGraphDialogService _dialogs;
    private bool _reloading;
    private List<GraphNodeViewModel> _nodes = [];
    private List<GraphEdgeViewModel> _edges = [];

    /// <summary>
    /// The characters standing in the open graph. Read-only: the canvas
    /// hears the one <see cref="Changed"/> when the cast has moved whole,
    /// and nothing anywhere puts a character in a graph except through the
    /// store — so there is no kinder middle ground than a plain list
    /// nobody outside can poke at.
    /// </summary>
    public IReadOnlyList<GraphNodeViewModel> Nodes => _nodes;

    /// <summary>The ties drawn between them, on the same rule as above.</summary>
    public IReadOnlyList<GraphEdgeViewModel> Edges => _edges;

    /// <summary>Every graph, by name, as the picker offers them.</summary>
    [ObservableProperty]
    private IReadOnlyList<GraphSummary> _graphs = [];

    /// <summary>The graph standing open, or nothing while there are none.</summary>
    [ObservableProperty]
    private GraphSummary? _currentGraph;

    /// <summary>The node under the writer's eye, or nothing.</summary>
    [ObservableProperty]
    private GraphNodeViewModel? _selectedNode;

    /// <summary>The tie under the writer's eye, or nothing.</summary>
    [ObservableProperty]
    private GraphEdgeViewModel? _selectedEdge;

    /// <summary>
    /// The tie whose words are being rewritten where they stand — the box
    /// the window opens over the pill itself, and this is which pill:
    /// nothing until a hand points at the words, and nothing the moment
    /// they are answered or the pointing is withdrawn.
    /// </summary>
    [ObservableProperty]
    private GraphEdgeViewModel? _editingLabel;

    /// <summary>
    /// The canvas hears this: everything it draws has changed wholesale.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The canvas hears this too: the graph has been arranged afresh, and
    /// the paper may want to bring the whole of it into the glass — a
    /// graph that stands half off the paper is a graph the writer has to
    /// go looking for.
    /// </summary>
    public event Action? Arranged;

    /// <summary>Whether a graph is open for anything to be added to.</summary>
    public bool HasCurrentGraph => CurrentGraph is not null;

    /// <summary>Whether a tie could be drawn — a graph open and two to join.</summary>
    public bool CanAddRelationship => HasCurrentGraph && Nodes.Count >= 2;

    /// <summary>Whether something stands selected to be edited or struck out.</summary>
    public bool HasSelection => SelectedNode is not null || SelectedEdge is not null;

    /// <summary>
    /// Creates the state over the store it writes through and the dialogs
    /// it asks.
    /// </summary>
    /// <param name="store">Where every graph is kept.</param>
    /// <param name="dialogs">Where every question is asked.</param>
    public GraphState(IGraphStore store, IGraphDialogService dialogs) {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    /// <summary>
    /// Opens the first graph there is, or stands the window on nothing at
    /// all while there are none — the canvas has words for that too.
    /// </summary>
    public void Load() => ReloadFirst();

    /// <summary>
    /// Takes the canvas's own pushes back: a node or tie clicked, the
    /// selection swept away, and a node the writer's hand has let go of.
    /// </summary>
    /// <param name="node">The node clicked, or nothing.</param>
    public void SelectNode(GraphNodeViewModel? node) {
        SelectedEdge = null;
        SelectedNode = node;
    }

    /// <summary>Selects a tie, letting any node selection go.</summary>
    /// <param name="edge">The tie clicked, or nothing.</param>
    public void SelectEdge(GraphEdgeViewModel? edge) {
        SelectedNode = null;
        SelectedEdge = edge;
    }

    /// <summary>Sweeps the selection away — a press on empty paper.</summary>
    public void ClearSelection() {
        SelectedNode = null;
        SelectedEdge = null;
    }

    /// <summary>
    /// Opens a tie's words for rewriting where they stand: the hand
    /// pointed at the pill itself, so the tie comes selected under the eye
    /// and this says which words the window's box is about to stand over —
    /// no dialog crosses the paper for a word's worth of change.
    /// </summary>
    /// <param name="edge">The tie whose words were pointed at.</param>
    public void BeginLabelEdit(GraphEdgeViewModel edge) {
        ArgumentNullException.ThrowIfNull(edge);
        SelectEdge(edge);
        EditingLabel = edge;
    }

    /// <summary>
    /// Takes back the words the writer left in the box: the store hears
    /// them the moment they are accepted, so the pill says them the next
    /// time the map is opened as well as this one, and the box closes.
    /// </summary>
    /// <param name="words">What the box holds now, or nothing for emptiness itself.</param>
    public void CommitLabelEdit(string? words) {
        var edge = EditingLabel;
        EditingLabel = null;
        if (edge is null) {
            return;
        }

        var trimmed = (words ?? string.Empty).Trim();
        if (trimmed == edge.Label) {
            return;
        }

        _store.UpdateRelationship(edge.Id, trimmed);
        edge.Label = trimmed;
        Changed?.Invoke();
    }

    /// <summary>Closes the box without letting a keystroke of it reach the tie.</summary>
    public void CancelLabelEdit() => EditingLabel = null;

    /// <summary>
    /// Keeps the place the writer's own hand left a node at: the store
    /// hears it immediately, so a crash or a close never loses a drag.
    /// </summary>
    /// <param name="node">The node that was let go of.</param>
    public void PersistPosition(GraphNodeViewModel node) {
        ArgumentNullException.ThrowIfNull(node);
        _store.MoveCharacter(node.Id, node.X, node.Y);
    }

    /// <summary>
    /// Keeps the curve the writer's own hand bent a tie into — written the
    /// moment the hand lets go of the dot, like a node's place, so the
    /// curve the map remembers is the curve the map was last seen with.
    /// </summary>
    /// <param name="edge">The tie that was bent.</param>
    public void PersistBend(GraphEdgeViewModel edge) {
        ArgumentNullException.ThrowIfNull(edge);
        _store.BendRelationship(edge.Id, edge.Bend);
    }

    [RelayCommand]
    private async Task NewGraphAsync() {
        string? error = null;

        while (true) {
            var name = await _dialogs.AskGraphName(null, error);
            if (name is null) {
                return;
            }

            var trimmed = name.Trim();
            if (trimmed.Length == 0) {
                error = "A graph needs a name.";
                continue;
            }

            try {
                Reload(_store.CreateGraph(trimmed));
                return;
            } catch (InvalidOperationException) {
                // Another graph took the name between the list and the
                // write — rare, and still the writer's to resolve.
                error = "A graph under that name already stands.";
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasCurrentGraph))]
    private async Task RenameGraphAsync() {
        if (CurrentGraph is not { } graph) {
            return;
        }

        string? error = null;

        while (true) {
            var name = await _dialogs.AskGraphName(graph.Name, error);
            if (name is null) {
                return;
            }

            var trimmed = name.Trim();
            if (trimmed.Length == 0) {
                error = "A graph needs a name.";
                continue;
            }

            try {
                _store.RenameGraph(graph.Id, trimmed);
                Reload(graph.Id);
                return;
            } catch (InvalidOperationException) {
                error = "A graph under that name already stands.";
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasCurrentGraph))]
    private async Task DeleteGraphAsync() {
        if (CurrentGraph is not { } graph) {
            return;
        }

        var struck = await _dialogs.AskConfirm(
            $"Delete the graph \"{graph.Name}\" — every character and every tie in it?");
        if (!struck) {
            return;
        }

        _store.DeleteGraph(graph.Id);
        ReloadFirst();
    }

    [RelayCommand]
    private Task AddCharacterAsync() {
        if (CurrentGraph is not { } graph) {
            return Task.CompletedTask;
        }

        return AddCharacterAsync(graph.Id, null, null);
    }

    [RelayCommand(CanExecute = nameof(CanAddRelationship))]
    private Task AddRelationshipAsync() => AskRelationshipAsync(SelectedNode?.Id);

    /// <summary>
    /// Draws a tie starting from a node the canvas was right-clicked at:
    /// one end stands already, and only the other has to be picked.
    /// </summary>
    /// <param name="node">The node the tie starts from.</param>
    public Task ConnectFromAsync(GraphNodeViewModel node) {
        ArgumentNullException.ThrowIfNull(node);

        return Nodes.Count >= 2 ? AskRelationshipAsync(node.Id) : Task.CompletedTask;
    }

    /// <summary>
    /// Draws a tie between two nodes a hand has joined by dragging out of
    /// one rim and letting go on the other: the gesture itself named both
    /// ends, so the dialog opens with them standing answered and asks only
    /// what the tie should say.
    /// </summary>
    /// <param name="from">The node the tie is dragged out of.</param>
    /// <param name="to">The node the tie is let go of on.</param>
    public Task ConnectNodesAsync(GraphNodeViewModel from, GraphNodeViewModel to) {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        return Nodes.Count >= 2 ? AskRelationshipAsync(from.Id, to.Id) : Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAsync() {
        if (SelectedNode is { } node) {
            await EditCharacterAsync(node);
        } else if (SelectedEdge is { } edge) {
            await EditRelationshipAsync(edge);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectionAsync() {
        if (SelectedNode is { } node) {
            var struck = await _dialogs.AskConfirm(
                $"Delete {node.Name}, and every tie standing on them?");
            if (!struck) {
                return;
            }

            _store.DeleteCharacter(node.Id);
        } else if (SelectedEdge is { } edge) {
            _store.DeleteRelationship(edge.Id);
        } else {
            return;
        }

        Reload(CurrentGraph?.Id);
    }

    [RelayCommand(CanExecute = nameof(HasCurrentGraph))]
    private void Relayout() {
        if (CurrentGraph is not { } graph) {
            return;
        }

        // The whole arrangement, asked and written in one breath: the
        // layout knows the graph as it stands, and the store hears one
        // write rather than a move per node. The margin keeps every node
        // clear of the paper's edge, so that no circle and no name stands
        // half off it.
        var placed = GraphLayout.Arrange(
            [.. Nodes.Select(node => node.Id)],
            [.. Edges.Select(edge => (edge.From.Id, edge.To.Id))],
            WorldWidth,
            WorldHeight,
            GraphMetrics.NodeMargin);

        var byId = Nodes.ToDictionary(node => node.Id);
        foreach (var place in placed) {
            if (byId.TryGetValue(place.Node, out var node)) {
                node.X = place.X;
                node.Y = place.Y;
            }
        }

        _store.PlaceAll(graph.Id, [.. placed.Select(place => (place.Node, place.X, place.Y))]);
        Changed?.Invoke();

        // The paper hears of the new arrangement too, and brings it whole
        // into the glass.
        Arranged?.Invoke();
    }

    private async Task AddCharacterAsync(int graphId, double? x, double? y) {
        string? error = null;

        while (true) {
            var draft = await _dialogs.AskCharacter(null, null, error);
            if (draft is null) {
                return;
            }

            var trimmed = draft.Name.Trim();
            if (trimmed.Length == 0) {
                error = "A character needs a name.";
                continue;
            }

            var spot = PlaceNear(x, y);
            var id = _store.AddCharacter(graphId, trimmed, draft.Note, spot.X, spot.Y);
            Reload(graphId, selectNode: id);
            return;
        }
    }

    private async Task EditCharacterAsync(GraphNodeViewModel node) {
        string? error = null;

        while (true) {
            var draft = await _dialogs.AskCharacter(node.Name, node.Note, error);
            if (draft is null) {
                return;
            }

            var trimmed = draft.Name.Trim();
            if (trimmed.Length == 0) {
                error = "A character needs a name.";
                continue;
            }

            _store.UpdateCharacter(node.Id, trimmed, draft.Note);
            node.Name = trimmed;
            node.Note = draft.Note;
            Changed?.Invoke();
            return;
        }
    }

    private async Task EditRelationshipAsync(GraphEdgeViewModel edge) {
        var label = await _dialogs.AskRelationshipLabel(edge.Label, edge.From.Name, edge.To.Name);
        if (label is null) {
            return;
        }

        _store.UpdateRelationship(edge.Id, label.Trim());
        edge.Label = label.Trim();
        Changed?.Invoke();
    }

    /// <summary>
    /// The tie's own asking, whether it began at the toolbar with a node
    /// under the writer's eye, at a node right-clicked, or in a hand's drag
    /// between two rims: the dialog knows each end there is to know, and
    /// the store hears the tie when both ends stand answered.
    /// </summary>
    /// <param name="fromId">The character the tie starts at, or nothing for the dialog to ask both.</param>
    /// <param name="toId">The character it arrives at when the gesture already named one, or nothing likewise.</param>
    private async Task AskRelationshipAsync(int? fromId, int? toId = null) {
        if (CurrentGraph is not { } graph) {
            return;
        }

        string? error = null;

        while (true) {
            var draft = await _dialogs.AskRelationship(Infos(), fromId, toId, null, error);
            if (draft is null) {
                return;
            }

            if (draft.FromId == draft.ToId) {
                error = "A tie must join two different characters.";
                continue;
            }

            try {
                var id = _store.AddRelationship(
                    graph.Id, draft.FromId, draft.ToId, draft.Label.Trim());
                Reload(graph.Id, selectEdge: id);
                return;
            } catch (InvalidOperationException flaw) {
                error = flaw.Message;
            }
        }
    }

    /// <summary>
    /// Where a new character stands: exactly where the paper was pointed at
    /// when it was asked for, and a place on a widening spiral out of the
    /// middle when it was not — near enough the graph to read, far enough
    /// from the last arrival not to stand on them, and always inside the
    /// paper, so that a character added is a character the writer can see.
    /// </summary>
    private (double X, double Y) PlaceNear(double? x, double? y) {
        if (x is { } across && y is { } down) {
            return (across, down);
        }

        var angle = Nodes.Count * 2.39996322972865332; // the golden angle: a spiral that never crowds
        var radius = 70 + 26 * Nodes.Count;

        return (ClampToPaper(WorldWidth / 2 + Math.Cos(angle) * radius, WorldWidth),
            ClampToPaper(WorldHeight / 2 + Math.Sin(angle) * radius, WorldHeight));
    }

    /// <summary>
    /// Keeps a place inside the paper, with the room a node needs for its
    /// circle and the name under it.
    /// </summary>
    /// <param name="place">The place to keep inside.</param>
    /// <param name="extent">How far the paper reaches on that side.</param>
    /// <returns>The place, or the nearest one inside the paper's margin.</returns>
    private static double ClampToPaper(double place, double extent) =>
        Math.Clamp(place, GraphMetrics.NodeMargin, extent - GraphMetrics.NodeMargin);

    private IReadOnlyList<GraphNodeInfo> Infos() =>
        [.. Nodes.Select(node => new GraphNodeInfo(node.Id, node.Name))];

    /// <summary>Stands the window on the graph that comes first now.</summary>
    private void ReloadFirst() =>
        Reload(_store.ListGraphs().FirstOrDefault()?.Id);

    /// <summary>
    /// Stands the window on a graph whole: the picker moved, the canvas
    /// swept and filled, the selection let go — one reload rather than a
    /// thousand small edits, because every write here is a whole graph's
    /// worth of truth.
    /// </summary>
    private void Reload(int? graphId, int? selectNode = null, int? selectEdge = null) {
        _reloading = true;
        var graphs = _store.ListGraphs();
        Graphs = graphs;
        CurrentGraph = graphId is null ? null : graphs.FirstOrDefault(g => g.Id == graphId);
        _reloading = false;

        OpenGraph();

        if (selectNode is not null) {
            SelectedNode = Nodes.FirstOrDefault(node => node.Id == selectNode);
        }

        if (selectEdge is not null) {
            SelectedEdge = Edges.FirstOrDefault(edge => edge.Id == selectEdge);
        }
    }

    /// <summary>Fills the canvas with whatever stands in the open graph.</summary>
    private void OpenGraph() {
        // The new cast is built beside the old one and swapped in whole, so
        // the canvas never sees a half-graph: two lists and one pointer.
        var nodes = new List<GraphNodeViewModel>();
        var edges = new List<GraphEdgeViewModel>();
        SelectedNode = null;
        SelectedEdge = null;
        EditingLabel = null;

        if (CurrentGraph is { } graph) {
            var snapshot = _store.LoadGraph(graph.Id);

            foreach (var row in snapshot.Nodes) {
                nodes.Add(new GraphNodeViewModel(
                    row.Id, row.Name, row.Note, row.X, row.Y));
            }

            var byId = nodes.ToDictionary(node => node.Id);
            foreach (var tie in snapshot.Edges) {
                if (byId.TryGetValue(tie.FromId, out var from) &&
                    byId.TryGetValue(tie.ToId, out var to)) {
                    edges.Add(new GraphEdgeViewModel(tie.Id, from, to, tie.Label, tie.Bend));
                }
            }
        }

        _nodes = nodes;
        _edges = edges;

        Changed?.Invoke();
        RefreshCommands();
    }

    // The picker, the selection and the canvas's contents each move the
    // ground under every command's feet, so each of them tells the commands
    // to ask again whether they would still do anything.
    partial void OnCurrentGraphChanged(GraphSummary? value) {
        if (!_reloading) {
            OpenGraph();
        }

        RefreshCommands();
    }

    partial void OnSelectedNodeChanged(GraphNodeViewModel? value) => RefreshCommands();

    partial void OnSelectedEdgeChanged(GraphEdgeViewModel? value) => RefreshCommands();

    private void RefreshCommands() {
        OnPropertyChanged(nameof(HasCurrentGraph));
        OnPropertyChanged(nameof(CanAddRelationship));
        OnPropertyChanged(nameof(HasSelection));

        RenameGraphCommand.NotifyCanExecuteChanged();
        DeleteGraphCommand.NotifyCanExecuteChanged();
        AddRelationshipCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        RelayoutCommand.NotifyCanExecuteChanged();
    }
}
