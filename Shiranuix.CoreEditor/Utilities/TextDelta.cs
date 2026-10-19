namespace Shiranuix.CoreEditor;

/// <summary>
/// The span two texts disagree over: where they stop agreeing, and what each
/// side has there.
/// </summary>
/// <remarks>
/// One algorithm serves two owners — the undo history and the edit ledger —
/// because both ask the same question of a before and an after, and a second
/// answer to it would be a second chance for them to differ.
/// </remarks>
public static class TextDelta {
    /// <summary>
    /// Works out what one move changed.
    /// </summary>
    /// <param name="before">The text as it stood.</param>
    /// <param name="after">The text as it stands.</param>
    /// <param name="start">Where the disagreement begins.</param>
    /// <param name="removed">What the move took out there.</param>
    /// <param name="inserted">What the move put there instead.</param>
    /// <returns>True when the text moved at all.</returns>
    /// <remarks>
    /// The span runs from the first difference to the last, so a move in the
    /// middle of a line stores that line's change and nothing around it. No
    /// difference means nothing moved — a note about air.
    /// </remarks>
    public static bool TryDiff(
        string before,
        string after,
        out int start,
        out string removed,
        out string inserted) {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        start = 0;
        var shared = Math.Min(before.Length, after.Length);

        while (start < shared && before[start] == after[start]) {
            start++;
        }

        var beforeEnd = before.Length;
        var afterEnd = after.Length;

        while (beforeEnd > start && afterEnd > start
                && before[beforeEnd - 1] == after[afterEnd - 1]) {
            beforeEnd--;
            afterEnd--;
        }

        removed = before.Substring(start, beforeEnd - start);
        inserted = after.Substring(start, afterEnd - start);

        return removed.Length > 0 || inserted.Length > 0;
    }
}
