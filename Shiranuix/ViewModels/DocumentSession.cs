using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;
using Shiranuix.Persistence;
using Shiranuix.Services;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The open document: its text, its path, and every rule about how one becomes
/// the other.
/// </summary>
/// <remarks>
/// This owns the document, not the window. It reads and writes through
/// <see cref="IFileService"/>, remembers paths through
/// <see cref="IRecentFilesStore"/>, and reports back
/// <see cref="DocumentOutcome"/> values rather than sentences — display text is
/// the coordinator's business, and this class knows none of it.
///
/// The counts and the line numbers live here too, because they follow from the
/// text and nothing else is better placed to know when the text moved.
/// </remarks>
public sealed partial class DocumentSession : ObservableObject {
    /// <summary>
    /// How many paths the recent-files list keeps. Matches what the start
    /// screen has room for, so the list cannot outgrow the space it is given.
    /// </summary>
    private const int RecentFilesLimit = 10;

    /// <summary>
    /// How many lines the window holds while the file carries on past it.
    /// </summary>
    private const int WindowLines = 50;

    /// <summary>
    /// How many lines a slide keeps between the caret and the edge it just
    /// crossed, so a writer moving through a long file always has context
    /// above or below where they are working.
    /// </summary>
    private const int ContextLines = 10;

    /// <summary>
    /// How many window starts are remembered for sliding back and for
    /// undo's return trips. Capped the way the history is, and never below
    /// the file's own start: every walk falls back on that one.
    /// </summary>
    private const int AnchorLimit = 1000;

    private readonly IFileService _files;
    private readonly IRecentFilesStore _recentStore;
    private readonly IDocumentHistory _history;

    /// <summary>
    /// What the writer has changed about the file, in the file's own
    /// coordinates — the thing saving writes out.
    /// </summary>
    private readonly EditLedger _ledger = new();

    /// <summary>
    /// The file the unmoved text comes from at save time: the one last read
    /// or written, or null while the document has no file behind it.
    /// </summary>
    private string? _sourcePath;

    /// <summary>
    /// True while the text is being put back by the history itself — an
    /// undo or a redo — rather than moved by the writer.
    /// </summary>
    /// <remarks>
    /// Every ordinary set of <see cref="Text"/> is the writer's move and is
    /// recorded in the history; a set under this flag is the history doing
    /// its own work, and recording it would echo the entry straight back
    /// onto the stack the undo had just taken it from. The ledger hears
    /// both: a text put back still has to be put back in the file.
    /// </remarks>
    private bool _restoringText;

    /// <summary>
    /// What a scan said of the whole file: the baseline every count is
    /// worked from, because the window only holds part of the text the
    /// counts describe.
    /// </summary>
    private ScanResult _scan;

    /// <summary>
    /// Where the window begins in the file, in the file's own coordinates —
    /// the ones the ledger keeps its moves in, so the two compare directly.
    /// </summary>
    private LineAnchor _windowStart;

    /// <summary>
    /// The file line the window starts on, counted from zero. The gutter's
    /// numbers begin one past it, plus whatever the moves before the window
    /// did to the lines above.
    /// </summary>
    private int _windowFileLine;

    /// <summary>
    /// True while the file carries on past the window's end — the
    /// difference between a window that can slide forward and one that is
    /// already showing the file's tail.
    /// </summary>
    private bool _atEnd;

    /// <summary>
    /// Every window start this document has stood at, with the file line it
    /// stood on, in the order they fall. An undo made in a window that has
    /// since slid away is put back by walking from the nearest of these:
    /// from the file's start, every time, would be the whole file.
    /// </summary>
    private readonly List<(LineAnchor At, int FileLine)> _anchors = new();

    /// <summary>
    /// True while the window is being moved under the caret — by a slide or
    /// an undo's return trip — so the caret change that lands inside the
    /// move does not start the move again.
    /// </summary>
    private bool _sliding;

    private readonly DirtyTracker<string> _text = new(string.Empty);

