using Avalonia.Controls;

namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// Shows the relationship map's questions as windows over the map's own
/// window, wherever the writer happens to be standing.
/// </summary>
/// <remarks>
/// <para>
/// The same walk as the other window dialogs — each dialog made new every
/// ask, because a window answered with something is spent — with one rule
/// that is this service's own: every question stands over the graph's
/// window rather than over the editor's. A dialog owned by the writing
/// behind it wakes that writing when it opens and hands it the focus back
/// when it closes, which is how a writer ends up typing in the editor
/// having asked the map a question; owned by the map, the map is where
/// they were and where they stay.
/// </para>
/// <para>
/// The one yes/no question goes through the application's own prompt with
/// the same window handed to it, so deleting a graph looks like every other
/// warning the writer has ever heeded — only standing in the right place.
/// </para>
/// </remarks>
public sealed class WindowGraphDialogService : IGraphDialogService {
    private readonly IGraphWindowService _graphs;
    private readonly IConfirmQuestion _confirm;

    /// <summary>
    /// Creates the service over the window service the map's window is
    /// kept on, and the one prompt it shares with the application.
    /// </summary>
    /// <param name="graphs">Where the map's standing window is asked for.</param>
    /// <param name="confirm">Shows the one yes/no question.</param>
    public WindowGraphDialogService(IGraphWindowService graphs, IConfirmQuestion confirm) {
        _graphs = graphs ?? throw new ArgumentNullException(nameof(graphs));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
    }

    /// <summary>
    /// The window a dialog is shown over: the map's own. A question asked
    /// while no map stands is a program asking out of turn rather than a
    /// writer's doing, and it says so.
    /// </summary>
    private Window Owner => _graphs.Current ?? throw new InvalidOperationException(
        "The relationship map's window is not standing, so its question has no window to be asked over.");

    /// <inheritdoc />
    public async Task<string?> AskGraphName(string? initial, string? error) {
        var dialog = new Views.TextInputDialog(new TextInputDialogViewModel(
            "Character graph", "Name", initial, error));

        return await dialog.ShowDialog<string?>(Owner);
    }

    /// <inheritdoc />
    public async Task<CharacterDraft?> AskCharacter(string? name, string? note, string? error) {
        var dialog = new Views.CharacterDialog(new CharacterDialogViewModel(name, note, error));

        return await dialog.ShowDialog<CharacterDraft?>(Owner);
    }

    /// <inheritdoc />
    public async Task<RelationshipDraft?> AskRelationship(
        IReadOnlyList<GraphNodeInfo> characters,
        int? fromId,
        int? toId,
        string? label,
        string? error) {
        var dialog = new Views.RelationshipDialog(new RelationshipDialogViewModel(
            characters, fromId, toId, label, error));

        return await dialog.ShowDialog<RelationshipDraft?>(Owner);
    }

    /// <inheritdoc />
    public async Task<string?> AskRelationshipLabel(string current, string fromName, string toName) {
        var dialog = new Views.TextInputDialog(new TextInputDialogViewModel(
            "Relationship", $"{fromName} → {toName}", current, null));

        return await dialog.ShowDialog<string?>(Owner);
    }

    /// <inheritdoc />
    public Task<bool> AskConfirm(string message) =>
        _confirm.Ask(Owner, "Delete", message, "Delete");
}
