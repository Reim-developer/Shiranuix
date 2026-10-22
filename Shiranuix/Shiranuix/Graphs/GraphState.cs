using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Persistence;
using Shiranuix.Services.Dialogs;

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

    /// <summary>The characters standing in the open graph.</summary>
    public ObservableCollection<GraphNodeViewModel> Nodes { get; } = [];

    /// <summary>The ties drawn between them.</summary>
    public ObservableCollection<GraphEdgeViewModel> Edges { get; } = [];

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
    /// The canvas hears this: everything it draws has changed wholesale.
    /// </summary>
    public event Action? Changed;

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
    /// Keeps the place the writer's own hand left a node at: the store
    /// hears it immediately, so a crash or a close never loses a drag.
    /// </summary>
    /// <param name="node">The node that was let go of.</param>
    public void PersistPosition(GraphNodeViewModel node) {
        ArgumentNullException.ThrowIfNull(node);
        _store.MoveCharacter(node.Id, node.X, node.Y);
    }

    /// <summary>
    /// Adds a character the canvas was double-clicked at — the writer
    /// pointing at the empty paper where somebody should stand.
    /// </summary>
    /// <param name="x">Where across they should stand, or nothing for a place of the state's own choosing.</param>
    /// <param name="y">Where down they should stand, or nothing likewise.</param>
    public Task AddCharacterAtAsync(double? x = null, double? y = null) {
        if (CurrentGraph is not { } graph) {
            return Task.CompletedTask;
        }

        return AddCharacterAsync(graph.Id, x, y);
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
    private async Task AddRelationshipAsync() {
        if (CurrentGraph is not { } graph) {
            return;
        }

        string? error = null;

        while (true) {
            var draft = await _dialogs.AskRelationship(
                Infos(), SelectedNode?.Id, null, null, error);
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
        // write rather than a move per node.
        var placed = GraphLayout.Arrange(
            [.. Nodes.Select(node => node.Id)],
            [.. Edges.Select(edge => (edge.From.Id, edge.To.Id))],
            WorldWidth,
            WorldHeight);

        var byId = Nodes.ToDictionary(node => node.Id);
        foreach (var place in placed) {
            if (byId.TryGetValue(place.Id, out var node)) {
                node.X = place.X;
                node.Y = place.Y;
            }
        }

        _store.PlaceAll(graph.Id, [.. placed.Select(place => (place.Id, place.X, place.Y))]);
        Changed?.Invoke();
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
    /// Where a new character stands: exactly where the paper was pointed at
    /// when it was asked for, and a place on a widening spiral out of the
    /// middle when it was not — near enough the graph to read, far enough
    /// from the last arrival not to stand on them.
    /// </summary>
    private (double X, double Y) PlaceNear(double? x, double? y) {
        if (x is { } across && y is { } down) {
            return (across, down);
        }

        var angle = Nodes.Count * 2.39996322972865332; // the golden angle: a spiral that never crowds
        var radius = 70 + 26 * Nodes.Count;

        return (WorldWidth / 2 + Math.Cos(angle) * radius,
            WorldHeight / 2 + Math.Sin(angle) * radius);
    }

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
        Nodes.Clear();
        Edges.Clear();
        SelectedNode = null;
        SelectedEdge = null;

        if (CurrentGraph is { } graph) {
            var snapshot = _store.LoadGraph(graph.Id);

            foreach (var character in snapshot.Characters) {
                Nodes.Add(new GraphNodeViewModel(
                    character.Id, character.Name, character.Note, character.X, character.Y));
            }

            var byId = Nodes.ToDictionary(node => node.Id);
            foreach (var relationship in snapshot.Relationships) {
                if (byId.TryGetValue(relationship.FromCharacterId, out var from) &&
                    byId.TryGetValue(relationship.ToCharacterId, out var to)) {
                    Edges.Add(new GraphEdgeViewModel(
                        relationship.Id, from, to, relationship.Label));
                }
            }
        }

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
