using Shiranuix.Plugins;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Text;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An editor door that keeps a document in memory and writes down what was
/// asked of it, so a plugin can be tested without a window or a file.
/// </summary>
/// <remarks>
/// What a plugin is really given by the editor is this interface, and a test
/// that runs a plugin needs it in the same shape the editor hands it over. The
/// behaviour here is deliberately plain: text goes where it was told to go,
/// refusals are refusals, and anything a plugin might want to assert about
/// afterwards is written down.
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

    /// <summary>Every refusal this door handed out, in order.</summary>
    public List<PluginOutcome> Refusals { get; } = [];

    /// <summary>Every sentence a plugin put in the status line, in order.</summary>
    public List<string> Statuses { get; } = [];

    /// <inheritdoc />
    public TextRange Selection { get; private set; } = TextRange.Empty;

    /// <inheritdoc />
    public EditorSnapshot Snapshot => new(
        Text, Path, new TextPoint(Caret, 1, Caret + 1), Selection, IsDirty);

    /// <inheritdoc />
    public event Action<PluginDocumentEventArgs>? DocumentChanged;

    /// <inheritdoc />
    public PluginOutcome Replace(TextRange range, string text) {
        if (string.IsNullOrEmpty(text)) {
            return Refuse(PluginOutcome.Failure("There is no text to put there."));
        }

        if (range == TextRange.Empty) {
            Text = Text.Insert(range.Start, text);
        } else {
            Text = Text.Remove(range.Start, range.Length).Insert(range.Start, text);
        }

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
        Statuses.Add(message);
        return PluginOutcome.Ok();
    }

    /// <inheritdoc />
    public PluginOutcome Undo() => Refuse(PluginOutcome.Failure("There is nothing to undo."));

    /// <inheritdoc />
    public PluginOutcome Redo() => Refuse(PluginOutcome.Failure("There is nothing to redo."));

    /// <inheritdoc />
    public Task<PluginOutcome> SaveAsync(CancellationToken cancellationToken) {
        Path ??= "untitled.txt";
        IsDirty = false;
        Announce(PluginDocumentEvent.Saved);

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
