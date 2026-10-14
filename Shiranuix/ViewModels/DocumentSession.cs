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

    private readonly IFileService _files;
    private readonly IRecentFilesStore _recentStore;
    private readonly DirtyTracker<string> _text = new(string.Empty);

    /// <summary>
    /// Creates a session over file I/O and the stored recent-files list.
    /// </summary>
    /// <param name="files">File I/O.</param>
    /// <param name="recentFiles">The stored recent-files list.</param>
    public DocumentSession(IFileService files, IRecentFilesStore recentFiles) {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _recentStore = recentFiles ?? throw new ArgumentNullException(nameof(recentFiles));

        _text.Changed += OnBufferMoved;

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
    public string Text {
        get => _text.Value;
        set => _text.Value = value;
    }

    /// <summary>
    /// True when the buffer differs from the last thing read or written.
    /// </summary>
    public bool IsDirty => _text.IsDirty;

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
    /// The line numbers for the gutter: one per line, newline separated.
    /// </summary>
    /// <remarks>
    /// A single string rather than one control per line — the same reason the
    /// text is one control. An empty document still numbers its first line,
    /// because the cursor is on it whether anything has been typed or not.
    /// </remarks>
    public string LineNumbersText => string.Join('\n', Enumerable.Range(1, Math.Max(LineCount, 1)));

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
    /// The line the caret is on, counted from one.
    /// </summary>
    public int CaretLine => Caret.Line;

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
    /// The line-break convention this document's file is written in.
    /// </summary>
    [ObservableProperty]
    private Newline _newline = Newline.Lf;

    /// <summary>
    /// Replaces the document with a blank one, clean and unsaved.
    /// </summary>
    public void New() {
        Path = null;
        _text.Reset(string.Empty);
    }

    /// <summary>
    /// Reads a file into the document. A failed read changes nothing.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    public DocumentOutcome Open(string path) {
        string text;
        try {
            text = _files.ReadAllText(path);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        Text = text;
        Path = path;
        _text.MarkSaved();

        // The file's own convention is kept, so saving writes the breaks back
        // the way they arrived rather than rewriting them.
        Newline = Shiranuix.Models.Newline.Detect(text);
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
        try {
            _files.WriteAllText(path, Newline.Apply(Text));
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        Path = path;
        _text.MarkSaved();
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

    private void OnBufferMoved(object? sender, EventArgs e) {
        // The text moved, so a composition held against the old text has gone
        // with it — committed into the change or abandoned by it. Clearing
        // first leaves every listener below reading one consistent state.
        Composition = Composition.None;

        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(LineNumbersText));
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
    }

    partial void OnCompositionChanged(Composition value) {
        OnPropertyChanged(nameof(Caret));
        OnPropertyChanged(nameof(CaretLine));
        OnPropertyChanged(nameof(CaretColumn));
    }

    private void RefreshCounts() {
        var stats = TextStatistics.Measure(Text);
        WordCount = stats.Words;
        CharacterCount = stats.Characters;
        LineCount = stats.Lines;
        OnPropertyChanged(nameof(LineNumbersText));
    }
}
