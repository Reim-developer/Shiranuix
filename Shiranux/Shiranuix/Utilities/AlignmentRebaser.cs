using Shiranuix.CoreFormat;
using Shiranuix.Models;

namespace Shiranuix.Utilities;

/// <summary>
/// Carries each paragraph's alignment from the text a save reads to the
/// text that save writes, so the way a paragraph is set follows the
/// paragraph's own letters wherever they end up.
/// </summary>
/// <remarks>
/// <para>
/// Alignments in a <c>.shiranux</c> file are numbered by paragraph, and a
/// number only means something beside a particular run of letters. Type a
/// line above a centred heading and the heading's letters move down a
/// paragraph — the centring must move with them, or the next paragraph
/// would inherit a face it never had. So each aligned paragraph's opening
/// position is carried through the same moves the writer lays over the
/// file, and re-counted against the writing that comes out.
/// </para>
/// <para>
/// A paragraph whose letters are deleted outright has nothing left to
/// follow, so its alignment is dropped rather than left pointing at
/// whatever slid into its number. Two paragraphs folded into one keep the
/// alignment of the one that came first. Everything else simply moves: the
/// alignment lands on whichever paragraph now opens where the old one did.
/// </para>
/// </remarks>
public static class AlignmentRebaser {
    /// <summary>
    /// Re-anchors a set of alignments from a source text onto the writing
    /// composed from it.
    /// </summary>
    /// <param name="sourceText">The text the alignments were written
    /// against, with lines broken by <c>\n</c>.</param>
    /// <param name="alignments">The alignments, numbered against
    /// <paramref name="sourceText"/>.</param>
    /// <param name="replacements">The moves to compose with, in source
    /// order.</param>
    /// <param name="composed">The writing those moves compose.</param>
    /// <returns>
    /// The alignments, renumbered against <paramref name="composed"/>.
    /// Paragraphs that lost their letters are gone; a number claimed twice
    /// keeps the lower one.
    /// </returns>
    public static IReadOnlyDictionary<int, ParagraphAlignment> Rebase(
        string sourceText,
        IReadOnlyDictionary<int, ParagraphAlignment> alignments,
        IReadOnlyList<TextReplacement> replacements,
        string composed) {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(alignments);
        ArgumentNullException.ThrowIfNull(replacements);
        ArgumentNullException.ThrowIfNull(composed);

        if (alignments.Count == 0 || replacements.Count == 0) {
            // Nothing to move: either no face was set, or no letter moved,
            // and the numbers still say what they said.
            return new Dictionary<int, ParagraphAlignment>(alignments);
        }

        var composedParagraphs = CountLines(composed) + 1;
        var rebased = new Dictionary<int, ParagraphAlignment>();

        // Ascending, so a number claimed by two paragraphs keeps the one
        // that was higher up the page.
        foreach (var pair in alignments.OrderBy(pair => pair.Key)) {
            var start = ParagraphStart(sourceText, pair.Key);

            if (start < 0) {
                // The source never had that paragraph: a stray the format
                // let through, with no letters left to follow.
                continue;
            }

            if (IsDeleted(sourceText, start, replacements)) {
                continue;
            }

            var landed = Math.Clamp(MapPosition(start, replacements), 0, composed.Length);
            var index = CountLines(composed, landed);

            if (index >= composedParagraphs) {
                continue;
            }

            if (!rebased.TryAdd(index, pair.Value)) {
                // Two paragraphs landed on one; the first one here is the
                // one higher up, and it keeps the face.
                continue;
            }
        }

        return rebased;
    }

    /// <summary>
    /// Where a paragraph's first letter stands, or <c>-1</c> when the text
    /// has no such paragraph.
    /// </summary>
    private static int ParagraphStart(string text, int paragraph) {
        var start = 0;

        for (var current = 0; current < paragraph; current++) {
            var line = text.IndexOf('\n', start);

            if (line < 0) {
                return -1;
            }

            start = line + 1;
        }

        return start;
    }

    /// <summary>
    /// Whether a move erased the paragraph whole — start to its own
    /// trailing break, and put nothing in their place.
    /// </summary>
    /// <remarks>
    /// A paragraph gone from the page has no alignment to carry: the face
    /// belonged to letters that are no longer there. The trailing break
    /// counts as part of the paragraph, because a paragraph that ends the
    /// file has no break of its own to protect it — reaching the end of the
    /// text is the same erasure.
    /// </remarks>
    private static bool IsDeleted(
        string text,
        int start,
        IReadOnlyList<TextReplacement> replacements) {
        var line = text.IndexOf('\n', start);
        var end = line < 0 ? text.Length : line + 1;

        foreach (var replacement in replacements) {
            if (replacement.Text.Length > 0) {
                // Something was written where the paragraph stood, and the
                // paragraph keeps whatever that became.
                continue;
            }

            if (replacement.Start <= start && replacement.Start + replacement.Length >= end) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Carries a position in the source through the moves to where those
    /// letters now stand in the composed writing.
    /// </summary>
    /// <remarks>
    /// A move wholly behind the position shifts it by how much longer or
    /// shorter the writing became. A move standing exactly on the position
    /// shifts it too — pressing Enter at the head of a paragraph opens a
    /// new paragraph above it, and the face goes down with the letters
    /// rather than staying behind on a number. A position swallowed by a
    /// move lands at the head of whatever took its place, which is where
    /// the letters it was watching have gone.
    /// </remarks>
    private static int MapPosition(int position, IReadOnlyList<TextReplacement> replacements) {
        var shift = 0;

        foreach (var replacement in replacements) {
            if (replacement.Start + replacement.Length <= position) {
                shift += replacement.Text.Length - replacement.Length;
                continue;
            }

            if (replacement.Start > position) {
                break;
            }

            return replacement.Start + shift;
        }

        return position + shift;
    }

    /// <summary>
    /// How many lines a text breaks into: paragraphs less one, since the
    /// last line carries no break of its own.
    /// </summary>
    private static int CountLines(string text) {
        var count = 0;

        foreach (var character in text) {
            if (Newline.IsLineFeed(character)) {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// How many lines open before a position: the paragraph number that
    /// position falls in.
    /// </summary>
    private static int CountLines(string text, int before) {
        var count = 0;

        for (var index = 0; index < before && index < text.Length; index++) {
            if (Newline.IsLineFeed(text[index])) {
                count++;
            }
        }

        return count;
    }
}
