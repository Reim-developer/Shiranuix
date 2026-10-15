using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The document's counts and the gutter's run of numbers: the whole file's
/// words, characters and lines, and one line number per line on screen.
/// </summary>
/// <remarks>
/// <para>
/// Worked out of the text and nothing else, so it can be checked without a
/// window or a file. The counts are the whole file's even though the
/// document only holds a window of it: the scan's numbers are the baseline,
/// the window's own stretch is swapped from the file's text to the
/// document's, and every move beyond the window's edges is added over the
/// top — its text is not on screen, but it is still part of the file.
/// </para>
/// <para>
/// The gutter's numbers and the counts share one line arithmetic, so they
/// are worked out together, once per move of the text — the numbers read
/// back as a field, not a second measuring.
/// </para>
/// </remarks>
public sealed partial class DocumentCounts : ObservableObject {
    /// <summary>Word count, refreshed on every edit.</summary>
    [ObservableProperty]
    private int _wordCount;

    /// <summary>Character count, refreshed on every edit.</summary>
    [ObservableProperty]
    private int _characterCount;

    /// <summary>Line count, refreshed on every edit.</summary>
    [ObservableProperty]
    private int _lineCount;

    /// <summary>
    /// The line numbers for the gutter: one per line on screen, newline
    /// separated, each the line's number in the file rather than in the
    /// window.
    /// </summary>
    /// <remarks>
    /// A single string rather than one control per line — the same reason the
    /// text is one control. The first number is the window's place in the
    /// file plus whatever the moves before it did to the lines above; a
    /// window of a small file numbers itself from one, as it always has. An
    /// empty document still numbers its first line, because the cursor is on
    /// it whether anything has been typed or not.
    /// </remarks>
    public string LineNumbersText => _lineNumbersText;

    /// <summary>
    /// The number the window's first line carries — the same number the
    /// gutter begins with, kept for the caret's own line, which is its
    /// place in the file rather than in the window.
    /// </summary>
    public int FirstLine { get; private set; } = 1;

    /// <summary>
    /// The gutter's numbers as <see cref="Refresh"/> last worked them out.
    /// </summary>
    private string _lineNumbersText = string.Empty;

    /// <summary>
    /// Works every count out anew: the whole file's from the scan that read
    /// it, with the window's own stretch swapped to the document's text and
    /// the moves outside the window added over the top.
    /// </summary>
    /// <param name="scan">What a scan said of the whole file.</param>
    /// <param name="ledger">The moves standing against the file.</param>
    /// <param name="fileLine">The line the window stands on in the file.</param>
    /// <param name="text">The window's text as the document shows it.</param>
    /// <remarks>
    /// The window's own counts come off the ledger, measured when the window
    /// arrived rather than scanned again for every keystroke; the moves
    /// outside it are measured as they are read, because their text is
    /// nowhere else held in one piece.
    /// </remarks>
    public void Refresh(ScanResult scan, EditLedger ledger, int fileLine, string text) {
        var measured = TextStatistics.Measure(text);
        var window = ledger.WindowStats;

        var words = scan.Words - window.Words + measured.Words;
        var characters = scan.Characters - window.Characters + measured.Characters;
        var newlines = Newlines(scan.Lines) - Newlines(window) + Newlines(measured);

        foreach (var entry in ledger.EntriesOutside()) {
            // The counts of a move standing outside the window are worked
            // out once and held — its text never changes, and every
            // keystroke would otherwise re-read it whole.
            var outsideText = entry.TextStats;
            var outsideOriginal = entry.OriginalStats;

            words += outsideText.Words - outsideOriginal.Words;
            characters += outsideText.Characters - outsideOriginal.Characters;
            newlines += Newlines(outsideText) - Newlines(outsideOriginal);
        }

        WordCount = words;
        CharacterCount = characters;

        // Lines follow from the newlines and whether the file says anything
        // at all — the one count the window alone cannot decide, because an
        // empty window is one line of a file with content and no line of a
        // file without.
        LineCount = characters > 0 ? newlines + 1 : 0;

        // The gutter's run of numbers, beside the counts it shares its line
        // arithmetic with: one string per move of the text, read straight
        // off the field whenever the view asks.
        FirstLine = FirstLineNumber(ledger, fileLine);
        _lineNumbersText = string.Join(
            '\n', Enumerable.Range(FirstLine, Math.Max(measured.Lines, 1)));

        OnPropertyChanged(nameof(FirstLine));
        OnPropertyChanged(nameof(LineNumbersText));
    }

    /// <summary>
    /// The number the window's first line carries: its place in the file
    /// from the walk that brought the window there, plus every line the
    /// moves before the window added or took out above it.
    /// </summary>
    /// <param name="ledger">The moves standing against the file.</param>
    /// <param name="fileLine">The line the window stands on in the file.</param>
    /// <returns>The first line's number, counted from one.</returns>
    private static int FirstLineNumber(EditLedger ledger, int fileLine) {
        var lines = fileLine;

        foreach (var entry in ledger.EntriesBefore(ledger.WindowFrom)) {
            lines += Newlines(entry.TextStats) - Newlines(entry.OriginalStats);
        }

        return lines + 1;
    }

    /// <summary>
    /// The newlines a set of counts stands for: a text with content has one
    /// more line than it has newlines, and an empty one has neither.
    /// </summary>
    /// <param name="stats">The counts to read.</param>
    /// <returns>How many newlines the text holds.</returns>
    private static int Newlines(TextStatisticsResult stats) => stats.Lines > 0 ? stats.Lines - 1 : 0;

    /// <summary>
    /// The newlines a scan counted in a file, as a number of lines to work
    /// with — zero for a file with no lines at all.
    /// </summary>
    /// <param name="lines">The scan's line count.</param>
    /// <returns>The same count.</returns>
    private static int Newlines(int lines) => lines > 0 ? lines - 1 : 0;
}
