using Shiranuix.CoreFormat;
namespace Shiranuix.CoreEditor;

/// <summary>
/// The document's side of the file seam: the small moves
/// <see cref="DocumentFile"/> asks for when a file arrives, when a blank
/// takes its place, and when a write lands.
/// </summary>
/// <remarks>
/// Every method here is one step of the story the file side tells, in the
/// order it tells it — the same ordering the session kept when New, Open
/// and Save lived in the main file, only now named one move at a time.
/// </remarks>
public sealed partial class DocumentSession {
    Newline IFileStage.Newline => Newline;

    string IFileStage.Text => Text;

    IReadOnlyList<TextReplacement> IFileStage.PendingChanges() => _ledger.Build();

    /// <summary>
    /// The document as its file would say it now: the whole writing, the
    /// file streamed through with every move since the last write laid over
    /// it — the same composition a save makes, handed over as text.
    /// </summary>
    /// <remarks>
    /// The window is what the screen shows and what the writer edits; a
    /// whole-file ask — an export, say — walks the file the save walks,
    /// without writing any of it. The letters behind the window's edge come
    /// off the file itself.
    /// </remarks>
    public string WholeText() => _file.WholeText((IFileStage)this);

    /// <summary>
    /// The same whole writing, without holding the thread that asked.
    /// </summary>
    /// <returns>The whole writing, however little of it was on screen.</returns>
    public Task<string> WholeTextAsync() => _file.WholeTextAsync((IFileStage)this);

    void IFileStage.AdoptFile(string path, ScanResult scan, string text, bool more) {
        // The file's own text arriving is the same rebegun as a new
        // document — not a move the writer made — so the old moves go, the
        // ledger takes this window as its zero, and the replacement is not
        // recorded as a move against anything.
        _history.Clear();
        _ledger.Clear();
        _ledger.ResetWindow(0, text);

        // Window and counts first, text second: the text's own notification
        // asks for both.
        _scan = scan;
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: more);
        AnnounceWindowEdges();

        _text.Value = text;
        Path = path;
    }

    void IFileStage.AdoptTypography(DocumentTypography? said) {
        // Nothing carried is nothing changed: the document keeps standing on
        // whatever type it was already standing on, and nothing is written
        // back to the store for a choice the file never made.
        if (said is null) {
            return;
        }

        _typography.FontFamily = said.FontFamily;
        _typography.FontSize = said.FontSize;
    }

    void IFileStage.AdoptBlank(ScanResult scan, Newline newline) {
        Path = null;

        // A blank document is a document rebegun: the moves of the old one
        // would restore a text this document has no claim to, and the
        // ledger's would spell a file it has nothing to do with.
        _history.Clear();
        _ledger.Clear();
        _ledger.ResetWindow(0, string.Empty);

        // Back to one window over nothing: no file behind it, no walk to
        // remember, nothing past the end to slide towards. The counts
        // refresh once the window and the scan say the same nothing, so
        // the gutter and the caret's line number the blank document.
        _scan = scan;
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: false);
        _text.Reset(string.Empty);
        AnnounceWindowEdges();

        // A blank document is this machine's blank document — the operating
        // system's own convention — not whatever the last file arrived in.
        Newline = newline;
    }

    void IFileStage.MarkSaved(Newline newline) {
        _text.MarkSaved();

        // The file's own convention is kept, so saving writes the breaks
        // back the way they arrived rather than rewriting them — and it
        // becomes the clean point, like the text beside it.
        Newline = newline;
    }

    void IFileStage.ReseatCaret() => SetCaretWithoutSliding(CaretIndex);

    void IFileStage.ApplySave(string path, ScanResult written) {
        Path = path;

        // The moves have collapsed into the file that was written, so every
        // position after them slid: the window and its anchors, and the
        // windows the history's entries name, all move to where their
        // stretch of file now stands. The window keeps showing exactly what
        // it showed — only where it stands has changed.
        _window.Shift(_ledger);
        _history.Rebase(position => _ledger.ShiftAt(position).Characters);
        _scan = written;

        // What was written now says exactly what the window says, so the
        // moves behind it are the new zero. The history keeps going: a save
        // is a point the text passed through, not a new document.
        _ledger.RebaseToSaved(Text);

        _text.MarkSaved();
    }
}