    /// <summary>
    /// Creates a session over file I/O, the stored recent-files list, and the
    /// document's own undo history.
    /// </summary>
    /// <param name="files">File I/O.</param>
    /// <param name="recentFiles">The stored recent-files list.</param>
    /// <param name="history">The document's undo and redo.</param>
    public DocumentSession(IFileService files, IRecentFilesStore recentFiles, IDocumentHistory history) {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _recentStore = recentFiles ?? throw new ArgumentNullException(nameof(recentFiles));
        _history = history ?? throw new ArgumentNullException(nameof(history));

        _text.Changed += OnBufferMoved;

        // A document begins as one window over nothing: the file it would
        // show has no lines, and nothing stands outside the window to count.
        _scan = new ScanResult(0, 0, 0, Shiranuix.Models.Newline.Detect(string.Empty));
        BeginWindow(new LineAnchor(0, 0), 0, atEnd: false);

        // The store hands back newest first, which is the order the start
        // screen shows.
        foreach (var path in _recentStore.Load()) {
            RecentFiles.Add(path);
        }

        RecentFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentFiles));

        RefreshCounts();
    }

    /// <summary>
    /// The document's full text. The single source of truth for its content.
    /// </summary>
    /// <remarks>
    /// A set through here is the writer's move — a keystroke, a paste, a
    /// deletion pushed through the binding — and goes into the history so it
    /// can be taken back, and into the ledger so saving can write it. A set
    /// the history makes itself — an undo landing, a redo — runs under
    /// <see cref="_restoringText"/> and reaches only the ledger: recording
    /// it in the history would put the entry straight back on the stack the
    /// undo had just taken it from, but the file still has to learn of it.
    /// </remarks>
    public string Text {
        get => _text.Value;
        set {
            if (!_restoringText) {
                // The window this move was made in, and where the caret
                // stood before it: both are what an undo needs to put the
                // document back the way it was. The window's bounds are the
                // ledger's — stable across every move the window holds, so a
                // run of typing still merges into one entry.
                _history.Note(
                    _text.Value,
                    value,
                    new UndoTarget(_ledger.WindowFrom, _ledger.WindowTo, CaretIndex));
            }

            _ledger.Note(_text.Value, value);
            _text.Value = value;
        }
    }

    /// <summary>
    /// Whether there is a move to take back.
    /// </summary>
    public bool CanUndo => _history.CanUndo;

    /// <summary>
    /// Whether a taken-back move is waiting to be reapplied.
    /// </summary>
    public bool CanRedo => _history.CanRedo;

    /// <summary>
    /// Takes the last move back, restoring the text it was made to, with
    /// the caret where the move began.
    /// </summary>
    public void Undo() {
        if (!_history.PeekUndo(out var target)) {
            return;
        }

        // The move's span means nothing against a window that has slid
        // away, so the window it was made in goes back on screen first and
        // the move comes off the text it was actually made against.
        ReturnWindow(target);

        if (!_history.TryUndo(Text, out var restored)) {
            return;
        }

        RestoreText(restored);
        SetCaretWithoutSliding(target.Caret);
    }

    /// <summary>
    /// Reapplies the last move taken back, with the caret after what the
    /// move put back.
    /// </summary>
    public void Redo() {
        if (!_history.PeekRedo(out var target)) {
            return;
        }

        ReturnWindow(target);

        if (!_history.TryRedo(Text, out var restored)) {
            return;
        }

        RestoreText(restored);
        SetCaretWithoutSliding(target.Caret);
    }

    /// <summary>
    /// Puts the window back over the stretch a history entry was made in,
    /// when the document is not already showing it.
    /// </summary>
    /// <param name="target">The entry's window.</param>
    /// <remarks>
    /// The window's start is a line of the file, and the anchors remember
    /// where past windows stood: the nearest one at or before the entry's
    /// window is walked forward from, a handful of lines at most. What comes
    /// back is read fresh, moves laid over it the same as always — if the
    /// file no longer has the stretch the entry names, the document stays
    /// where it is rather than show the wrong text.
    /// </remarks>
    private void ReturnWindow(UndoTarget target) {
        if (_ledger.WindowFrom == target.WindowFrom && _ledger.WindowTo == target.WindowTo) {
            // The window is already where the move was made.
            return;
        }

        if (Path is null) {
            return;
        }

        var anchor = AnchorAtOrBefore(target.WindowFrom);
        var walk = _files.LineStartOf(Path, anchor.At, target.WindowFrom);
        var read = _files.ReadLines(Path, walk.At, WindowLines);

        if (walk.At.Offset != target.WindowFrom) {
            return;
        }

        ApplyWindow(walk.At, anchor.FileLine + walk.Lines, read.Text, read.More);
    }

    /// <summary>
    /// Puts a text the history produced into the document without recording
    /// the placement as a move of its own.
    /// </summary>
    /// <param name="text">The text to restore.</param>
    private void RestoreText(string text) {
        _restoringText = true;
        try {
            Text = text;
        } finally {
            _restoringText = false;
        }
    }

    /// <summary>
    /// True when the document differs from the last thing read or written:
    /// the moves stand against the file, or its line-break convention has.
    /// A convention change alone would write a different file, so it counts.
    /// </summary>
    /// <remarks>
    /// The ledger is the whole question rather than the text beside it: the
    /// window's text moves when the window slides, and a window slid back
    /// over an edited stretch shows the moves again even though the text
    /// never went through the writer's hands. An empty ledger means the file
    /// and the document agree, however many moves it took to get there.
    /// </remarks>
    public bool IsDirty => !_ledger.IsEmpty || Newline != _savedNewline;

    /// <summary>
    /// True while the buffer holds no characters at all.
    /// </summary>
    /// <remarks>
    /// Only <see cref="string.IsNullOrEmpty"/> counts: a document holding a
    /// single space is something the user typed.
    /// </remarks>
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    /// <summary>
    /// The path of the file backing this document, or null for an unsaved one.
    /// </summary>
    [ObservableProperty]
    private string? _path;

    /// <summary>
    /// The file name, or "Untitled" when the document has never been saved.
    /// </summary>
    public string Name => Path is null ? "Untitled" : System.IO.Path.GetFileName(Path);

    /// <summary>The recent-files list shown on the start screen.</summary>
    public RecentList<string> RecentFiles { get; } = new(RecentFilesLimit);

    /// <summary>
    /// True while <see cref="RecentFiles"/> holds anything.
    /// </summary>
    public bool HasRecentFiles => RecentFiles.Count > 0;

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
    public string LineNumbersText {
        get {
            var first = FirstLineNumber;
            var lines = Math.Max(TextStatistics.Measure(Text).Lines, 1);

            return string.Join('\n', Enumerable.Range(first, lines));
        }
    }

    /// <summary>
    /// The number the window's first line carries: its place in the file
    /// from the walk that brought the window there, plus every line the
    /// moves before the window added or took out above it.
    /// </summary>
    private int FirstLineNumber {
        get {
            var lines = _windowFileLine;

            foreach (var span in _ledger.SpansBefore(_ledger.WindowFrom)) {
                lines += Newlines(TextStatistics.Measure(span.Text))
                    - Newlines(TextStatistics.Measure(span.Original));
            }

            return lines + 1;
        }
    }

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
    /// Where the caret is: which line and which column, counted from one.
    /// </summary>
    /// <remarks>
    /// Read against the page as it is drawn. A composing character counts —
    /// it is on screen and the caret is standing in it — and the cursor inside
    /// it decides the column, falling back to its end when the input method
    /// does not say where the cursor is.
    /// </remarks>
    public TextPosition Caret {
        get {
            var caret = Math.Clamp(CaretIndex, 0, Text.Length);

            if (Composition.IsEmpty) {
                return TextPosition.Of(Text, caret);
            }

            var page = Text.Insert(caret, Composition.Text);
            return TextPosition.Of(page, caret + Composition.Caret);
        }
    }

    /// <summary>
    /// The line the caret is on, counted from one — its line in the file,
    /// not its line in the window, so a caret a thousand lines in reports
    /// the number the gutter shows beside it.
    /// </summary>
    public int CaretLine => FirstLineNumber + Caret.Line - 1;

    /// <summary>
    /// The column the caret is on, counted from one.
    /// </summary>
    public int CaretColumn => Caret.Column;

    /// <summary>
    /// How this document's file is written: UTF-8, which is what
    /// <see cref="IFileService"/> reads and writes.
    /// </summary>
    public string Encoding => "UTF-8";

    /// <summary>
    /// The line-break convention this document's file is written in. A new
    /// document starts on the operating system's own convention; an opened
    /// file keeps the one it arrived in; the status bar's segment changes it.
    /// </summary>
    [ObservableProperty]
    private Newline _newline = Newline.Default;

    /// <summary>
    /// The convention the file was last read or written in — the point dirty
    /// is measured against, the way the text has its own saved point.
    /// </summary>
    private Newline _savedNewline = Newline.Default;

    /// <summary>
    /// Replaces the document with a blank one, clean and unsaved, on the
    /// operating system's line-break convention.
    /// </summary>
    public void New() {
        Path = null;

        // A blank document is a document rebegun: the moves of the old one
        // would restore a text this document has no claim to, and the
        // ledger's would spell a file it has nothing to do with.
        _history.Clear();
        _ledger.Clear();
        _ledger.ResetWindow(0, string.Empty);
        _sourcePath = null;
        _text.Reset(string.Empty);

        // Back to one window over nothing: no file behind it, no walk to
        // remember, nothing past the end to slide towards.
        _scan = new ScanResult(0, 0, 0, Shiranuix.Models.Newline.Detect(string.Empty));
        BeginWindow(new LineAnchor(0, 0), 0, atEnd: false);

        // A blank document is this machine's blank document — the operating
        // system's own convention — not whatever the last file arrived in.
        Newline = Shiranuix.Models.Newline.Default;
        _savedNewline = Newline;
    }

    /// <summary>
    /// Reads a file into the document. A failed read changes nothing.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    public DocumentOutcome Open(string path) {
        ScanResult scan;
        LineRead read;
        try {
            // The counts and the convention come off the stream, and the
            // window follows: the file is never held whole — a file past
            // what memory holds opens as its first fifty lines, with the
            // rest waiting behind the end of the window.
            scan = _files.Scan(path);
            read = _files.ReadLines(path, new LineAnchor(0, 0), WindowLines);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        // The file's own text arriving is the same rebegun as a new
        // document — not a move the writer made — so the old moves go, the
        // ledger takes this window as its zero, and the replacement is not
        // recorded as a move against anything.
        _history.Clear();
        _ledger.Clear();
        _ledger.ResetWindow(0, read.Text);
        _sourcePath = path;

        // Window and counts first, text second: the text's own notification
        // asks for both.
        _scan = scan;
        BeginWindow(new LineAnchor(0, 0), 0, atEnd: read.More);

        _text.Value = read.Text;
        Path = path;
        _text.MarkSaved();
        SetCaretWithoutSliding(CaretIndex);

        // The file's own convention is kept, so saving writes the breaks back
        // the way they arrived rather than rewriting them — and it becomes
        // the clean point, like the text beside it.
        Newline = scan.Newline;
        _savedNewline = Newline;
        Remember();

        return DocumentOutcome.Done;
    }

    /// <summary>
    /// Writes the buffer to a path, which becomes the document's file.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// Only after a successful write does the path become the document's, so a
    /// failed save does not silently re-point the document at a file it never
    /// reached.
    /// </remarks>
    public DocumentOutcome Save(string path) {
        ScanResult written;
        try {
            // The file as it stands, with every move since it was read laid
            // over the spans those moves fall on: the ledger says what
            // changed, the file service streams the rest. What comes back
            // is what the file now says — the whole file's counts, however
            // little of it was on screen.
            written = _files.WriteCombined(path, _sourcePath, _ledger.Build(), Newline);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        Path = path;
        _sourcePath = path;

        // The moves have collapsed into the file that was written, so every
        // position after them slid: the window, the anchors an undo returns
        // through, and the windows the history's entries name all move to
        // where their stretch of file now stands. The window keeps showing
        // exactly what it showed — only where it stands has changed.
        var window = ShiftOf(_ledger.WindowFrom);

        _windowStart = new LineAnchor(
            _windowStart.Byte + window.Bytes,
            _windowStart.Offset + window.Characters);

        for (var index = 0; index < _anchors.Count; index++) {
            var anchor = _anchors[index];
            var shift = ShiftOf(anchor.At.Offset);

            _anchors[index] = (
                new LineAnchor(anchor.At.Byte + shift.Bytes, anchor.At.Offset + shift.Characters),
                anchor.FileLine + shift.Lines);
        }

        _history.Rebase(position => ShiftOf(position).Characters);
        _windowFileLine += window.Lines;
        _scan = written;

        // What was written now says exactly what the window says, so the
        // moves behind it are the new zero. The history keeps going: a save
        // is a point the text passed through, not a new document.
        _ledger.RebaseToSaved(Text);

        _text.MarkSaved();
        _savedNewline = Newline;
        Remember();

        return DocumentOutcome.Done;
    }

    /// <summary>
    /// Opens a file without asking whether to discard anything.
    /// </summary>
    /// <param name="path">The file to read.</param>
    public DocumentOutcome OpenFromCommandLine(string path) => Open(path);

    private void Remember() {
        if (Path is null) {
            return;
        }

        RecentFiles.MoveToFront(Path);

        // The store keeps its own copy, capped to the same number, so the list
        // outlives the session that built it.
        _recentStore.Remember(Path);
    }

    /// <summary>
    /// Points the document at a window with no history against it: the
    /// ledger has already been told where the window stands.
    /// </summary>
    /// <param name="start">Where the window begins in the file.</param>
    /// <param name="fileLine">The file line it begins on, from zero.</param>
    /// <param name="atEnd">Whether the file carries on past the window.</param>
    private void BeginWindow(LineAnchor start, int fileLine, bool atEnd) {
        _windowStart = start;
        _windowFileLine = fileLine;
        _atEnd = atEnd;

        _anchors.Clear();
        _anchors.Add((start, fileLine));
    }

    /// <summary>
    /// Puts a freshly read window on screen: the file's own text with every
    /// move standing in the window laid back over it, because the writer's
    /// moves do not reach the file until saving.
    /// </summary>
    /// <param name="start">Where the window begins in the file.</param>
    /// <param name="fileLine">The file line it begins on, from zero.</param>
    /// <param name="original">The file's own text over the window.</param>
    /// <param name="atEnd">Whether the file carries on past the window.</param>
    private void ApplyWindow(LineAnchor start, int fileLine, string original, bool atEnd) {
        _windowStart = start;
        _windowFileLine = fileLine;
        _atEnd = atEnd;

        _ledger.ResetWindow(start.Offset, original);
        AddAnchor(start, fileLine);

        // Bypassing the setter keeps the window's own repositioning out of
        // both doors: a slide is not a move the writer made, and moving the
        // window twice for one keystroke would be one undo too many.
        _text.Value = _ledger.Materialize();
        SetCaretWithoutSliding(CaretIndex);
    }

    /// <summary>
    /// Moves the window to a file line, with the caret's place in the file
    /// carried across.
    /// </summary>
    /// <param name="fileLine">The line the window should begin on.</param>
    /// <param name="caretOffset">Where the caret stands, in file
    /// coordinates.</param>
    /// <remarks>
    /// Both edges step out over any move they would otherwise cut: a window
    /// holding half a move cannot show it whole, and the writer's moves are
    /// never cut in half. That is what lets the window grow past its fifty
    /// lines — or shrink its start below the line asked for — and it is why
    /// every entry stays whole however many windows pass over it.
    /// </remarks>
    private void MoveWindow(int fileLine, int caretOffset) {
        if (Path is null) {
            return;
        }

        _sliding = true;

        try {
            var anchor = AnchorAtOrBeforeLine(fileLine);
            var walk = _files.MoveLines(Path, anchor.At, fileLine - anchor.FileLine);
            var start = walk.At;
            var startLine = anchor.FileLine + walk.Lines;

            // The front edge steps back to the line any cut move begins on.
            while (_ledger.CutAt(start.Offset) is { } cut) {
                var snap = _files.LineStartOf(Path, start, cut.Start);

                if (snap.At.Offset >= start.Offset) {
                    break;
                }

                startLine += snap.Lines;
                start = snap.At;
            }

            var lines = WindowLines;
            var read = _files.ReadLines(Path, start, lines);

            // The trailing edge steps forward to the line any cut move ends
            // on — a paste spanning the edge widens the window to hold it.
            while (_ledger.CutAt(read.End.At.Offset - 1) is { } beyond) {
                var extend = _files.LineStartOf(
                    Path, read.End.At, beyond.Start + beyond.Original.Length);
                lines += extend.Lines + 1;

                if (lines <= 0) {
                    break;
                }

                read = _files.ReadLines(Path, start, lines);
            }

            ApplyWindow(start, startLine, read.Text, read.More);
            SetCaretWithoutSliding(CaretInWindow(caretOffset));
        } finally {
            _sliding = false;
        }
    }

    /// <summary>
    /// Takes the window forward: the caret stands on the window's last
    /// line, and ten lines of it stay above the edge the caret is about to
    /// cross, so the writer keeps their context as they go.
    /// </summary>
    private void SlideForward() {
        if (!_atEnd) {
            // The window already reaches the file's end.
            return;
        }

        var caretLine = _windowFileLine + TextStatistics.Lines(Text) - 1;
        var target = Math.Max(caretLine - ContextLines, _windowFileLine + 1);

        MoveWindow(target, _ledger.WindowFrom + _ledger.WindowOriginal.Length);
    }

    /// <summary>
    /// Takes the window back: ten lines of it stay below the caret it came
    /// from, the mirror of a slide forward.
    /// </summary>
    private void SlideBack() {
        if (_windowStart.Offset == 0) {
            // The window already begins the file.
            return;
        }

        var target = _windowFileLine + ContextLines - WindowLines;

        if (target < 0) {
            target = 0;
        }

        if (target >= _windowFileLine) {
            target = _windowFileLine - 1;
        }

        MoveWindow(target, _ledger.WindowFrom);
    }

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

    /// <summary>
    /// Translates where the caret stood in the old window into where the
    /// same place stands in the window now on screen: file position for
    /// file position, plus whatever the moves in between changed.
    /// </summary>
    /// <param name="fileOffset">The caret's position in the file.</param>
    /// <returns>The caret's index in the window's text, clamped into it.</returns>
    private int CaretInWindow(int fileOffset) {
        var local = fileOffset - _ledger.WindowFrom;

        foreach (var span in _ledger.Spans()) {
            if (span.Start >= _ledger.WindowFrom && span.Start < fileOffset) {
                local += span.Text.Length - span.Original.Length;
            }
        }

        return Math.Clamp(local, 0, Text.Length);
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

    /// <summary>
    /// How far a file position moved when every move before it collapsed
    /// into the saved file: in characters, in bytes for the anchors, and in
    /// lines for the gutter.
    /// </summary>
    /// <param name="position">The position, in the file as it was read.</param>
    /// <returns>The shift.</returns>
    private (int Characters, int Bytes, int Lines) ShiftOf(int position) {
        var characters = 0;
        var bytes = 0;
        var lines = 0;

        foreach (var span in _ledger.SpansBefore(position)) {
            characters += span.Text.Length - span.Original.Length;
            bytes += System.Text.Encoding.UTF8.GetByteCount(span.Text)
                - System.Text.Encoding.UTF8.GetByteCount(span.Original);
            lines += Newlines(TextStatistics.Measure(span.Text))
                - Newlines(TextStatistics.Measure(span.Original));
        }

        return (characters, bytes, lines);
    }

    private void OnBufferMoved(object? sender, EventArgs e) {
        // The text moved, so a composition held against the old text has gone
        // with it — committed into the change or abandoned by it. Clearing
        // first leaves every listener below reading one consistent state.
        Composition = Composition.None;

        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(LineNumbersText));

        // Every move the history records or takes back arrives here with the
        // text, so the two doors the view asks open with it.
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        RefreshCounts();

        // The caret follows the text: an edit that removes a newline moves the
        // caret's line even if nothing else touched it.
        OnPropertyChanged(nameof(CaretLine));
        OnPropertyChanged(nameof(CaretColumn));
    }

    partial void OnCaretIndexChanged(int value) {
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
            SlideForward();
        } else if (value <= 0) {
            SlideBack();
        }
    }

    partial void OnNewlineChanged(Newline value) =>
        OnPropertyChanged(nameof(IsDirty));

    partial void OnCompositionChanged(Composition value) {
        OnPropertyChanged(nameof(Caret));
        OnPropertyChanged(nameof(CaretLine));
        OnPropertyChanged(nameof(CaretColumn));
    }

    private void RefreshCounts() {
        // The whole file's counts from the scan that read it, with the
        // window swapped from the file's own text to the document's — and
        // every move beyond the window's edges added over the top: its text
        // is not on screen, but it is still part of the file.
        var text = TextStatistics.Measure(Text);
        var window = TextStatistics.Measure(_ledger.WindowOriginal);

        var words = _scan.Words - window.Words + text.Words;
        var characters = _scan.Characters - window.Characters + text.Characters;
        var newlines = Newlines(_scan.Lines) - Newlines(window) + Newlines(text);

        foreach (var span in _ledger.SpansOutside()) {
            var outsideText = TextStatistics.Measure(span.Text);
            var outsideOriginal = TextStatistics.Measure(span.Original);

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

        OnPropertyChanged(nameof(LineNumbersText));
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
