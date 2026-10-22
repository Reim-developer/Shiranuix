using System.Text;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// Chooses the typeface the paper is drawn in: the choice the page shows,
/// unless that choice cannot carry the document's writing.
/// </summary>
/// <remarks>
/// <para>
/// A page may be set in a face the machine carries in name only — Georgia,
/// for one, holds the length of Latin but not every Vietnamese mark. The
/// screen forgives this: the drawing there asks the machine for a stand-in
/// glyph, letter by letter, and the words still read. Paper does not — the
/// pen has no such grace, and a letter it cannot find becomes an empty
/// square. So before the pen takes its face, the faces are read: the page's
/// own first, and, when it cannot carry every letter the document draws,
/// the first spare face that does.
/// </para>
/// </remarks>
public static class SheetFonts {
    /// <summary>
    /// The codepoints the document draws, each once, leaving out the ones no
    /// face is asked to carry.
    /// </summary>
    /// <remarks>
    /// Control characters are left out of the counting: no face holds marks
    /// for them, and none should be failed for that. A letter standing alone
    /// out of its pair is broken writing — it is dropped rather than asked
    /// of anyone.
    /// </remarks>
    public static IReadOnlySet<int> UsedCodepoints(string text) {
        ArgumentNullException.ThrowIfNull(text);

        var used = new HashSet<int>();

        for (var index = 0; index < text.Length;) {
            int codepoint;
            int width;

            if (char.IsSurrogate(text[index])) {
                if (index + 1 < text.Length && char.IsSurrogatePair(text[index], text[index + 1])) {
                    codepoint = char.ConvertToUtf32(text, index);
                    width = 2;
                } else {
                    ++index;
                    continue;
                }
            } else {
                codepoint = text[index];
                width = 1;
            }

            if (codepoint is >= 0x20 and not 0x7F) {
                used.Add(codepoint);
            }

            index += width;
        }

        return used;
    }

    /// <summary>
    /// The face the paper is drawn in: the writing's own when every letter
    /// fits, otherwise the first face that takes them all.
    /// </summary>
    /// <param name="chosen">The typeface the page is set in, as the writer sees it.</param>
    /// <param name="used">Every codepoint the document draws.</param>
    /// <param name="faceBytes">
    /// The machine's file for a family as the pen would read it, or nothing
    /// where the machine carries no such face.
    /// </param>
    /// <param name="spares">The faces asked in turn, in order.</param>
    /// <returns>
    /// The chosen face when it carries every letter, or when nothing does;
    /// the first spare face that carries them all otherwise.
    /// </returns>
    public static string PickFace(
        string chosen,
        IReadOnlySet<int> used,
        Func<string, byte[]?> faceBytes,
        string[] spares) {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(used);
        ArgumentNullException.ThrowIfNull(faceBytes);
        ArgumentNullException.ThrowIfNull(spares);

        if (Carries(chosen)) {
            return chosen;
        }

        foreach (var spare in spares) {
            if (Carries(spare)) {
                return spare;
            }
        }

        return chosen;

        bool Carries(string family) {
            var font = faceBytes(family);

            return font is not null &&
                (used.Count == 0 || FontCoverage.CoversAll(font, used));
        }
    }
}
