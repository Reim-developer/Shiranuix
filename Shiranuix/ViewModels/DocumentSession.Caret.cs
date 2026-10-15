using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;

namespace Shiranuix.ViewModels;

/// <summary>
/// The caret's own share of the open document: where it stands, what the
/// input method is composing around it, and when its place at an edge asks
/// the window to travel.
/// </summary>
/// <remarks>
/// The session is one class across a few files by concern, and this is the
/// caret's file: the text pipeline in the main file moves it under the
/// writer's edits, the window engine's slides land it wherever their trip
/// ends, and the arithmetic of its place lives here.
/// </remarks>
public sealed partial class DocumentSession {
    /// <summary>
    /// The caret's offset into the text, as the editor reports it.
    /// </summary>
    /// <remarks>
    /// The editor is the only thing that knows where the caret is; this holds
    /// what it is told, and the line and column are worked out from it. Nothing
    /// here ever moves the caret itself.
    /// </remarks>
    [ObservableProperty]
    private int _caretIndex;

    /// <summary>
    /// What the input method is composing at the caret, if anything.
    /// </summary>
    /// <remarks>
    /// Handed in by the view, which is where the input method lives. Cleared
    /// here the moment the text moves — the same rule the editor's own
    /// presenter follows — because a composition committed or abandoned is not
    /// part of anything anymore, and a stale one would count a character that
    /// no longer exists.
    /// </remarks>
    [ObservableProperty]
    private Composition _composition = Composition.None;

    /// <summary>
    /// True while the window is being moved under the caret — by a slide or
    /// an undo's return trip — so the caret change that lands inside the
    /// move does not start the move again.
    /// </summary>
    private bool _sliding;

    /// <summary>
    /// Where the caret is: which line and which column, counted from one.
    /// </summary>
    /// <remarks>
    /// Read against the page as it is drawn. A composing character counts —
    /// it is on screen and the caret is standing in it — and the cursor inside
    /// it decides the column, falling back to its end when the input method
    /// does not say where the cursor is.
    ///
    /// The place is worked out the first time anything asks after the caret,
    /// the text or the composition moved, and held after that: the view reads
    /// the line and the column together on every one of those moves, and each
    /// read alone walks from the top of the window's text — one walk answers
    /// the burst a single move causes, and the move after it starts again.
    /// </remarks>
    public TextPosition Caret => _caretPosition ??= ComputeCaret();

    /// <summary>
    /// The last place the caret was read at, or null when the next read has
    /// to work it out again.
    /// </summary>
    private TextPosition? _caretPosition;

    /// <summary>
    /// Works the caret's place out against the text and composition as they
    /// stand now.
    /// </summary>
    private TextPosition ComputeCaret() {
        var caret = Math.Clamp(CaretIndex, 0, Text.Length);

        if (Composition.IsEmpty) {
            return TextPosition.Of(Text, caret);
        }

        var page = Text.Insert(caret, Composition.Text);
        return TextPosition.Of(page, caret + Composition.Caret);
    }

    /// <summary>
    /// The line the caret is on, counted from one — its line in the file,
    /// not its line in the window, so a caret a thousand lines in reports
    /// the number the gutter shows beside it.
    /// </summary>
    public int CaretLine => _counts.FirstLine + Caret.Line - 1;

    /// <summary>
    /// The column the caret is on, counted from one.
    /// </summary>
    public int CaretColumn => Caret.Column;

    /// <summary>
    /// Lands the caret where it stands in the file, wherever the window now
    /// shows that place — without reading the new position as the writer
    /// asking for another slide.
    /// </summary>
    /// <param name="index">The caret's index in the window's text.</param>
    private void SetCaretWithoutSliding(int index) {
        _sliding = true;

        try {
            CaretIndex = Math.Clamp(index, 0, Text.Length);
        } finally {
            _sliding = false;
        }
    }

    partial void OnCaretIndexChanged(int value) {
        _caretPosition = null;

        OnPropertyChanged(nameof(Caret));
        OnPropertyChanged(nameof(CaretLine));
        OnPropertyChanged(nameof(CaretColumn));

        // The caret touching an edge of the window is the writer asking to
        // see what is past it — unless the window is already there, or this
        // caret change is the window moving itself.
        if (_sliding || Path is null || !Composition.IsEmpty) {
            return;
        }

        if (value >= Text.Length) {
            _navigator.SlideForward(this);
        } else if (value <= 0) {
            _navigator.SlideBack(this);
        }
    }

    partial void OnCompositionChanged(Composition value) {
        _caretPosition = null;

        OnPropertyChanged(nameof(Caret));
        OnPropertyChanged(nameof(CaretLine));
        OnPropertyChanged(nameof(CaretColumn));
    }
}
