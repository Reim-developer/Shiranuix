using System.Text;
using Shiranuix.Models;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// One line as the sheet will draw it: its words, and where its first
/// character sits across the page's body.
/// </summary>
public sealed record SheetLine(string Text, double Left);

/// <summary>One page: the lines it carries, in the order they are drawn.</summary>
public sealed record SheetPage(IReadOnlyList<SheetLine> Lines);

/// <summary>The whole document as it came out: its pages, first to last.</summary>
public sealed record SheetPlan(IReadOnlyList<SheetPage> Pages);

/// <summary>
/// Lays a document out on paper before anyone draws it: which words land
/// on which line, where that line begins, and which page it falls on.
/// </summary>
/// <remarks>
/// <para>
/// The layout is asked for before the drawing rather than done inside it,
/// because the plan is the part worth testing. Given a way of measuring a
/// piece of writing, the plan is decided entirely here, with no pen in
/// sight - the writer who draws it merely carries out the decisions it
/// already took. Drawing stays thin; the thinking has a place a test can
/// reach.
/// </para>
/// <para>
/// Lines are broken the way every page is set: whole words travel, until a
/// word alone is wider than the sheet has room for, in which case it is cut
/// where the room ends rather than pushed past the right edge. Tabs become
/// four spaces before any measuring: how wide a tab is drawn is a
/// typeface's business, and plain text answers in spaces. So does every
/// other air between words, and the marks nobody can see are not written:
/// see <see cref="PlainWriting"/>.
/// </para>
/// </remarks>
public static class SheetLayout {
    /// <summary>What a piece of writing measures as, in the unit the sheet is drawn in.</summary>
    public delegate double Measure(string text);

    /// <summary>
    /// Lays the document's words out onto pages.
    /// </summary>
    /// <param name="text">The document as written.</param>
    /// <param name="measure">What a piece of writing is as wide as.</param>
    /// <param name="width">The room a line has, across the page's body.</param>
    /// <param name="height">The room the body has, down the page.</param>
    /// <param name="lineHeight">The room one row of text claims.</param>
    /// <param name="align">Which way the lines are set.</param>
    /// <returns>The pages, in order; a first page exists even for an empty document.</returns>
    public static SheetPlan Plan(
        string text,
        Measure measure,
        double width,
        double height,
        double lineHeight,
        TextAlign align) {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineHeight);

        var placements = new List<SheetLine>();

        foreach (var raw in PlainWriting(text).Split('\n')) {
            var line = raw.TrimEnd('\r').Replace("\t", "    ");

            if (string.IsNullOrWhiteSpace(line)) {
                // A line with nothing said in it - an empty one, or one of
                // nothing but spaces - is a row the sheet keeps the air
                // in, and the page counts it as taken even though it
                // holds no marks.
                placements.Add(new SheetLine(string.Empty, 0.0));
                continue;
            }

            LayOut(placements, line, measure, width, align);
        }

        var rowsPerSheet = Math.Max(1, (int)Math.Floor(height / lineHeight));
        var pages = new List<SheetPage>();

        for (var first = 0; first < placements.Count; first += rowsPerSheet) {
            var count = Math.Min(rowsPerSheet, placements.Count - first);
            pages.Add(new SheetPage(placements.GetRange(first, count)));
        }

