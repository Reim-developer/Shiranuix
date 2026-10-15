using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The window engine's own share of the open document: putting a planned
/// window on screen, travelling by the writer's asking, and telling the view
/// what stands beyond the window's edges.
/// </summary>
/// <remarks>
/// The session is one class across a few files by concern, and this is the
/// window's file. Where the window stands, and every walk that moves it,
/// belongs to <see cref="DocumentWindow"/> and <see cref="WindowNavigator"/>;
/// this file is only the stage side — the plans reaching the screen.
/// </remarks>
public sealed partial class DocumentSession {
    /// <summary>
    /// Puts a freshly planned window on screen: the file's own text with
    /// every move standing in the window laid back over it, because the
    /// writer's moves do not reach the file until saving.
    /// </summary>
    /// <param name="plan">The window the engine worked out.</param>
    private void ApplyWindow(WindowPlan plan) {
        _window.Adopt(plan);
        _ledger.ResetWindow(plan.Start.Offset, plan.Original);

        // Bypassing the setter keeps the window's own repositioning out of
        // both doors: a slide is not a move the writer made, and moving the
        // window twice for one keystroke would be one undo too many.
        _text.Value = _ledger.Materialize();
        SetCaretWithoutSliding(CaretIndex);
        AnnounceWindowEdges();
    }

    /// <summary>
    /// True while the file carries lines above the window — the news the
    /// view's top edge draws its cue from.
    /// </summary>
    public bool HasLinesAbove => _navigator.HasLinesAbove;

    /// <summary>
    /// True while the file carries lines past the window — the same news
    /// at the bottom edge.
    /// </summary>
    public bool HasLinesBelow => _navigator.HasLinesBelow;

    /// <summary>
    /// Slides the window by a number of lines — the writer asking to see
    /// elsewhere in the file with the wheel, a page key or a gesture —
    /// with the caret's own place in the file carried across and clamped
    /// into whatever is shown afterwards.
    /// </summary>
    /// <param name="lines">How far to move: forward is positive, back is
    /// negative.</param>
    public void SlideBy(int lines) => _navigator.SlideBy(this, lines);

    /// <summary>
    /// Takes the window back to the file's own beginning — Ctrl+Home's
    /// whole-file jump, from whatever corner of the file is on screen.
    /// </summary>
    public void SlideToStart() => _navigator.SlideToStart(this);

    /// <summary>
    /// Takes the window to the file's last stretch — Ctrl+End's whole-file
    /// jump — read straight off the scan's line count rather than slid to
    /// one window at a time.
    /// </summary>
    public void SlideToEnd() => _navigator.SlideToEnd(this);

    /// <summary>
    /// The document as <see cref="WindowNavigator"/> reaches it: the scan
    /// behind the window, the move that puts a plan on screen, and the
    /// caret landing without a slide.
    /// </summary>
    ScanResult IWindowStage.Scan => _scan;

    void IWindowStage.Adopt(WindowPlan plan) => ApplyWindow(plan);

    void IWindowStage.SetCaretQuietly(int index) => SetCaretWithoutSliding(index);

    /// <summary>
    /// Tells the view what stands beyond the window now, so the edges can
    /// draw their cue over the right things.
    /// </summary>
    private void AnnounceWindowEdges() {
        OnPropertyChanged(nameof(HasLinesAbove));
        OnPropertyChanged(nameof(HasLinesBelow));
    }
}
