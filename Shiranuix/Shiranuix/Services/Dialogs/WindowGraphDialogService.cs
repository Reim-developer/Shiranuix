using Avalonia.Controls;
using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Shows the relationship map's questions as windows over the application's
/// main window.
/// </summary>
/// <remarks>
/// The same walk as the other window dialogs: the window arrives as a
/// <see cref="Func{Window}"/> rather than captured, because the container
/// is still building the window when it builds this, and each dialog is
/// made new every ask — a window answered with something is spent. The
/// one yes/no question goes through the same dialog every other prompt in
/// the application is drawn in, so deleting a graph looks like every other
/// warning the writer has ever heeded.
/// </remarks>
public sealed class WindowGraphDialogService : IGraphDialogService {
    private readonly Func<Window> _window;
    private readonly IDialogService _prompts;

    /// <summary>
    /// Creates the service over a way of asking for the window.
    /// </summary>
    /// <param name="window">Resolves the window that owns the dialogs.</param>
    /// <param name="prompts">Shows the one yes/no question.</param>
    public WindowGraphDialogService(Func<Window> window, IDialogService prompts) {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    /// <inheritdoc />
    public async Task<string?> AskGraphName(string? initial, string? error) {
        var dialog = new Views.TextInputDialog(new TextInputDialogViewModel(
            "Character graph", "Name", initial, error));

        return await dialog.ShowDialog<string?>(_window());
    }

    /// <inheritdoc />
    public async Task<CharacterDraft?> AskCharacter(string? name, string? note, string? error) {
        var dialog = new Views.CharacterDialog(new CharacterDialogViewModel(name, note, error));

        return await dialog.ShowDialog<CharacterDraft?>(_window());
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

        return await dialog.ShowDialog<RelationshipDraft?>(_window());
    }

    /// <inheritdoc />
    public async Task<string?> AskRelationshipLabel(string current, string fromName, string toName) {
        var dialog = new Views.TextInputDialog(new TextInputDialogViewModel(
            "Relationship", $"{fromName} → {toName}", current, null));

        return await dialog.ShowDialog<string?>(_window());
    }

    /// <inheritdoc />
    public async Task<bool> AskConfirm(string message) {
        var answer = await _prompts.ShowAsync(new DialogRequest<bool>(
            "Delete",
            message,
            [
                new DialogButton<bool>("Keep", false, IsDefault: true, IsCancel: true),
                new DialogButton<bool>("Delete", true)
            ]));

        // A dismissed prompt strikes nothing out.
        return answer == true;
    }
}
