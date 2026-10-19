using Shiranuix.Models;
using Shiranuix.Persistence;
using Shiranuix.Services;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The document's file: which file the text came from, where its clean
/// point stands, which files were opened lately, and the whole story of how
/// one becomes the document — New, Open and Save.
/// </summary>
/// <remarks>
/// The file side of the document, beside the raw I/O
/// <see cref="IFileService"/> is: this decides what a file arriving means
/// for the document and asks it to adopt the outcome through
/// <see cref="IFileStage"/>, the way <see cref="WindowNavigator"/> plans a
/// window and asks its stage to put it on screen. The stage keeps the
/// ordering of a rebegun or a save to one conversation; this class keeps
/// the state a file has and the document does not — the path behind the
/// unmoved text, the convention it was last written in, and the list of
/// files worth reaching for again.
/// </remarks>
public sealed class DocumentFile {
    /// <summary>
    /// How many paths the recent-files list keeps. Matches what the start
    /// screen has room for, so the list cannot outgrow the space it is given.
    /// </summary>
    private const int RecentFilesLimit = 10;

    private readonly IFileService _files;
    private readonly IRecentFilesStore _recentStore;

    /// <summary>
    /// Creates the file side over file I/O and the stored recent-files
    /// list, loading the list back into memory.
    /// </summary>
    /// <param name="files">File I/O.</param>
    /// <param name="recentFiles">The stored recent-files list.</param>
    public DocumentFile(IFileService files, IRecentFilesStore recentFiles) {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _recentStore = recentFiles ?? throw new ArgumentNullException(nameof(recentFiles));

        // The store hands back newest first, which is the order the start
        // screen shows.
        foreach (var path in _recentStore.Load()) {
            RecentFiles.Add(path);
        }
    }

    /// <summary>
    /// The file the unmoved text comes from at save time: the one last read
    /// or written, or null while the document has no file behind it.
    /// </summary>
    public string? SourcePath { get; private set; }

    /// <summary>
    /// The convention the file was last read or written in — the point dirty
    /// is measured against, the way the text has its own saved point.
    /// </summary>
    public Newline SavedNewline { get; private set; } = Newline.Default;

    /// <summary>The recent-files list shown on the start screen.</summary>
    public RecentList<string> RecentFiles { get; } = new(RecentFilesLimit);

    /// <summary>
    /// Replaces the document with a blank one, clean and unsaved, on the
    /// operating system's line-break convention.
    /// </summary>
    /// <param name="stage">The document to blank.</param>
    /// <remarks>
    /// A blank document is not a recent file: nothing was reached for, and
    /// the list is a list of files.
    /// </remarks>
    public void New(IFileStage stage) {
        SourcePath = null;
        stage.AdoptBlank(BlankScan(), Newline.Default);
        SavedNewline = Newline.Default;
    }

