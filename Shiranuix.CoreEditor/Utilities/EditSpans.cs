namespace Shiranuix.CoreEditor;

/// <summary>
/// The ledger's moves in the shape the document reads them: one span per
/// entry, in the order the entries fall.
/// </summary>
/// <remarks>
/// The map from an entry to a span had been written once per projection —
/// the whole list, the moves outside the window, the moves before a
/// position — and the copies were the same loop. This is that loop, once.
/// </remarks>
public static class EditSpans {
    /// <summary>
    /// Maps entries to spans, in order.
    /// </summary>
    /// <param name="entries">The moves to map.</param>
    /// <returns>One span per entry, in the order they fall.</returns>
    public static IReadOnlyList<EditSpan> Of(IReadOnlyList<EditLedgerEntry> entries) {
        var spans = new EditSpan[entries.Count];

        for (var index = 0; index < entries.Count; index++) {
            var entry = entries[index];
            spans[index] = new EditSpan(entry.Start, entry.Original, entry.Text);
        }

        return spans;
    }
}
