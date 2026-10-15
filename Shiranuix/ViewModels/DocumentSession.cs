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
/// the coordinator's business, and this class knows none of it. Where the
/// window stands in the file, and every walk that moves it, belongs to
/// <see cref="DocumentWindow"/>; the travel over the file belongs to
/// <see cref="WindowNavigator"/>, which reaches this class as its
/// <see cref="IWindowStage"/> — this puts its plans on the screen.
///
/// The counts and the line numbers belong to <see cref="DocumentCounts"/>,
/// which follows the text and hears when it moves.
/// </remarks>
public sealed partial class DocumentSession : ObservableObject, IWindowStage {
    /// <summary>
    /// How many paths the recent-files list keeps. Matches what the start
    /// screen has room for, so the list cannot outgrow the space it is given.
    /// </summary>
    private const int RecentFilesLimit = 10;

    private readonly IFileService _files;
    private readonly IRecentFilesStore _recentStore;
    private readonly IDocumentHistory _history;

    /// <summary>
    /// Where the window stands in the file and how it gets anywhere else.
    /// </summary>
    private readonly DocumentWindow _window;

    /// <summary>
    /// What the writer has changed about the file, in the file's own
    /// coordinates — the thing saving writes out.
    /// </summary>
    private readonly EditLedger _ledger;

    /// <summary>
    /// Every way the window travels over the file, reached through this
    /// class as its stage: the plans are the engine's, putting one on
    /// screen is this class's own.
    /// </summary>
    private readonly WindowNavigator _navigator;

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
    /// True while the window is being moved under the caret — by a slide or
    /// an undo's return trip — so the caret change that lands inside the
    /// move does not start the move again.
    /// </summary>
    private bool _sliding;

    private readonly DirtyTracker<string> _text = new(string.Empty);

    /// <summary>
    /// Creates a session over file I/O, the stored recent-files list, the
    /// document's own undo history, the window engine that walks its file,
    /// the moves standing against it, and the navigator that travels it.
    /// </summary>
    /// <param name="files">File I/O.</param>
    /// <param name="recentFiles">The stored recent-files list.</param>
    /// <param name="history">The document's undo and redo.</param>
    /// <param name="window">Where the window stands and how it moves.</param>
    /// <param name="ledger">The moves standing against the file.</param>
    /// <param name="navigator">How the window travels over the file.</param>
    public DocumentSession(
        IFileService files,
        IRecentFilesStore recentFiles,
        IDocumentHistory history,
        DocumentWindow window,
        EditLedger ledger,
        WindowNavigator navigator) {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _recentStore = recentFiles ?? throw new ArgumentNullException(nameof(recentFiles));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        _text.Changed += OnBufferMoved;

        // The counts' own changes come out under the same names, so the
        // view hears them through this class's doors.
        _counts.PropertyChanged += (_, e) => OnPropertyChanged(e);

        // A document begins as one window over nothing: the file it would
        // show has no lines, and nothing stands outside the window to count.
        _scan = new ScanResult(0, 0, 0, Shiranuix.Models.Newline.Detect(string.Empty));
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: false);

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
        _navigator.ReturnTo(this, target);

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

        _navigator.ReturnTo(this, target);

        if (!_history.TryRedo(Text, out var restored)) {
            return;
        }

        RestoreText(restored);
        SetCaretWithoutSliding(target.Caret);
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

    /// <summary>
    /// The document's counts and the gutter's run of numbers, worked out
    /// of the text beside the scan and the ledger they are read from.
    /// </summary>
    /// <remarks>
    /// A collaborator rather than this class's own arithmetic: the counts
    /// follow from the text and nothing else, and its changes are forwarded
    /// here so the view hears them through the doors it is bound to.
    /// </remarks>
    private readonly DocumentCounts _counts = new();

    /// <summary>Word count, refreshed on every edit.</summary>
    public int WordCount => _counts.WordCount;

    /// <summary>Character count, refreshed on every edit.</summary>
    public int CharacterCount => _counts.CharacterCount;

    /// <summary>Line count, refreshed on every edit.</summary>
    public int LineCount => _counts.LineCount;

    /// <summary>
    /// The line numbers for the gutter: one per line on screen, newline
    /// separated, each the line's number in the file rather than in the
    /// window — worked out in <see cref="DocumentCounts"/> beside the
    /// counts it shares its line arithmetic with.
    /// </summary>
    public string LineNumbersText => _counts.LineNumbersText;

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
    public int CaretLine => _counts.FirstLine + Caret.Line - 1;

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

        // Back to one window over nothing: no file behind it, no walk to
        // remember, nothing past the end to slide towards. The counts
        // refresh once the window and the scan say the same nothing, so
        // the gutter and the caret's line number the blank document.
        _scan = new ScanResult(0, 0, 0, Shiranuix.Models.Newline.Detect(string.Empty));
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: false);
        _text.Reset(string.Empty);
        AnnounceWindowEdges();

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
            // what memory holds opens as its first window of lines, with
            // the rest waiting behind the end of the window.
            scan = _files.Scan(path);
            read = _files.ReadLines(path, new LineAnchor(0, 0), DocumentWindow.Lines);
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
        _window.Begin(new LineAnchor(0, 0), 0, atEnd: read.More);
        AnnounceWindowEdges();

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
        // position after them slid: the window and its anchors, and the
        // windows the history's entries name, all move to where their
        // stretch of file now stands. The window keeps showing exactly what
        // it showed — only where it stands has changed.
        _window.Shift(_ledger);
        _history.Rebase(position => _ledger.ShiftAt(position).Characters);
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
    /// Tells the view what stands beyond the window now, so the edges can
    /// draw their cue over the right things.
    /// </summary>
    private void AnnounceWindowEdges() {
        OnPropertyChanged(nameof(HasLinesAbove));
        OnPropertyChanged(nameof(HasLinesBelow));
    }

    private void OnBufferMoved(object? sender, EventArgs e) {
        // The text moved, so a composition held against the old text has gone
        // with it — committed into the change or abandoned by it. Clearing
        // first leaves every listener below reading one consistent state.
        Composition = Composition.None;

        // The counts and the gutter's run of numbers come first, worked out
        // of the text that just landed: every notification below reads the
        // fresh ones, and the gutter's own goes out with the counts.
        RefreshCounts();

        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsEmpty));

        // Every move the history records or takes back arrives here with the
        // text, so the two doors the view asks open with it.
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));

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
            _navigator.SlideForward(this);
        } else if (value <= 0) {
            _navigator.SlideBack(this);
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
        // window swapped from the file's own text to the document's — the
        // arithmetic itself is the counts class's business.
        _counts.Refresh(_scan, _ledger, _window.FileLine, Text);
    }
}