    /// <summary>
    /// Reads a file into the document. A failed read changes nothing.
    /// </summary>
    /// <param name="stage">The document to read into.</param>
    /// <param name="path">The file to read.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// This holds its thread from the first byte to the last, which is right
    /// for a caller already off the drawing thread and wrong for one sitting
    /// on it — that caller wants <see cref="OpenAsync"/>, which reads
    /// elsewhere and hands the document back only once the file has landed.
    /// </remarks>
    public DocumentOutcome Open(IFileStage stage, string path) {
        ScanResult scan;
        LineRead read;
        try {
            (scan, read) = ReadWindow(path);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        AdoptRead(stage, path, scan, read);
        return DocumentOutcome.Done;
    }

    /// <summary>
    /// Reads a file into the document without holding the thread that asked,
    /// so a large file arrives while the window keeps drawing.
    /// </summary>
    /// <param name="stage">The document to read into.</param>
    /// <param name="path">The file to read.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// Only the reading moves off-thread, because it is bytes and counts and
    /// touches nothing anyone is watching. The adoption comes back to the
    /// thread that owns the document: every notification it fires belongs to
    /// the bindings listening there.
    /// </remarks>
    public async Task<DocumentOutcome> OpenAsync(IFileStage stage, string path) {
        ScanResult scan;
        LineRead read;
        try {
            (scan, read) = await Task.Run(() => ReadWindow(path));
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        AdoptRead(stage, path, scan, read);
        return DocumentOutcome.Done;
    }

    /// <summary>
    /// Everything about a file that can be learned without touching the
    /// document: its counts, its convention, and the lines the window shows.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The file as far as the window reaches.</returns>
    private (ScanResult Scan, LineRead Read) ReadWindow(string path) {
        // The counts and the convention come off the stream, and the
        // window follows: the file is never held whole — a file past
        // what memory holds opens as its first window of lines, with
        // the rest waiting behind the end of the window.
        var scan = _files.Scan(path);
        var read = _files.ReadLines(path, new LineAnchor(0, 0), DocumentWindow.Lines);
        return (scan, read);
    }

    /// <summary>
    /// Puts a file that was just read on the document: its window, its clean
    /// point, and the path behind the unmoved text.
    /// </summary>
    /// <param name="stage">The document to read into.</param>
    /// <param name="path">The file that was read.</param>
    /// <param name="scan">The file's counts and convention.</param>
    /// <param name="read">The window's lines, and whether more stand behind.</param>
    private void AdoptRead(IFileStage stage, string path, ScanResult scan, LineRead read) {
        // The file's own text arriving is the same rebegun as a new
        // document — not a move the writer made — so the old moves go, the
        // ledger takes this window as its zero, and the replacement is not
        // recorded as a move against anything.
        SourcePath = path;
        stage.AdoptFile(path, scan, read.Text, read.More);

        // The file's own convention is kept, so saving writes the breaks back
        // the way they arrived rather than rewriting them — and it becomes
        // the clean point, like the text beside it.
        stage.MarkSaved(scan.Newline);
        stage.ReseatCaret();
        SavedNewline = scan.Newline;
        Remember(path);
    }

    /// <summary>
    /// Writes the document to a path, which becomes the document's file.
    /// </summary>
    /// <param name="stage">The document to write.</param>
    /// <param name="path">The file to write.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// Only after a successful write does the path become the document's, so a
    /// failed save does not silently re-point the document at a file it never
    /// reached. Like <see cref="Open"/>, this holds its thread for the whole
    /// write; a caller drawing the window wants <see cref="SaveAsync"/>.
    /// </remarks>
    public DocumentOutcome Save(IFileStage stage, string path) {
        ScanResult written;
        try {
            // The file as it stands, with every move since it was read laid
            // over the spans those moves fall on: the ledger says what
            // changed, the file service streams the rest. What comes back
            // is what the file now says — the whole file's counts, however
            // little of it was on screen.
            written = _files.WriteCombined(path, SourcePath, stage.PendingChanges(), stage.Newline);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        return CommitSave(stage, path, written);
    }

    /// <summary>
    /// Writes the document to a path without holding the thread that asked,
    /// so the window keeps drawing while a large file is composed and laid
    /// down.
    /// </summary>
    /// <param name="stage">The document to write.</param>
    /// <param name="path">The file to write.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// The moves are taken before the work leaves, on the thread that owns
    /// them — what the writer lays down is what the document held at this
    /// instant, and the writer never sees the document again. Everything past
    /// that is work on bytes alone, so it runs elsewhere; the adoption waits
    /// for the thread that owns the document.
    /// </remarks>
    public async Task<DocumentOutcome> SaveAsync(IFileStage stage, string path) {
        ScanResult written;
        try {
            var pending = stage.PendingChanges();
            var newline = stage.Newline;
            written = await Task.Run(() => _files.WriteCombined(path, SourcePath, pending, newline));
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        return CommitSave(stage, path, written);
    }

    /// <summary>
    /// The half of a save that belongs to the document rather than to the
    /// file: the path, the clean point, and what the status bar promises.
    /// </summary>
    /// <param name="stage">The document that was written.</param>
    /// <param name="path">The file that now holds it.</param>
    /// <param name="written">What the file says, having been written.</param>
    /// <returns>Always success — the failure paths end before reaching here.</returns>
    private DocumentOutcome CommitSave(IFileStage stage, string path, ScanResult written) {
        SourcePath = path;
        stage.ApplySave(path, written);

        // A .shiranux file goes out on line feeds whatever the status bar
        // named — that is the shape of the format, and the window's letters
        // were normalised to match as they were laid in. So the convention
        // the file landed on is the one the document now stands on: keeping
        // the old one would have the bar promising breaks the file does not
        // carry.
        //
        // The clean point lands first, deliberately: moving the convention
        // is what fires the dirty notification, and that notification reads
        // whichever clean point stands at the instant it fires. Landing it
        // after would leave the bar claiming dirty on a file just written.
        var own = Utf8File.IsShiranux(path);
        SavedNewline = own ? Newline.Lf : stage.Newline;

        if (own) {
            stage.MarkSaved(Newline.Lf);
        }

        Remember(path);

        return DocumentOutcome.Done;
    }

    /// <summary>
    /// The document as its file would stand now, without writing it: the same
    /// composition a save makes, handed back as whole text.
    /// </summary>
    /// <param name="stage">The document whose moves are laid over the file.</param>
    /// <returns>
    /// The whole writing — the letters outside the window arrive off the
    /// file itself, so the answer carries the document entire, wherever the
    /// window stands.
    /// </returns>
    public string WholeText(IFileStage stage) => _files.ReadCombined(SourcePath, stage.PendingChanges());

    /// <summary>
    /// The same whole writing, handed back without holding the thread that
    /// asked, so a large document arrives while the window keeps drawing.
    /// </summary>
    /// <param name="stage">The document whose moves are laid over the file.</param>
    /// <returns>The whole writing, however little of it was on screen.</returns>
    /// <remarks>
    /// The moves are read here, on the thread that owns them, and only the
    /// composition moves off it — the same split a save makes, and for the
    /// same reason: the answer must be the document as of one instant.
    /// </remarks>
    public async Task<string> WholeTextAsync(IFileStage stage) {
        var pending = stage.PendingChanges();
        return await Task.Run(() => _files.ReadCombined(SourcePath, pending));
    }

    /// <summary>
    /// Remembers a path the document just stood on: to the front of the
    /// list, and in the store's own copy.
    /// </summary>
    /// <param name="path">The file to remember.</param>
    private void Remember(string path) {
        RecentFiles.MoveToFront(path);

        // The store keeps its own copy, capped to the same number, so the list
        // outlives the session that built it.
        _recentStore.Remember(path);
    }

    /// <summary>
    /// What a document with no file behind it stands on: no lines, and
    /// nothing standing outside a window to count.
    /// </summary>
    private static ScanResult BlankScan() =>
        new(0, 0, 0, Newline.Detect(string.Empty));
}
