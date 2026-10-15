using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// Where a document's window stands in its file, what it remembers of where
/// it has stood, and how it moves.
/// </summary>
/// <remarks>
/// <para>
/// A large file is never on screen whole: the window is the fifty lines the
/// document shows at once, and this owns their place — the file anchor they
/// begin at, the line they begin on, whether the file carries on past them,
/// and the walk's starting points for getting anywhere else. Moving is done
/// here: planning a window over a line or an offset, snapping both edges over
/// any move they would cut so no entry is ever halved, and sliding every
/// remembered coordinate when a save collapses the moves in.
/// </para>
/// <para>
/// It never touches the text or the caret: a plan comes back as data and the
/// session lays it over the screen. It reads through <see cref="IFileService"/>
/// and is handed the ledger to ask about moves — it holds neither, which is
/// what lets the whole engine run headless against a faked file service.
/// </para>
/// </remarks>
public sealed class DocumentWindow {
    /// <summary>
    /// How many lines the window holds while the file carries on past it.
    /// </summary>
    public const int Lines = 50;

    /// <summary>
    /// How many lines a slide keeps between the caret and the edge it just
    /// crossed, so a writer moving through a long file always has context
    /// above or below where they are working.
    /// </summary>
    private const int ContextLines = 10;

    /// <summary>
    /// How many lines a page key turns: the window's own size less the
    /// context a slide that lands a caret keeps — so a page after a page
    /// still leaves ten lines of the last one in sight.
    /// </summary>
    public const int PageLines = Lines - ContextLines;

    /// <summary>
    /// How many window starts are remembered for sliding back and for
    /// undo's return trips. Capped the way the history is, and never below
    /// the file's own start: every walk falls back on that one.
    /// </summary>
    private const int AnchorLimit = 1000;

    private readonly IFileService _files;

    /// <summary>
    /// Every window start this document has stood at, with the file line it
    /// stood on, in the order they fall. An undo made in a window that has
    /// since slid away is put back by walking from the nearest of these:
    /// from the file's start, every time, would be the whole file.
    /// </summary>
    private readonly List<(LineAnchor At, int FileLine)> _anchors = new();

    /// <summary>
    /// Creates the window over the file service it walks and reads with.
    /// </summary>
    /// <param name="files">File I/O.</param>
    public DocumentWindow(IFileService files) =>
        _files = files ?? throw new ArgumentNullException(nameof(files));

    /// <summary>
    /// Where the window begins in the file, in the file's own coordinates —
    /// the ones the ledger keeps its moves in, so the two compare directly.
    /// </summary>
    public LineAnchor Start { get; private set; }

    /// <summary>
    /// The file line the window starts on, counted from zero. The gutter's
    /// numbers begin one past it, plus whatever the moves before the window
    /// did to the lines above.
    /// </summary>
    public int FileLine { get; private set; }

    /// <summary>
    /// True while the file carries on past the window's end — the
    /// difference between a window that can slide forward and one that is
    /// already showing the file's tail.
    /// </summary>
    public bool AtEnd { get; private set; }

    /// <summary>
    /// Points the window at a fresh place with no walk behind it: the file
    /// it would show, the line it begins on, and nothing remembered before
    /// this — the way a new or newly opened document starts.
    /// </summary>
    /// <param name="start">Where the window begins in the file.</param>
    /// <param name="fileLine">The file line it begins on, from zero.</param>
    /// <param name="atEnd">Whether the file carries on past the window.</param>
    public void Begin(LineAnchor start, int fileLine, bool atEnd) {
        Start = start;
        FileLine = fileLine;
        AtEnd = atEnd;

        _anchors.Clear();
        _anchors.Add((start, fileLine));
    }

    /// <summary>
    /// Plans the window over a file line, walked from the nearest window
    /// start rather than from the file's own start.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="fileLine">The line the window should begin on.</param>
    /// <param name="ledger">The moves standing against the file, for
    /// keeping every entry whole.</param>
    /// <returns>The window over that line.</returns>
    public WindowPlan Plan(string path, int fileLine, EditLedger ledger) {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(ledger);

        var anchor = AnchorAtOrBeforeLine(fileLine);
        var walk = _files.MoveLines(path, anchor.At, fileLine - anchor.FileLine);

        return ReadPlanned(path, walk.At, anchor.FileLine + walk.Lines, ledger);
    }

    /// <summary>
    /// Plans the window over an exact file position — the stretch an undo's
    /// return trip names — walked from the nearest window start at or before
    /// it.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="offset">The file position the window should begin at.</param>
    /// <param name="ledger">The moves standing against the file, for
    /// keeping every entry whole.</param>
    /// <returns>The window over that position, or nothing when the file no
    /// longer has the stretch the position names.</returns>
    public WindowPlan? PlanAt(string path, int offset, EditLedger ledger) {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(ledger);

        var anchor = AnchorAtOrBefore(offset);
        var walk = _files.LineStartOf(path, anchor.At, offset);

        if (walk.At.Offset != offset) {
            return null;
        }

        return ReadPlanned(path, walk.At, anchor.FileLine + walk.Lines, ledger);
    }

    /// <summary>
    /// Takes the window to a planned place and remembers it as one of the
    /// starts later walks may begin from.
    /// </summary>
    /// <param name="plan">The window to adopt.</param>
    public void Adopt(WindowPlan plan) {
        Start = plan.Start;
        FileLine = plan.FileLine;
        AtEnd = plan.AtEnd;
        AddAnchor(plan.Start, plan.FileLine);
    }

