using System.ComponentModel;
using Shiranuix.Plugins;
using Shiranuix.ViewModels;

namespace Shiranuix.PluginHost;

/// <summary>
/// The news a plugin hears about the document, mapped from the editor's own
/// change notifications.
/// </summary>
/// <remarks>
/// <para>
/// The editor announces properties; a plugin would rather hear sentences. This
/// is the translation: the text moved, the caret moved, a document arrived or
/// went away. A save is not a property change at all — it happens when a plugin
/// asks for one — so it is announced from <see cref="SaveAsync"/> instead.
/// </para>
/// <para>
/// Every notification carries a snapshot taken at that moment, so a plugin
/// working in the background is looking at text it was handed rather than at a
/// document that keeps moving underneath it.
/// </para>
/// </remarks>
internal sealed partial class EditorHostCapability {
    /// <inheritdoc />
    public event Action<PluginDocumentEventArgs>? DocumentChanged;

    /// <summary>
    /// Announces that the document was written to its file.
    /// </summary>
    /// <param name="detail">What the write said, when it said anything.</param>
    /// <returns>The outcome of the write.</returns>
    internal PluginOutcome AnnounceSave(string? detail) {
        DocumentChanged?.Invoke(new PluginDocumentEventArgs(PluginDocumentEvent.Saved, Snapshot));
        return detail is null
            ? PluginOutcome.Ok()
            : PluginOutcome.Failure(detail);
    }

    /// <inheritdoc />
    public async Task<PluginOutcome> SaveAsync(CancellationToken cancellationToken) {
        var document = _editor.Document;

        var path = document.Path;
        if (path is null) {
            cancellationToken.ThrowIfCancellationRequested();
            path = await _dialogs.PickSaveFileAsync(_editor.Name);

            if (path is null) {
                return PluginOutcome.Failure("The writer did not say where to save it.");
            }
        }

        var outcome = document.Save(path);
        return AnnounceSave(outcome.Succeeded ? outcome.Detail : $"Could not save: {outcome.Detail}");
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e) {
        var kind = e.PropertyName switch {
            nameof(DocumentSession.Text) => PluginDocumentEvent.Changed,
            nameof(DocumentSession.Path) => _editor.Document.Path is null
                ? PluginDocumentEvent.Closed
                : PluginDocumentEvent.Opened,
            _ => (PluginDocumentEvent?)null
        };

        if (kind is { } news) {
            DocumentChanged?.Invoke(new PluginDocumentEventArgs(news, Snapshot));
        }
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e) {
        // The caret and the selection are the editor's own, but they are the
        // document's news too: a plugin that follows the writer wants to know
        // where they are, not only what they wrote.
        if (e.PropertyName is nameof(EditorViewModel.CaretIndex)
            or nameof(EditorViewModel.SelectionStart)
            or nameof(EditorViewModel.SelectionEnd)) {
            DocumentChanged?.Invoke(
                new PluginDocumentEventArgs(PluginDocumentEvent.Changed, Snapshot));
        }
    }
}
