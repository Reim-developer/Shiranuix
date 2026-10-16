using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The properties the view binds straight onto the coordinator: one-line
/// doors into the state that owns each rule.
/// </summary>
/// <remarks>
/// Avalonia's compiled bindings take their property name from the path's
/// first segment — the object the view is bound to, which is this class. A
/// nested path neither writes nor refreshes: <c>{Document.Text,
/// Mode=TwoWay}</c> never reached the setter, and <c>{Document.IsEmpty}</c>
/// never re-evaluated, both of which showed up as the start screen refusing
/// to disappear on the first keystroke.
///
/// So the view model is the surface the view binds. Every member in this
/// file is a one-line door with no logic of its own — the rules stay where
/// they were written, in <see cref="DocumentSession"/> and
/// <see cref="TypographyState"/>, and the constructor's wiring raises their
/// changes under these names, so the doors stay live.
/// </remarks>
public partial class EditorViewModel {
    /// <summary>
    /// The one box a command's name can be typed into: the door every command
    /// has, whether it belongs to the editor or to a plugin nobody told the
    /// writer about.
    /// </summary>
    public PaletteState Palette { get; }

    /// <summary>The document's full text. The single source of truth for its content.</summary>
    public string Text {
        get => Document.Text;
        set => Document.Text = value;
    }

    /// <summary>Whether the document has a move to take back.</summary>
    public bool CanUndo => Document.CanUndo;

    /// <summary>Whether a taken-back move is waiting to be reapplied.</summary>
    public bool CanRedo => Document.CanRedo;

    /// <summary>
    /// Takes the last move back — Ctrl+Z, which the window's key handler
    /// calls because the text box's own undo is the one that dies when the
    /// text is replaced from outside.
    /// </summary>
    public void Undo() => Document.Undo();

    /// <summary>
    /// Reapplies the last move taken back — Ctrl+Y and Ctrl+Shift+Z.
    /// </summary>
    public void Redo() => Document.Redo();

    /// <summary>
    /// Slides the window over the file by a number of lines — the window's
    /// key handler calls it for the wheel, the page keys and the
    /// whole-file jumps, each of which knows how far the writer asked.
    /// </summary>
    /// <param name="lines">How far to move: forward is positive, back is
    /// negative.</param>
    public void SlideBy(int lines) => Document.SlideBy(lines);

    /// <summary>
    /// Takes the window to the file's first stretch — Ctrl+Home.
    /// </summary>
    public void SlideToStart() => Document.SlideToStart();

    /// <summary>
    /// Takes the window to the file's last stretch — Ctrl+End.
    /// </summary>
    public void SlideToEnd() => Document.SlideToEnd();

    /// <summary>How many lines a page key turns.</summary>
    public int PageLines => DocumentWindow.PageLines;

    /// <summary>True while the file carries lines above the window.</summary>
    public bool HasLinesAbove => Document.HasLinesAbove;

    /// <summary>True while the file carries lines past the window.</summary>
    public bool HasLinesBelow => Document.HasLinesBelow;

    /// <summary>Whether lines wrap within the page's width, or run past it.</summary>
    public bool WordWrap {
        get => Typography.WordWrap;
        set => Typography.WordWrap = value;
    }

    /// <summary>The typeface the document is set in.</summary>
    public string FontFamily {
        get => Typography.FontFamily;
        set => Typography.FontFamily = value;
    }

    /// <summary>The point size the document is set in.</summary>
    public double FontSize {
        get => Typography.FontSize;
        set => Typography.FontSize = value;
    }

    /// <summary>The point sizes offered by the size picker.</summary>
    public IReadOnlyList<double> FontSizes => Typography.FontSizes;

    /// <summary>The typefaces offered by the font picker.</summary>
    public IReadOnlyList<string> AvailableFonts => Typography.AvailableFonts;

    /// <summary>The line height the page is set in.</summary>
    public double EditorLineHeight => Typography.EditorLineHeight;

    /// <summary>The recent-files list shown on the start screen.</summary>
    public RecentList<string> RecentFiles => Document.RecentFiles;

    /// <summary>True while <see cref="RecentFiles"/> holds anything.</summary>
    public bool HasRecentFiles => Document.HasRecentFiles;

    /// <summary>The path of the file backing this document, or null for an unsaved one.</summary>
    public string? Path => Document.Path;

    /// <summary>The file name, or "Untitled" when the document has never been saved.</summary>
    public string Name => Document.Name;

    /// <summary>Word count, refreshed on every edit.</summary>
    public int WordCount => Document.WordCount;

    /// <summary>Character count, refreshed on every edit.</summary>
    public int CharacterCount => Document.CharacterCount;

    /// <summary>Line count, refreshed on every edit.</summary>
    public int LineCount => Document.LineCount;

    /// <summary>The line numbers for the gutter: one per line, newline separated.</summary>
    public string LineNumbersText => Document.LineNumbersText;

    /// <summary>The caret's offset into the text, as the editor reports it.</summary>
    public int CaretIndex {
        get => Document.CaretIndex;
        set => Document.CaretIndex = value;
    }

    /// <summary>The line the caret is on, counted from one.</summary>
    public int CaretLine => Document.CaretLine;

    /// <summary>The column the caret is on, counted from one.</summary>
    public int CaretColumn => Document.CaretColumn;

    /// <summary>
    /// What the input method is composing at the caret, as the view reports it.
    /// </summary>
    /// <remarks>
    /// Written by the view only — the view model never composes anything — and
    /// held by the document, where the caret is worked out from it.
    /// </remarks>
    public Composition Composition {
        get => Document.Composition;
        set => Document.Composition = value;
    }

    /// <summary>How this document's file is written.</summary>
    public string Encoding => Document.Encoding;

    /// <summary>The line-break convention this document's file is written in.</summary>
    public string Newline => Document.Newline.Name;

    /// <summary>
    /// True while the document's line breaks are a bare line feed — the
    /// choice the status bar's LF stands for.
    /// </summary>
    public bool NewlineIsLf => Document.Newline == Shiranuix.Models.Newline.Lf;

    /// <summary>
    /// True while the document's line breaks are carriage return then line
    /// feed — the choice the status bar's CRLF stands for.
    /// </summary>
    public bool NewlineIsCrLf => Document.Newline == Shiranuix.Models.Newline.CrLf;
}
