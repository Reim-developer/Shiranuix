using Shiranuix.Plugins.Text;
namespace Shiranuix.Plugins.Editor;

/// <summary>
/// The document as it stands right now, read whole.
/// </summary>
/// <remarks>
/// A snapshot, so a plugin working in the background is looking at text it was
/// handed rather than at a document that keeps moving underneath it. It is
/// handed over after every <see cref="IEditorHost.DocumentChanged"/>, and asking
/// for it again is the way to see what the document says now.
/// </remarks>
/// <param name="Text">The whole text, as it stands.</param>
/// <param name="Path">
/// Where the document came from or is going to, or null while it has never
/// been saved anywhere.
/// </param>
/// <param name="Caret">Where the caret stands.</param>
/// <param name="Selection">What is selected, empty when nothing is.</param>
/// <param name="IsDirty">Whether the document has changes not yet written.</param>
public readonly record struct EditorSnapshot(
    string Text,
    string? Path,
    TextPoint Caret,
    TextRange Selection,
    bool IsDirty);
