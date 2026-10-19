namespace Shiranuix.CoreEditor;

/// <summary>
/// Translates where the caret stands between the window's text and the
/// file it is a window onto.
/// </summary>
/// <remarks>
/// <para>
/// The window shows a stretch of file with the writer's moves laid over
/// it, so the two coordinate systems drift apart by exactly what those
/// moves changed: a position in the file and the same place in the window's
/// text are one question asked from either side. Both directions are pure
/// functions over the ledger and a length, so the caret's travel is
/// checkable without a document, a window, or a cursor.
/// </para>
/// <para>
/// One rule decides every case: a caret standing inside a move's own text
/// stands where that move's span of file stands, as many characters in as
/// the text is — and never past the end of the file characters it replaced.
/// </para>
/// </remarks>
public static class WindowCaret {
    /// <summary>
    /// Translates where the caret stands in the file into where the same
    /// place stands in the window's current text.
    /// </summary>
    /// <param name="ledger">The moves standing against the file.</param>
    /// <param name="fileOffset">The caret's position in the file.</param>
    /// <param name="textLength">The window's text length, to clamp into.</param>
    /// <returns>The caret's index in the window's text, clamped into it.</returns>
    public static int ToWindow(EditLedger ledger, int fileOffset, int textLength) {
        ArgumentNullException.ThrowIfNull(ledger);

        var local = fileOffset - ledger.WindowFrom;

        foreach (var span in ledger.Spans()) {
            if (span.Start >= ledger.WindowFrom && span.Start < fileOffset) {
                local += span.Text.Length - span.Original.Length;
            }
        }

        return Math.Clamp(local, 0, textLength);
    }

    /// <summary>
    /// Translates where the caret stands in the window's current text into
    /// where the same place stands in the file — the mirror of
    /// <see cref="ToWindow"/>, for when the caret is the thing a move
    /// starts from rather than the place one lands in: the wheel turns the
    /// window around where the writer was working, not around where the
    /// window happens to be.
    /// </summary>
    /// <param name="ledger">The moves standing against the file.</param>
    /// <param name="localIndex">The caret's index in the window's text.</param>
    /// <param name="textLength">The window's text length, to clamp into.</param>
    /// <returns>The caret's position, in file coordinates.</returns>
    public static int ToFile(EditLedger ledger, int localIndex, int textLength) {
        ArgumentNullException.ThrowIfNull(ledger);

        var local = Math.Clamp(localIndex, 0, textLength);
        var delta = 0;

        foreach (var span in ledger.Spans()) {
            var end = span.Start + span.Original.Length;

            // The window's text begins at its own seam: a move that ended
            // before it does not reach the text at all, while one standing
            // exactly there is the text the window opens with.
            if (end < ledger.WindowFrom || (end == ledger.WindowFrom && span.Start < end)) {
                continue;
            }

            // Entries fall in order, and everything past the window's own
            // stretch has no place in its text.
            if (span.Start > ledger.WindowTo
                    || (span.Start == ledger.WindowTo && span.Start < end)) {
                break;
            }

            var textStart = span.Start - ledger.WindowFrom + delta;

            if (local <= textStart) {
                // The caret stands in the untouched file before the move.
                break;
            }

            delta += span.Text.Length - span.Original.Length;

            if (local <= textStart + span.Text.Length) {
                // Inside the move's own text: it stands where its span of
                // file stands, as many characters in as the text is —
                // and never past the end of the file characters it replaced.
                return span.Start + Math.Min(local - textStart, span.Original.Length);
            }
        }

        return ledger.WindowFrom + local - delta;
    }
}