    /// <summary>
    /// Where a forward slide should begin: ten lines of context stay between
    /// the caret's line and the new front edge, but never over a window that
    /// has already passed the caret.
    /// </summary>
    /// <param name="caretLine">The file line the caret stands on.</param>
    /// <returns>The line the window should begin on.</returns>
    public int ForwardTarget(int caretLine) => Math.Max(caretLine - ContextLines, FileLine + 1);

    /// <summary>
    /// Where a backward slide should begin: ten lines of context stay below
    /// the caret the window came from — the mirror of a forward slide.
    /// </summary>
    /// <returns>The line the window should begin on.</returns>
    public int BackTarget() {
        var target = FileLine + ContextLines - Lines;

        if (target < 0) {
            target = 0;
        }

        if (target >= FileLine) {
            target = FileLine - 1;
        }

        return target;
    }

    /// <summary>
    /// Slides every coordinate the window remembers after what collapsed
    /// into the file that was just written: its own start, its line, and
    /// every anchor a later walk may begin from. The window keeps showing
    /// exactly what it showed — only where it stands has changed.
    /// </summary>
    /// <param name="ledger">The moves as they stood against the file when
    /// it was written, before the ledger takes the new file as its zero.</param>
    public void Shift(EditLedger ledger) {
        ArgumentNullException.ThrowIfNull(ledger);

        var window = ledger.ShiftAt(Start.Offset);

        Start = new LineAnchor(Start.Byte + window.Bytes, Start.Offset + window.Characters);
        FileLine += window.Lines;

        for (var index = 0; index < _anchors.Count; index++) {
            var anchor = _anchors[index];
            var shift = ledger.ShiftAt(anchor.At.Offset);

            _anchors[index] = (
                new LineAnchor(anchor.At.Byte + shift.Bytes, anchor.At.Offset + shift.Characters),
                anchor.FileLine + shift.Lines);
        }
    }

    /// <summary>
    /// Reads the window at a walked position: both edges stepped out over
    /// any move they would otherwise cut, then the lines themselves.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="start">Where the walk landed.</param>
    /// <param name="fileLine">The file line it landed on.</param>
    /// <param name="ledger">The moves standing against the file.</param>
    /// <returns>The window.</returns>
    /// <remarks>
    /// Both edges step out over any move they would cut: a window holding
    /// half a move cannot show it whole, and the writer's moves are never
    /// cut in half. That is what lets the window grow past its fifty lines —
    /// or shrink its start below the line asked for — and it is why every
    /// entry stays whole however many windows pass over it.
    /// </remarks>
    private WindowPlan ReadPlanned(string path, LineAnchor start, int fileLine, EditLedger ledger) {
        // The front edge steps back to the line any cut move begins on.
        while (ledger.CutAt(start.Offset) is { } cut) {
            var snap = _files.LineStartOf(path, start, cut.Start);

            if (snap.At.Offset >= start.Offset) {
                break;
            }

            fileLine += snap.Lines;
            start = snap.At;
        }

        var lines = Lines;
        var read = _files.ReadLines(path, start, lines);

        // The trailing edge steps forward to the line any cut move ends
        // on — a paste spanning the edge widens the window to hold it.
        while (ledger.CutAt(read.End.At.Offset - 1) is { } beyond) {
            var extend = _files.LineStartOf(path, read.End.At, beyond.Start + beyond.Original.Length);
            lines += extend.Lines + 1;

            if (lines <= 0) {
                break;
            }

            read = _files.ReadLines(path, start, lines);
        }

        return new WindowPlan(start, fileLine, read.Text, read.More);
    }

    /// <summary>
    /// The remembered window start at or before a file position, so a walk
    /// covers only the lines between the two.
    /// </summary>
    /// <param name="offset">The file position, in characters.</param>
    /// <returns>The nearest window start and the line it stood on.</returns>
    private (LineAnchor At, int FileLine) AnchorAtOrBefore(int offset) {
        var best = _anchors[0];

        foreach (var anchor in _anchors) {
            if (anchor.At.Offset > offset) {
                break;
            }

            best = anchor;
        }

        return best;
    }

    /// <summary>
    /// The remembered window start at or before a file line.
    /// </summary>
    /// <param name="fileLine">The line, from zero.</param>
    /// <returns>The nearest window start and the line it stood on.</returns>
    private (LineAnchor At, int FileLine) AnchorAtOrBeforeLine(int fileLine) {
        var best = _anchors[0];

        foreach (var anchor in _anchors) {
            if (anchor.FileLine > fileLine) {
                break;
            }

            best = anchor;
        }

        return best;
    }

    /// <summary>
    /// Remembers where a window stood, in the order the positions fall.
    /// </summary>
    /// <param name="at">Where the window began.</param>
    /// <param name="fileLine">The line it began on.</param>
    private void AddAnchor(LineAnchor at, int fileLine) {
        foreach (var anchor in _anchors) {
            if (anchor.At.Offset == at.Offset) {
                return;
            }
        }

        var index = _anchors.Count;

        while (index > 0 && _anchors[index - 1].At.Offset > at.Offset) {
            index--;
        }

        _anchors.Insert(index, (at, fileLine));

        // Undo cannot reach further back than the history itself, so the
        // windows it might return through stay bounded the same way — while
        // the file's own start, every walk falls back on, is never dropped.
        while (_anchors.Count > AnchorLimit) {
            _anchors.RemoveAt(1);
        }
    }
}
