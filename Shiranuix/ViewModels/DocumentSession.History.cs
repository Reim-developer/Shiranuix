namespace Shiranuix.ViewModels;

/// <summary>
/// The document's undo and redo as the session holds them: the two doors the
/// view asks, and the trips that put the text back the way a move found it.
/// </summary>
/// <remarks>
/// The session is one class across a few files by concern, and this is the
/// history's file. The stack itself belongs to <see cref="IDocumentHistory"/>;
/// what lives here is the session's side of the conversation — bringing the
/// window back to the move's own ground, putting the restored text through
/// the one door that records nothing, and re-seating the caret where the move
/// began.
/// </remarks>
public sealed partial class DocumentSession {
    /// <summary>
    /// Whether there is a move to take back.
    /// </summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>
    /// Whether a taken-back move is waiting to be reapplied.
    /// </summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Takes the last move back, restoring the text it was made to, with
    /// the caret where the move began.
    /// </summary>
    public void Undo() {
        if (!_history.PeekUndo(out var target)) {
            return;
        }

        // The move's span means nothing against a window that has slid
        // away, so the window it was made in goes back on screen first and
        // the move comes off the text it was actually made against.
        _navigator.ReturnTo(this, target);

        if (!_history.TryUndo(Text, out var restored)) {
            return;
        }

        RestoreText(restored);
        SetCaretWithoutSliding(target.Caret);
    }

    /// <summary>
    /// Reapplies the last move taken back, with the caret after what the
    /// move put back.
    /// </summary>
    public void Redo() {
        if (!_history.PeekRedo(out var target)) {
            return;
        }

        _navigator.ReturnTo(this, target);

        if (!_history.TryRedo(Text, out var restored)) {
            return;
        }

        RestoreText(restored);
        SetCaretWithoutSliding(target.Caret);
    }

    /// <summary>
    /// Puts a text the history produced into the document without recording
    /// the placement as a move of its own.
    /// </summary>
    /// <param name="text">The text to restore.</param>
    private void RestoreText(string text) {
        _restoringText = true;
        try {
            Text = text;
        } finally {
            _restoringText = false;
        }
    }
}
