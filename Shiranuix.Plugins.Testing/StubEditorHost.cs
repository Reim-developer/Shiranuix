using Shiranuix.Plugins;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Text;

namespace Shiranuix.Plugins.Testing;

/// <summary>
/// An editor door that keeps a document in memory and writes down what was
/// asked of it, so a plugin can be tested without a window, a file or a
/// database.
/// </summary>
/// <remarks>
/// This is the same shape the editor hands a plugin, doing the plainest thing
/// that is still true: text goes where it was told to go, a refusal is a
/// refusal, and anything a test might want to assert on afterwards is written
/// down rather than guessed at.
/// </remarks>
public sealed class StubEditorHost : IEditorHost {
    /// <summary>The document's text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Where the document came from, or null.</summary>
    public string? Path { get; set; }

    /// <summary>Where the caret stands.</summary>
    public int Caret { get; set; }

    /// <summary>Whether the document has unwritten changes.</summary>
    public bool IsDirty { get; set; }

    /// <summary>What is selected, empty when nothing is.</summary>
    public TextRange Selection { get; set; } = TextRange.Empty;

    /// <summary>Every refusal this door handed out, in order.</summary>
    public List<PluginOutcome> Refusals { get; } = [];

    /// <summary>Every sentence a plugin put in the status line, in order.</summary>
    public List<string> Statuses { get; } = [];

    /// <summary>Every path a plugin asked to open, in order.</summary>
    public List<string> OpenedPaths { get; } = [];

    /// <summary>
    /// The files this door is willing to open, keyed by path. Seed it and the
    /// disk is never involved.
    /// </summary>
    public Dictionary<string, string> OpenableFiles { get; } = [];

    /// <summary>Where a save goes when the document has no file of its own.</summary>
    public string SaveAs { get; set; } = "untitled.txt";

    /// <inheritdoc />
    public EditorSnapshot Snapshot => new(
        Text,
        Path,
        new TextPoint(Caret, 1, Caret + 1),
        Selection,
        IsDirty);

    /// <inheritdoc />
    public event Action<PluginDocumentEventArgs>? DocumentChanged;

    /// <inheritdoc />
    public PluginOutcome Replace(TextRange range, string text) {
        if (range.Start > Text.Length || range.End > Text.Length) {
            return Refuse(PluginOutcome.Failure("That range is not in this document."));
        }

        Text = range.IsEmpty
            ? Text.Insert(range.Start, text)
            : Text.Remove(range.Start, range.Length).Insert(range.Start, text);

        Caret = range.Start + text.Length;
        Selection = new TextRange(range.Start, text.Length);
        IsDirty = true;

        Announce(PluginDocumentEvent.Changed);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Insert(string text) => Replace(new TextRange(Caret, 0), text);

    /// <inheritdoc />
    public PluginOutcome Select(TextRange range) {
        Selection = range;
        Caret = range.End;

        Announce(PluginDocumentEvent.Changed);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome SetStatus(string message) {
        Statuses.Add(message ?? string.Empty);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Undo() => Refuse(PluginOutcome.Failure("There is nothing to undo."));

    /// <inheritdoc />
    public PluginOutcome Redo() => Refuse(PluginOutcome.Failure("There is nothing to redo."));

    /// <inheritdoc />
    public Task<PluginOutcome> SaveAsync(CancellationToken cancellationToken) {
        Path ??= SaveAs;
        IsDirty = false;
        Announce(PluginDocumentEvent.Saved);

        return Task.FromResult(PluginOutcome.Ok());
    }

    /// <inheritdoc />
    public Task<PluginOutcome> OpenFileAsync(string path, CancellationToken cancellationToken) {
        OpenedPaths.Add(path);

        if (!OpenableFiles.TryGetValue(path, out var contents)) {
            return Task.FromResult(PluginOutcome.Failure($"There is no file at '{path}'."));
        }

        Text = contents;
        Path = path;
        Caret = 0;
        Selection = TextRange.Empty;
        IsDirty = false;
        Announce(PluginDocumentEvent.Opened);

        return Task.FromResult(PluginOutcome.Ok());
    }

    /// <summary>
    /// Announces something that happened, the way the editor would.
    /// </summary>
    /// <param name="kind">What happened.</param>
    public void Announce(PluginDocumentEvent kind) =>
        DocumentChanged?.Invoke(new PluginDocumentEventArgs(kind, Snapshot));

    private PluginOutcome Refuse(PluginOutcome outcome) {
        Refusals.Add(outcome);
        return outcome;
    }
}
