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
    public DocumentOutcome Open(IFileStage stage, string path) {
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
        SourcePath = path;
        stage.AdoptFile(path, scan, read.Text, read.More);

        // The file's own convention is kept, so saving writes the breaks back
        // the way they arrived rather than rewriting them — and it becomes
        // the clean point, like the text beside it.
        stage.MarkSaved(scan.Newline);
        stage.ReseatCaret();
        SavedNewline = scan.Newline;
        Remember(path);

        return DocumentOutcome.Done;
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
    /// reached.
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
