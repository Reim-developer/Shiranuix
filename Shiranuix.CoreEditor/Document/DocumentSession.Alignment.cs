using Shiranuix.CoreFormat;
namespace Shiranuix.CoreEditor;

/// <summary>
/// How the document's lines are set: which stand centred or flush right,
/// the line the caret stands on, and the rule a save writes them under.
/// </summary>
/// <remarks>
/// <para>
/// The map is numbered against the file behind the unmoved text rather
/// than the text on the screen, for the same reason a move is kept that
/// way: typing does not move the file, so a line's number keeps naming the
/// letters it was set beside until a save carries it onto the writing that
/// came out. The window only ever shows part of the document, and a line
/// numbered inside the window would change every time the window slides —
/// alignments that slid with the view would land on the wrong letters
/// whenever the file was written.
/// </para>
/// <para>
/// Flush left is what a line is when nothing said anything about it, so a
/// line set back to left leaves the map rather than joining it: the map
/// holds only the lines that were given a say, and an empty one is exactly
/// what a file that has never been aligned carries.
/// </para>
/// </remarks>
public sealed partial class DocumentSession {
    /// <summary>
    /// Every line flush left — what a document stands on before a file has
    /// said anything, and what it falls back to when the file carried
    /// nothing.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, ParagraphAlignment> None =
        new Dictionary<int, ParagraphAlignment>();

    /// <summary>
    /// How each line of the file behind the unmoved text is set, by its
    /// number there — the map writing lays into a <c>.shiranux</c> file.
    /// </summary>
    private IReadOnlyDictionary<int, ParagraphAlignment> _alignments = None;

    /// <summary>How each line of the file is set, by its number there.</summary>
    public IReadOnlyDictionary<int, ParagraphAlignment> Alignments => _alignments;

    /// <summary>
    /// How the line the caret stands on is set — flush left unless the file
    /// said otherwise.
    /// </summary>
    /// <remarks>
    /// The caret's line is the line a press of the bar's chips would change,
    /// so this is the answer those chips ask. Its number is worked out in
    /// the file rather than in the window's text, because the alignments
    /// are numbered in the file and the caret stands in the window.
    /// </remarks>
    public ParagraphAlignment AlignmentAtCaret =>
        _alignments.TryGetValue(CaretSourceLine(), out var alignment)
            ? alignment
            : ParagraphAlignment.Left;

    /// <summary>
    /// Sets how the line the caret stands on is set, leaving every other
    /// line where it was.
    /// </summary>
    /// <param name="alignment">Flush left, centred, or flush right.</param>
    public void AlignCaret(ParagraphAlignment alignment) {
        var line = CaretSourceLine();
        var next = new Dictionary<int, ParagraphAlignment>(_alignments);

        if (alignment is ParagraphAlignment.Left) {
            next.Remove(line);
        } else {
            next[line] = alignment;
        }

        SetAlignments(next);
    }

