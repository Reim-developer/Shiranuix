using Shiranuix.CoreFormat;
namespace Shiranuix.CoreEditor;

/// <summary>
/// How the document's lines are set: which stand centred or flush right,
/// the line the caret stands on, and the rule a save writes them under.
/// </summary>
/// <remarks>
/// <para>
/// The map is numbered the way the gutter numbers the writing: by each
/// line's place in the whole file as it now stands — the window's own
/// lines counted from wherever the window opens, with every line the moves
/// before it added or took out carried on top. A line's number therefore
/// names the letters standing beside it at the instant, and when typing
/// opens or folds a line the faces are carried along with the letters they
/// were set over: a heading centred on line two stays the centred heading
/// while a line typed above turns it into line three. The carrying a save
/// used to make at the end is made here at every move instead, so the map
/// the page draws from, the chips ask, and the file is written with are
/// one and the same at every moment.
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
    /// How each line of the writing is set, by its number in the whole
    /// file as it stands now — the map writing lays into a
    /// <c>.shiranuix</c> file, carried along whenever lines come or go.
    /// </summary>
    private IReadOnlyDictionary<int, ParagraphAlignment> _alignments = None;

    /// <summary>How each line of the writing is set, by its number there.</summary>
    public IReadOnlyDictionary<int, ParagraphAlignment> Alignments => _alignments;

    /// <summary>
    /// How the line the caret stands on is set — flush left unless the file
    /// said otherwise.
    /// </summary>
    /// <remarks>
    /// The caret's line is the line a press of the bar's chips would change,
    /// so this is the answer those chips ask. Its number is counted the way
    /// the gutter counts it — the window's lines from wherever the window
    /// opens, the caret's own place among them counted in the text as the
    /// writer has made it — because the map is numbered in exactly that way
    /// and the chips stand on the map.
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
    /// <returns>How that line is set: what the map says about the line it
    /// stands on, or flush left when it says nothing.</returns>
    /// <remarks>
    /// The line is asked the way the gutter numbers it — its place in the
    /// window plus every line above the window — so what is drawn beside a
    /// line is the very thing a save would write for it, and the answer
    /// walks the map rather than the text: no line of the window's own is
    /// ever counted here, because the map already names it.
    /// </remarks>
    public ParagraphAlignment AlignmentAtWindowLine(int line) =>
        _alignments.TryGetValue(LinesAboveWindow() + line, out var alignment)
            ? alignment
            : ParagraphAlignment.Left;

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
                if (Newline.IsLineFeed(text[index])) {
                    starts.Add(index + 1);
                }
            }

            _windowLineStartsText = text;
            _windowLineStarts = starts.ToArray();
        }

        return _windowLineStarts;
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
    /// Carries the map along when the writing gains or loses lines: every
    /// entry's face is followed to the line its own letters now open, the
    /// way a save carried them at the end, made here at every move instead.
    /// </summary>
    /// <param name="oldText">The window's text before the move.</param>
    /// <param name="newText">The window's text after it.</param>
    /// <param name="start">Where the move began, in the window's text.</param>
    /// <param name="removed">What the move took out there.</param>
    /// <param name="inserted">What the move put there instead.</param>
    /// <remarks>
    /// <para>
    /// A move that opens no line and folds none away leaves every face
    /// beside the letters it was set beside, so most moves — every plain
    /// stretch of typing and deleting — never touch the map at all. One
    /// that does open or fold lines walks each face the same way the
    /// letters went: the line's opening position is carried through the
    /// move and re-counted against the text that came out, and a face
    /// whose whole paragraph — letters and its own trailing break — was
    /// erased is dropped rather than left pointing at whatever slid into
    /// its number. Two faces landing on one line keep the one that came
    /// first, and a number claimed by lines below the window steps by how
    /// many the move added or took out, since their letters stand past
    /// everything the window can see of the move.
    /// </para>
    /// <para>
    /// A restore — an undo landing, a redo — runs the very same walk with
    /// the move's own two texts traded places, so the faces come back up
    /// with the letters. A face dropped by an erasure stays dropped: what
    /// carried it was a face belonging to letters the map no longer holds
    /// anywhere, and the walk keeps only what it can still follow.
    /// </para>
    /// </remarks>
    private void RekeyAlignments(
        string oldText,
        string newText,
        int start,
        string removed,
        string inserted) {
        if (_alignments.Count == 0) {
            return;
        }

        var folded = CountLines(oldText, start + removed.Length) - CountLines(oldText, start);
        var opened = CountLines(inserted, inserted.Length);

        if (folded == 0 && opened == 0) {
            // No line was opened and none was folded away: every face
            // still stands beside the letters it was given beside.
            return;
        }

        var above = LinesAboveWindow();
        var starts = WindowLineStarts(oldText);
        var shift = opened - folded;
        var next = new Dictionary<int, ParagraphAlignment>();

        // Ascending, so a number two faces land on keeps the one that came
        // first — the same rule the carrying at save time stood on.
        foreach (var pair in _alignments.OrderBy(pair => pair.Key)) {
            var windowLine = pair.Key - above;

            if (windowLine < 0) {
                // Above the window: the letters stand before anything this
                // move can reach, so the face stays on its number.
                next[pair.Key] = pair.Value;
                continue;
            }

            if (windowLine >= starts.Length) {
                // Below the window: the letters themselves are past what
                // the window holds, so the face only steps by how many
                // lines the move added or took out above them.
                next[pair.Key + shift] = pair.Value;
                continue;
            }

            var position = starts[windowLine];

            if (inserted.Length == 0 && IsErasedWhole(oldText, position, start, removed.Length)) {
                // The letters whole — start to their own trailing break,
                // with nothing in their place: the face belonged to
                // writing that is no longer there, and no line sliding
                // into the number inherits it.
                continue;
            }

            var landed = Math.Clamp(
                MapThroughMove(position, start, removed.Length, inserted.Length), 0, newText.Length);
            next.TryAdd(above + CountLines(newText, landed), pair.Value);
        }

        // A walk that lands every face back on its own number moves
        // nothing, and a document that has moved nothing says nothing.
        if (!HoldsSameAnswers(next, _alignments)) {
            SetAlignments(next);
        }
    }

    /// <summary>
    /// Where a position in the old text stands in the new: one wholly
    /// behind the move steps by the move's own length, one inside it lands
    /// at the move's head — where the letters it watched have gone — and
    /// one ahead of it stays put.
    /// </summary>
    private static int MapThroughMove(int position, int start, int removed, int inserted) =>
        start + removed <= position
            ? position + inserted - removed
            : start > position
                ? position
                : start;

    /// <summary>
    /// Whether a move erased a paragraph whole — its opening position
    /// through its own trailing break, and nothing put in their place.
    /// </summary>
    /// <remarks>
    /// A paragraph gone from the page has no face to carry: the face
    /// belonged to letters that are no longer there. The trailing break
    /// counts as part of the paragraph, because a paragraph that ends the
    /// text has no break of its own to protect it — reaching the end is
    /// the same erasure.
    /// </remarks>
    private static bool IsErasedWhole(string text, int position, int start, int removed) {
        var line = text.IndexOf('\n', position);
        var end = line < 0 ? text.Length : line + 1;

        return start <= position && start + removed >= end;
    }

    /// <summary>
    /// Whether two maps hold the same answers, entry for entry.
    /// </summary>
    private static bool HoldsSameAnswers(
        IReadOnlyDictionary<int, ParagraphAlignment> left,
        IReadOnlyDictionary<int, ParagraphAlignment> right) {
        if (left.Count != right.Count) {
            return false;
        }

        foreach (var (line, alignment) in left) {
            if (!right.TryGetValue(line, out var now) || now != alignment) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// How many lines the window's text gives way to before its own first
    /// line: every line the file holds above it, plus whatever the moves
    /// before the window did to those lines.
    /// </summary>
    /// <returns>
    /// The zero-based number the window's first line carries — the gutter's
    /// own number one less, worked out of the same arithmetic so the two
    /// can never stand on different answers.
    /// </returns>
    private int LinesAboveWindow() =>
        DocumentCounts.FirstLineNumber(_ledger, _window.FileLine) - 1;

    /// <summary>
    /// The line the caret stands on, counted the way the gutter counts it.
    /// </summary>
    /// <returns>
    /// The line's own number: every line above the window, plus the lines
    /// inside it before where the caret stands — counted in the text as
    /// the writer has made of it, so a line typed above carries the
    /// numbers below it down with it the moment it is typed.
    /// </returns>
    private int CaretSourceLine() => LinesAboveWindow() + CountLines(Text, CaretIndex);

    /// <summary>
    /// How many lines begin before a position: one per break in front of it.
    /// </summary>
    /// <param name="text">The text the position stands in.</param>
    /// <param name="before">How far into it to count.</param>
    /// <returns>The zero-based number of the line the position stands on.</returns>
    private static int CountLines(string text, int before) {
        var lines = 0;

        for (var index = 0; index < before && index < text.Length; index++) {
            if (Newline.IsLineFeed(text[index])) {
                lines++;
            }
        }

        return lines;
    }
}
