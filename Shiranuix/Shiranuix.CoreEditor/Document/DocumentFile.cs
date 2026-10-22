using Shiranuix.CoreFormat;
namespace Shiranuix.CoreEditor;

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

    /// <summary>
    /// How much of a file's own writing is carried out to judge a
    /// substitute typeface by: long enough to show the letters, short
    /// enough to be read from a file of any size.
    /// </summary>
    private const int SampleCharacters = 300;

    /// <summary>
    /// Every line flush left — what a file that has never been aligned
    /// says about itself, and the standing its clean point rests on.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, ParagraphAlignment> None =
        new Dictionary<int, ParagraphAlignment>();

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

    /// <summary>
    /// The typeface and size the file behind the document holds — the point
    /// dirty is measured against, the way the convention has its own saved
    /// point — or null when the file cannot hold one at all.
    /// </summary>
    /// <remarks>
    /// Null means one of two things, and they behave the same: there is no
    /// file yet, or the file that is there is plain. Either way the setting
    /// has nowhere to go, so picking a new typeface over such a document is
    /// not a change to anything and leaves the document clean.
    /// </remarks>
    public DocumentTypography? SavedTypography { get; private set; }

    /// <summary>
    /// Whether the typeface the document stands on now differs from the one
    /// the file behind it holds — the setting's half of the dirty question.
    /// </summary>
    /// <param name="current">How the document is set as it stands.</param>
    /// <returns>
    /// True when there is a setting remembered and it is no longer this one.
    /// False where nothing is remembered, since there is nothing to depart
    /// from.
    /// </returns>
    public bool TypographyMoved(DocumentTypography current) {
        ArgumentNullException.ThrowIfNull(current);
        return SavedTypography is { } saved && !saved.Equals(current);
    }

    /// <summary>
    /// The way the file behind the document sets its lines — the point
    /// dirty is measured against, the way the typeface has its own saved
    /// point — or null when the file cannot hold any at all.
    /// </summary>
    /// <remarks>
    /// The same two nulls the typeface's own clean point answers with, and
    /// they mean the same thing: there is no file yet, or the file that is
    /// there is plain. Either way the setting has nowhere to go, so choosing
    /// where a line stands over such a document is not a change to anything
    /// and leaves the document clean. A <c>.shiranuix</c> file never answers
    /// with null — flush left is a setting it can carry as readily as any
    /// other, and the map it arrived with, however empty, is its own.
    /// </remarks>
    public IReadOnlyDictionary<int, ParagraphAlignment>? SavedAlignments { get; private set; }

    /// <summary>
    /// Whether the lines are set differently from how the file behind the
    /// document sets them — the setting's half of the dirty question.
    /// </summary>
    /// <param name="current">How the lines stand as they are now.</param>
    /// <returns>
    /// True when a setting is remembered and it is no longer this one.
    /// False where nothing is remembered, since there is nothing to depart
    /// from.
    /// </returns>
    public bool AlignmentsMoved(IReadOnlyDictionary<int, ParagraphAlignment> current) {
        ArgumentNullException.ThrowIfNull(current);

        if (SavedAlignments is not { } saved) {
            return false;
        }

        if (saved.Count != current.Count) {
            return true;
        }

        // Compared entry by entry rather than by identity: the document
        // copies its map every time a line is set, so two maps holding the
        // same answers are the same setting even though they are not the
        // same object.
        foreach (var (line, alignment) in saved) {
            if (!current.TryGetValue(line, out var now) || now != alignment) {
                return true;
            }
        }

        return false;
    }

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

        // The alignment's clean point lands before the blank replaces
        // anything, so the notifications the replacement raises read the
        // blank document's own: it stands on no file, so nothing is
        // remembered against it and no line is a departure from any.
        SavedAlignments = null;

        stage.AdoptBlank(BlankScan(), Newline.Default);
        SavedNewline = Newline.Default;

        // A blank document has no file to hold a setting, so nothing is
        // remembered against it: the typeface it draws in cannot be a
        // departure from anything.
        SavedTypography = null;
    }

    /// <summary>
    /// Reads a file into the document. A failed read changes nothing.
    /// </summary>
    /// <param name="stage">The document to read into.</param>
    /// <param name="path">The file to read.</param>
    /// <param name="said">The typeface and size the file was settled on,
    /// after whatever face it named was found or replaced; null for a file
    /// that says nothing, or a caller that never asked.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// This holds its thread from the first byte to the last, which is right
    /// for a caller already off the drawing thread and wrong for one sitting
    /// on it — that caller wants <see cref="OpenAsync"/>, which reads
    /// elsewhere and hands the document back only once the file has landed.
    /// </remarks>
    public DocumentOutcome Open(
        IFileStage stage,
        string path,
        DocumentTypography? said = null) {
        ScanResult scan;
        LineRead read;
        IReadOnlyDictionary<int, ParagraphAlignment>? aligned;
        try {
            (scan, read, aligned) = ReadWindow(path);
        } catch (Exception ex) when (IsUnreadable(ex)) {
            return DocumentOutcome.Unreadable(ex.Message);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        AdoptRead(stage, path, scan, read, said, aligned);
        return DocumentOutcome.Done;
    }

    /// <summary>
    /// Reads a file into the document without holding the thread that asked,
    /// so a large file arrives while the window keeps drawing.
    /// </summary>
    /// <param name="stage">The document to read into.</param>
    /// <param name="path">The file to read.</param>
    /// <param name="said">The typeface and size the file was settled on,
    /// after whatever face it named was found or replaced; null for a file
    /// that says nothing, or a caller that never asked.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// Only the reading moves off-thread, because it is bytes and counts and
    /// touches nothing anyone is watching. The adoption comes back to the
    /// thread that owns the document: every notification it fires belongs to
    /// the bindings listening there.
    /// </remarks>
    public async Task<DocumentOutcome> OpenAsync(
        IFileStage stage,
        string path,
        DocumentTypography? said = null) {
        ScanResult scan;
        LineRead read;
        IReadOnlyDictionary<int, ParagraphAlignment>? aligned;
        try {
            (scan, read, aligned) = await Task.Run(() => ReadWindow(path));
        } catch (Exception ex) when (IsUnreadable(ex)) {
            return DocumentOutcome.Unreadable(ex.Message);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        AdoptRead(stage, path, scan, read, said, aligned);
        return DocumentOutcome.Done;
    }

    /// <summary>
    /// Whether an error came from the bytes themselves: the file is not the
    /// shape it claims to be, is a shape from further ahead than this build,
    /// or ends before its own head promised it would.
    /// </summary>
    /// <param name="error">What the read threw.</param>
    /// <returns>True when the file is the thing that is wrong.</returns>
    /// <remarks>
    /// Told apart from every other failure on purpose. A file that is not
    /// there, or that this user may not read, is a fact about the machine or
    /// the day; this is a fact about the file, and it will be the same
    /// tomorrow. That is the one kind of failure worth stopping the user
    /// over, and the only one no retry has any hope of mending.
    /// </remarks>
    private static bool IsUnreadable(Exception error) =>
        error is InvalidDataException or EndOfStreamException;

    /// <summary>
    /// Reads what a file says about how it is set — the typeface and point
    /// size it carries — without touching the document, and gathers the
    /// first stretch of its own writing to judge a substitute face by.
    /// </summary>
    /// <param name="path">The file to ask.</param>
    /// <returns>
    /// What the file carried and a sample of its words, off the thread that
    /// asked. A file carrying nothing answers with nothing and no sample:
    /// there is no face to miss and therefore no face to pick.
    /// </returns>
    /// <remarks>
    /// This is asked before the file is opened rather than as part of
    /// opening it, because the face it names may be one this machine does
    /// not carry, and that has to be settled with whoever is opening the
    /// file before any of its words land on the page. Gathering the sample
    /// failing is not a failed open — the sample exists only so a choice can
    /// be made by looking — so it is swallowed here and the face still
    /// travels on.
    /// </remarks>
    public async Task<(DocumentTypography? Set, string Sample)> ReadSettingAsync(string path) {
        ArgumentNullException.ThrowIfNull(path);

        return await Task.Run(() => {
            DocumentTypography? said;
            try {
                said = _files.ReadTypography(path);
            } catch {
                // A file that cannot be asked is a file with nothing to say
                // here: the open itself reads it next, and the reason it
                // could not be read belongs to that read rather than to this
                // question. Nothing is guessed at in between.
                return ((DocumentTypography?)null, string.Empty);
            }

            if (said is null) {
                return ((DocumentTypography?)null, string.Empty);
            }

            string sample;
            try {
                var text = _files.ReadFrom(path, new LineAnchor(0, 0), SampleCharacters);
                var cut = text.IndexOf('\n');
                sample = (cut < 0 ? text : text[..cut]).Trim();
            } catch {
                sample = string.Empty;
            }

            return (said, sample);
        });
    }

    /// <summary>
    /// Everything about a file that can be learned without touching the
    /// document: its counts, its convention, the lines the window shows,
    /// and how it sets the lines it does.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The file as far as the window reaches.</returns>
    private (ScanResult Scan, LineRead Read, IReadOnlyDictionary<int, ParagraphAlignment>? Aligned) ReadWindow(string path) {
        // The counts and the convention come off the stream, and the
        // window follows: the file is never held whole — a file past
        // what memory holds opens as its first window of lines, with
        // the rest waiting behind the end of the window. The alignments
        // come off their own section beside those, numbered against the
        // same text, so the three answers describe one file.
        var scan = _files.Scan(path);
        var read = _files.ReadLines(path, new LineAnchor(0, 0), DocumentWindow.Lines);
        var aligned = _files.ReadAlignments(path);
        return (scan, read, aligned);
    }

    /// <summary>
    /// Puts a file that was just read on the document: its window, its clean
    /// point, and the path behind the unmoved text.
    /// </summary>
    /// <param name="stage">The document to read into.</param>
    /// <param name="path">The file that was read.</param>
    /// <param name="scan">The file's counts and convention.</param>
    /// <param name="read">The window's lines, and whether more stand behind.</param>
    /// <param name="said">How the file said it was set, settled before it landed.</param>
    /// <param name="aligned">How the file sets its lines, by their number
    /// in it — null where the file carries none.</param>
    private void AdoptRead(
        IFileStage stage,
        string path,
        ScanResult scan,
        LineRead read,
        DocumentTypography? said,
        IReadOnlyDictionary<int, ParagraphAlignment>? aligned) {
        // The file's own text arriving is the same rebegun as a new
        // document — not a move the writer made — so the old moves go, the
        // ledger takes this window as its zero, and the replacement is not
        // recorded as a move against anything.
        SourcePath = path;
        stage.AdoptFile(path, scan, read.Text, read.More);

        // The setting lands with the words: a file that carried one brings
        // its face over, and one that carried none leaves the document where
        // it stood, which is exactly what a face nobody can name would do.
        stage.AdoptTypography(said);

        // The file's own convention is kept, so saving writes the breaks back
        // the way they arrived rather than rewriting them — and it becomes
        // the clean point, like the text beside it.
        stage.MarkSaved(scan.Newline);
        stage.ReseatCaret();
        SavedNewline = scan.Newline;

        // What the file holds is what it was written with: the face it
        // carried, or — where it carried none — the face the document now
        // stands on, so saving such a file writes a face down and the
        // document turns clean by writing it. A plain file holds no face at
        // all, so nothing is remembered against it.
        SavedTypography = Utf8File.IsShiranuix(path)
            ? said ?? stage.Typography
            : null;

        // The alignments' clean point lands before the lines are told of
        // it, the same way the typeface's does: the notification they raise
        // reads whichever clean point stands at the instant it fires. A
        // .shiranuix file holds a set of alignments however few — flush left
        // is an answer as much as any other, so an arrival that carried
        // none stands on an empty map rather than on nothing. A plain file
        // holds none at all, so nothing is remembered against it.
        SavedAlignments = Utf8File.IsShiranuix(path)
            ? aligned ?? None
            : null;
        stage.AdoptAlignments(aligned);

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
        IReadOnlyDictionary<int, ParagraphAlignment>? aligned;
        try {
            // The file as it stands, with every move since it was read laid
            // over the spans those moves fall on: the ledger says what
            // changed, the file service streams the rest. What comes back
            // is what the file now says — the whole file's counts, however
            // little of it was on screen. The way the lines stand travels
            // the same road: the document's map is numbered against the
            // writing as it now stands, so the writer lays it down as it
            // comes — every entry beside the letters it was set over — and
            // the file it lands in is asked for it back. The numbers a
            // save remembers from here are the ones the file it just
            // wrote names.
            written = _files.WriteCombined(
                path, SourcePath, stage.PendingChanges(), stage.Newline,
                stage.Typography, stage.Alignments);
            aligned = ReadAlignmentsBack(path);
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        return CommitSave(stage, path, written, aligned);
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
        IReadOnlyDictionary<int, ParagraphAlignment>? aligned;
        try {
            // The moves, the convention, the setting and the alignments are
            // all taken before the work leaves, on the thread that owns
            // them: what the writer lays down is what the document held at
            // this instant, and the writer never sees the document again.
            var pending = stage.PendingChanges();
            var newline = stage.Newline;
            var set = stage.Typography;
            var lines = stage.Alignments;

            // Everything past that is work on bytes alone — composing the
            // text, carrying the lines' own way onto it, and asking the
            // file what landed — so it all runs elsewhere; the adoption
            // waits for the thread that owns the document.
            (written, aligned) = await Task.Run(() => {
                var done = _files.WriteCombined(path, SourcePath, pending, newline, set, lines);
                return (done, ReadAlignmentsBack(path));
            });
        } catch (Exception ex) {
            return DocumentOutcome.Failed(ex.Message);
        }

        return CommitSave(stage, path, written, aligned);
    }

    /// <summary>
    /// Asks the file just written how it sets its lines, or nothing at all
    /// when the file cannot hold any.
    /// </summary>
    /// <param name="path">The file that was just written.</param>
    /// <returns>
    /// The map the file now carries — the writer's own re-counting of what
    /// the document asked for — or null where the target is a plain file
    /// that keeps its letters and nothing else.
    /// </returns>
    private IReadOnlyDictionary<int, ParagraphAlignment>? ReadAlignmentsBack(string path) =>
        Utf8File.IsShiranuix(path) ? _files.ReadAlignments(path) : null;

    /// <summary>
    /// The half of a save that belongs to the document rather than to the
    /// file: the path, the clean point, and what the status bar promises.
    /// </summary>
    /// <param name="stage">The document that was written.</param>
    /// <param name="path">The file that now holds it.</param>
    /// <param name="written">What the file says, having been written.</param>
    /// <param name="aligned">How the file just written sets its lines —
    /// read back off it, so the numbers name its own text.</param>
    /// <returns>Always success — the failure paths end before reaching here.</returns>
    private DocumentOutcome CommitSave(
        IFileStage stage,
        string path,
        ScanResult written,
        IReadOnlyDictionary<int, ParagraphAlignment>? aligned) {
        SourcePath = path;

        // The setting the file now holds lands before anything is announced,
        // for the same reason the convention's clean point lands where it
        // does below: the notifications a save raises read whichever clean
        // points stand at the instant they fire. A document whose only change
        // was its typeface has to hear "clean" out of the very first
        // notification, rather than one raised while an older face was still
        // remembered. A plain file holds no face, so nothing is remembered
        // against it and a face picked over it is not a change to a file.
        SavedTypography = Utf8File.IsShiranuix(path) ? stage.Typography : null;

        // The way the lines stand lands the same way, a step ahead of the
        // lines being told of it. What the document handed the writer was
        // numbered against the writing as it stands — the same numbers the
        // gutter shows — so the map read back is what the document now
        // stands on: it names the very text on screen, and remembering it
        // turns the document clean rather than leaving a successful save
        // looking like a change. A plain file holds no way of setting a
        // line at all, so nothing is remembered against it and the lines
        // the writer chose go on being held for a target that can carry
        // them.
        var own = Utf8File.IsShiranuix(path);
        SavedAlignments = own ? aligned ?? None : null;

        if (own) {
            stage.AdoptAlignments(aligned);
        }

        stage.ApplySave(path, written);

        // A .shiranuix file goes out on line feeds whatever the status bar
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