    /// <summary>
    /// Puts a new map in place and tells every door that reads one of its
    /// three faces.
    /// </summary>
    /// <param name="alignments">The map to stand on from here.</param>
    private void SetAlignments(IReadOnlyDictionary<int, ParagraphAlignment> alignments) {
        _alignments = alignments ?? throw new ArgumentNullException(nameof(alignments));

        OnPropertyChanged(nameof(Alignments));
        OnPropertyChanged(nameof(AlignmentAtCaret));
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>
    /// How the line with a given number in the window's text is set — the
    /// same question <see cref="AlignmentAtCaret"/> answers for the line
    /// the caret stands on, asked for the page's drawing about any line
    /// at all.
    /// </summary>
    /// <param name="line">The line's number in the window's text, from
    /// zero — the number a formatter counts by breaking the text.</param>
    /// <returns>How that line is set: what the file said about the file
    /// line it stands on, or flush left when the file said nothing.</returns>
    /// <remarks>
    /// The line is counted in the window and answered from the file,
    /// the same way the caret's own line is, so that what is drawn
    /// beside a line is the very thing a save would write for it. The
    /// counts the answer needs are kept against the texts they were
    /// counted in, because the page asks once per line and a window's
    /// worth of lines would otherwise walk the window's text once each.
    /// </remarks>
    public ParagraphAlignment AlignmentAtWindowLine(int line) {
        var text = Text;
        var starts = WindowLineStarts(text);
        var position = starts[Math.Clamp(line, 0, starts.Length - 1)];

        var inFile = WindowCaret.ToFile(_ledger, position, text.Length);
        var original = _ledger.WindowOriginal;
        var local = Math.Clamp(inFile - _ledger.WindowFrom, 0, original.Length);

        return _alignments.TryGetValue(
            _window.FileLine + SourceLineCounts(original)[local], out var alignment)
            ? alignment
            : ParagraphAlignment.Left;
    }

    /// <summary>
    /// Where each line of the window's text begins, counted against the
    /// text it was counted in and held until that text is gone.
    /// </summary>
    /// <param name="text">The window's text as it stands now.</param>
    /// <returns>The text index each line begins at, the first at zero.</returns>
    private int[] WindowLineStarts(string text) {
        if (!ReferenceEquals(_windowLineStartsText, text)) {
            var starts = new List<int> { 0 };

            for (var index = 0; index < text.Length; index++) {
                if (text[index] == '\n') {
                    starts.Add(index + 1);
                }
            }

            _windowLineStartsText = text;
            _windowLineStarts = starts.ToArray();
        }

        return _windowLineStarts;
    }

    /// <summary>
    /// How many lines begin at or before each position of the window's
    /// original text, counted against the text it was counted in.
    /// </summary>
    /// <param name="original">The text the window was opened over, as it
    /// stands now.</param>
    /// <returns>The line count for every position, one past the end
    /// included.</returns>
    private int[] SourceLineCounts(string original) {
        if (!ReferenceEquals(_sourceLineCountsText, original)) {
            var counts = new int[original.Length + 1];

            for (var index = 0; index < original.Length; index++) {
                counts[index + 1] = counts[index] + (original[index] == '\n' ? 1 : 0);
            }

            _sourceLineCountsText = original;
            _sourceLineCounts = counts;
        }

        return _sourceLineCounts;
    }

    /// <summary>
    /// The window's text the line starts were last counted against, so a
    /// count is only ever taken once per text.
    /// </summary>
    private string? _windowLineStartsText;

    /// <summary>
    /// Where each line of that text began when it was counted.
    /// </summary>
    private int[] _windowLineStarts = [0];

    /// <summary>
    /// The window's original text the file line counts were last counted
    /// against, so a count is only ever taken once per text.
    /// </summary>
    private string? _sourceLineCountsText;

    /// <summary>
    /// How many lines began at or before each position of that text when
    /// it was counted.
    /// </summary>
    private int[] _sourceLineCounts = [0];

    /// <summary>
    /// The line the caret stands on, counted in the file behind the window.
    /// </summary>
    /// <returns>
    /// The line's own number: every line above the window, plus the lines
    /// inside it before where the caret stands — counted in the text the
    /// window was filled with rather than the text the writer has made of
    /// it, so a line typed above renumbers nothing before a save has
    /// carried the whole writing down.
    /// </returns>
    private int CaretSourceLine() {
        var inFile = WindowCaret.ToFile(_ledger, CaretIndex, Text.Length);
        var original = _ledger.WindowOriginal;
        var local = Math.Clamp(inFile - _ledger.WindowFrom, 0, original.Length);

        return _window.FileLine + CountLines(original, local);
    }

    /// <summary>
    /// How many lines begin before a position: one per break in front of it.
    /// </summary>
    /// <param name="text">The text the position stands in.</param>
    /// <param name="before">How far into it to count.</param>
    /// <returns>The zero-based number of the line the position stands on.</returns>
    private static int CountLines(string text, int before) {
        var lines = 0;

        for (var index = 0; index < before && index < text.Length; index++) {
            if (text[index] == '\n') {
                lines++;
            }
        }

        return lines;
    }
}
