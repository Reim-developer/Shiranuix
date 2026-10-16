using Shiranuix.Plugins;
using Shiranuix.Services.Dialogs;
using Shiranuix.ViewModels;

namespace Shiranuix.PluginHost;

/// <summary>
/// The editor, as a plugin is allowed to touch it: the document, and the moves
/// that go through the editor's own history so the writer can take them back.
/// </summary>
/// <remarks>
/// Everything here ends at the view model. Nothing reaches into a control or a
/// file system, and nothing that refuses says why in anything but a sentence —
/// which is what lets the whole thing be written without a window, and tested
/// without one.
/// </remarks>
internal sealed partial class EditorHostCapability : IEditorHost {
    private readonly EditorViewModel _editor;
    private readonly IFileDialogService _dialogs;

    /// <summary>
    /// Creates the editor door over the open document.
    /// </summary>
    /// <param name="editor">The window's coordinator.</param>
    /// <param name="dialogs">Asked where to save a document that has no file yet.</param>
    public EditorHostCapability(EditorViewModel editor, IFileDialogService dialogs) {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        // The document is the editor's, so the editor is where the news of it
        // comes from: a plugin hears about a change it did not make.
        _editor.Document.PropertyChanged += OnDocumentChanged;
        _editor.PropertyChanged += OnEditorChanged;
    }

    /// <inheritdoc />
    public EditorSnapshot Snapshot {
        get {
            var selection = new TextRange(
                _editor.SelectionStart,
                Math.Max(0, _editor.SelectionEnd - _editor.SelectionStart));

            return new EditorSnapshot(
                _editor.Text,
                _editor.Document.Path,
                new TextPoint(_editor.CaretIndex, _editor.CaretLine, _editor.CaretColumn),
                selection,
                _editor.Document.IsDirty);
        }
    }

    /// <inheritdoc />
    public PluginOutcome Replace(TextRange range, string text) {
        ArgumentNullException.ThrowIfNull(text);

        var current = _editor.Text;
        if (!range.FitsIn(current.Length)) {
            return PluginOutcome.Failure(
                $"That range runs past the end of a document {current.Length} characters long.");
        }

        if (range.IsEmpty && text.Length == 0) {
            return PluginOutcome.Ok();
        }

        // The one door into the text: set through the view model, so the move
        // is diffed, recorded and undoable exactly as if the writer typed it.
        _editor.Text = string.Concat(
            current.AsSpan(0, range.Start), text, current.AsSpan(range.End));

        CaretAfter(range.Start + text.Length, range.Start, range.Start + text.Length);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Insert(string text) {
        ArgumentNullException.ThrowIfNull(text);

        var at = Math.Clamp(_editor.CaretIndex, 0, _editor.Text.Length);

        return Replace(new TextRange(at, 0), text);
    }

    /// <inheritdoc />
    public PluginOutcome Select(TextRange range) {
        if (!range.FitsIn(_editor.Text.Length)) {
            return PluginOutcome.Failure(
                "That range is not in this document, so there is nothing to select.");
        }

        CaretAfter(range.End, range.Start, range.End);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome SetStatus(string message) {
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        _editor.StatusMessage = message;
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Undo() => History(_editor.CanUndo, _editor.Undo, "There is nothing to undo.");

    /// <inheritdoc />
    public PluginOutcome Redo() => History(_editor.CanRedo, _editor.Redo, "There is nothing to redo.");

    private static PluginOutcome History(bool can, Action move, string nothingThere) {
        if (!can) {
            return PluginOutcome.Failure(nothingThere);
        }

        move();
        return PluginOutcome.Ok();
    }

    /// <summary>
    /// Puts the caret and the selection where a move left them.
    /// </summary>
    /// <param name="caret">Where the caret stands afterwards.</param>
    /// <param name="start">What is selected, from here.</param>
    /// <param name="end">To here.</param>
    private void CaretAfter(int caret, int start, int end) {
        _editor.CaretIndex = caret;
        _editor.SelectionStart = start;
        _editor.SelectionEnd = end;
    }
}