        return new SheetPlan(pages);
    }

    /// <summary>
    /// The writing as plain text meets it: every air that stands between
    /// words — the space and the airs that refuse to part, the wide ones
    /// and the narrow — becomes one plain space, runs of them become one,
    /// and the marks that carry no ink are not carried at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is set apart at the spaces the words stand between. The kinds
    /// of air a tool slips into the writing — the space that will not give
    /// way, the figure's space, the thin one — mean the same thing to a row
    /// of prose: a place where one word ends and another waits. Left as they
    /// come, a whole sentence laced with them arrives as one word wider
    /// than the sheet, and the sheet cuts it where the room ends, letter by
    /// letter. They become the plain space and take their turn like one.
    /// </para>
    /// <para>
    /// The marks that carry nothing — the joins, the hidden hyphen, the
    /// order-marks a paste smuggles in — are not marks on paper at all.
    /// </para>
    /// </remarks>
    private static string PlainWriting(string text) {
        var builder = new StringBuilder(text.Length);
        var spaced = false;

        foreach (var character in text) {
            switch (character) {
                case >= '\u2000' and <= '\u200A':
                case '\u00A0' or '\u1680' or '\u202F' or '\u205F' or '\u3000':
                    spaced = true;
                    continue;

                case '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF':
                case '\u00AD' or '\uFE0E' or '\uFE0F' or '\u200E' or '\u200F':
                    continue;

                case '\n':
                    builder.Append('\n');
                    spaced = false;
                    continue;

                case ' ' when spaced:
                    continue;

                default:
                    if (spaced) {
                        builder.Append(' ');
                        spaced = false;
                    }

                    builder.Append(character);
                    continue;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Breaks one line of the document into the rows the sheet will carry
    /// and lays each into place, minding how the lines are set.
    /// </summary>
    private static void LayOut(
        ICollection<SheetLine> into,
        string line,
        Measure measure,
        double width,
        TextAlign align) {
        var words = line.Split(' ');
        var deck = string.Empty;
        var head = string.Empty;

        foreach (var word in words) {
            if (word.Length == 0 && deck.Length == 0) {
                // Spaces that lead a line lead it: they wait for the first
                // word and travel with it, so what was written stays
                // written - a poem's short third line keeps its air.
                head += " ";
                continue;
            }

            if (deck.Length > 0) {
                var joined = deck + " " + word;

                if (measure(joined) <= width) {
                    deck = joined;
                    continue;
                }

                GiveUp(into, deck, measure, width, align);
            }

            // Word at the head of a fresh row, with whatever spaces led
            // the line: a whole word if the room takes it, pieces of one
            // when the word alone is wider than everything. The last
            // piece stays in hand, so the word after it may still lean on
            // it if there is room.
            var asked = head + word;

            deck = measure(asked) <= width
                ? asked
                : CutOff(into, asked, measure, width, align);
            head = string.Empty;
        }

        GiveUp(into, deck, measure, width, align);
    }

    /// <summary>Puts what is in hand where it belongs and clears the hand.</summary>
    private static void GiveUp(
        ICollection<SheetLine> into,
        string deck,
        Measure measure,
        double width,
        TextAlign align) {
        if (deck.Length == 0) {
            return;
        }

        into.Add(new SheetLine(deck, PositionOf(deck, measure(deck), width, align)));
    }

    /// <summary>
    /// A word no row can hold: lays pieces of it down and answers with the
    /// last of them, the part still waiting for company.
    /// </summary>
    private static string CutOff(
        ICollection<SheetLine> into,
        string word,
        Measure measure,
        double width,
        TextAlign align) {
        var pieces = new List<string>();
        var piece = string.Empty;

        foreach (var character in word) {
            var longer = piece + character;

            if (piece.Length > 0 && measure(longer) > width) {
                pieces.Add(piece);
                piece = character.ToString();
                continue;
            }

            piece = longer;
        }

        pieces.Add(piece);

        // Pieces before the last have company on their row: only the last
        // keeps waiting.
        for (var index = 0; index < pieces.Count - 1; ++index) {
            var laid = pieces[index];
            into.Add(new SheetLine(laid, PositionOf(laid, measure(laid), width, align)));
        }

        return pieces[^1];
    }

    /// <summary>Where a line of a given width starts, minding how the lines are set.</summary>
    /// <remarks>
    /// Left lines start at the very place prose starts; the other two are
    /// measured against the room, never past it, so a line wider than the
    /// room (a lone character too wide to cut) still draws from the body's
    /// opening rather than from a place before it.
    /// </remarks>
    private static double PositionOf(string line, double lineWidth, double width, TextAlign align) =>
        align switch {
            TextAlign.Center => Math.Max(0.0, (width - lineWidth) / 2.0),
            TextAlign.Right => Math.Max(0.0, width - lineWidth),
            _ => 0.0,
        };
}
