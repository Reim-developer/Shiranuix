using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// Every way the window travels over the file: the caret reaching an edge,
/// the wheel and the page keys asking to see elsewhere, the whole-file
/// jumps, and the history's own recall of where a move was made.
/// </summary>
/// <remarks>
/// The plans are <see cref="DocumentWindow"/>'s business — walking from the
/// nearest remembered window start and stepping both edges out over any
/// move they would cut — and putting one on screen is the stage's; this
/// class decides when to ask for which, and never touches the text itself.
/// The caret's place in the file travels with the window, whatever moves
/// it, and is clamped into whatever is shown afterwards.
/// </remarks>
public sealed class WindowNavigator {
    /// <summary>
    /// Where the window stands in the file and how it gets anywhere else.
    /// </summary>
    private readonly DocumentWindow _window;

    /// <summary>
    /// What the writer has changed about the file — the moves the caret's
    /// place has to be told through.
    /// </summary>
    private readonly EditLedger _ledger;

    /// <summary>
    /// Creates a navigator over the window engine and the moves standing
    /// against the file.
    /// </summary>
    /// <param name="window">Where the window stands and how it moves.</param>
    /// <param name="ledger">The moves standing against the file.</param>
    public WindowNavigator(DocumentWindow window, EditLedger ledger) {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    /// <summary>
    /// True while the file carries lines above the window — the news the
    /// view's top edge draws its cue from.
    /// </summary>
    public bool HasLinesAbove => _window.Start.Offset > 0;

    /// <summary>
    /// True while the file carries lines past the window — the same news
    /// at the bottom edge.
    /// </summary>
    public bool HasLinesBelow => _window.AtEnd;

    /// <summary>
    /// Slides the window by a number of lines — the writer asking to see
    /// elsewhere in the file with the wheel, a page key or a gesture —
    /// with the caret's own place in the file carried across and clamped
    /// into whatever is shown afterwards.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    /// <param name="lines">How far to move: forward is positive, back is
    /// negative.</param>
    /// <remarks>
    /// The window never stands on lines it has not read: a forward slide
    /// stops at the last line it is already holding, because the file has
    /// at least those — <see cref="HasLinesBelow"/> says it carries on —
    /// but anything past them may not be there. Each stand knows its own
    /// stretch of file; the next notch walks a little further.
    /// </remarks>
    public void SlideBy(IWindowStage stage, int lines) {
        ArgumentNullException.ThrowIfNull(stage);

        if (stage.Path is null || lines == 0) {
            return;
        }

        var caret = WindowCaret.ToFile(_ledger, stage.CaretIndex, stage.Text.Length);

        if (lines > 0) {
            if (!_window.AtEnd) {
                // The window already reaches the file's end.
                return;
            }

            var forward = Math.Min(lines, DocumentWindow.Lines - 1);
            MoveWindow(stage, _window.FileLine + forward, caret);
            return;
        }

        var target = Math.Max(_window.FileLine + lines, 0);

        if (target >= _window.FileLine) {
            // The window already begins the file.
            return;
        }

        MoveWindow(stage, target, caret);
    }

    /// <summary>
    /// Takes the window back to the file's own beginning — Ctrl+Home's
    /// whole-file jump, from whatever corner of the file is on screen.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    public void SlideToStart(IWindowStage stage) {
        ArgumentNullException.ThrowIfNull(stage);

        if (stage.Path is null || _window.Start.Offset == 0) {
            // Nothing to travel: the window already begins the file.
            return;
        }

        MoveWindow(stage, 0, WindowCaret.ToFile(_ledger, stage.CaretIndex, stage.Text.Length));
    }

    /// <summary>
    /// Takes the window to the file's last stretch — Ctrl+End's whole-file
    /// jump — read straight off the scan's line count rather than slid to
    /// one window at a time.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    public void SlideToEnd(IWindowStage stage) {
        ArgumentNullException.ThrowIfNull(stage);

        if (stage.Path is null) {
            return;
        }

        var target = Math.Max(stage.Scan.Lines - DocumentWindow.Lines, 0);

        if (target == _window.FileLine && !_window.AtEnd) {
            // The window is already showing the file's tail.
            return;
        }

        MoveWindow(stage, target, WindowCaret.ToFile(_ledger, stage.CaretIndex, stage.Text.Length));
    }

    /// <summary>
    /// Takes the window forward: the caret stands on the window's last
    /// line, and ten lines of it stay above the edge the caret is about to
    /// cross, so the writer keeps their context as they go.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    public void SlideForward(IWindowStage stage) {
        ArgumentNullException.ThrowIfNull(stage);

        if (!_window.AtEnd) {
            // The window already reaches the file's end.
            return;
        }

        var caretLine = _window.FileLine + TextStatistics.Lines(stage.Text) - 1;
        var target = _window.ForwardTarget(caretLine);

        MoveWindow(stage, target, _ledger.WindowFrom + _ledger.WindowOriginal.Length);
    }

    /// <summary>
    /// Takes the window back: ten lines of it stay below the caret it came
    /// from, the mirror of a slide forward.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    public void SlideBack(IWindowStage stage) {
        ArgumentNullException.ThrowIfNull(stage);

        if (_window.Start.Offset == 0) {
            // The window already begins the file.
            return;
        }

        MoveWindow(stage, _window.BackTarget(), _ledger.WindowFrom);
    }

    /// <summary>
    /// Puts the window back over the stretch a history entry was made in,
    /// when the document is not already showing it.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    /// <param name="target">The entry's window.</param>
    /// <remarks>
    /// The window's start is a line of the file, and the anchors remember
    /// where past windows stood: the nearest one at or before the entry's
    /// window is walked forward from, a handful of lines at most. What comes
    /// back is read fresh, moves laid over it the same as always — if the
    /// file no longer has the stretch the entry names, the document stays
    /// where it is rather than show the wrong text.
    /// </remarks>
    public void ReturnTo(IWindowStage stage, UndoTarget target) {
        ArgumentNullException.ThrowIfNull(stage);

        if (_ledger.WindowFrom == target.WindowFrom && _ledger.WindowTo == target.WindowTo) {
            // The window is already where the move was made.
            return;
        }

        if (stage.Path is null) {
            return;
        }

        if (_window.PlanAt(stage.Path, target.WindowFrom, _ledger) is not { } plan) {
            return;
        }

        stage.Adopt(plan);
    }

    /// <summary>
    /// Moves the window to a file line, with the caret's place in the file
    /// carried across.
    /// </summary>
    /// <param name="stage">The document to move the window over.</param>
    /// <param name="fileLine">The line the window should begin on.</param>
    /// <param name="caretOffset">Where the caret stands, in file
    /// coordinates.</param>
    private void MoveWindow(IWindowStage stage, int fileLine, int caretOffset) {
        if (stage.Path is null) {
            return;
        }

        stage.Adopt(_window.Plan(stage.Path, fileLine, _ledger));
        stage.SetCaretQuietly(WindowCaret.ToWindow(_ledger, caretOffset, stage.Text.Length));
    }
}
