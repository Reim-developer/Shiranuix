using CommunityToolkit.Mvvm.Input;
using Shiranuix.CoreFormat;
using Shiranuix.Services;
using Shiranuix.ViewModels;

namespace Shiranuix.ViewModels;

/// <summary>
/// The commands the chrome offers: the file flows behind the toolbar and the
/// recent-files list, the status bar's two choices, and the confirm prompt
/// that stands before any of them can lose work.
/// </summary>
/// <remarks>
/// Each command lands a status line — the one sentence the writer reads
/// after the act — and each file flow asks the same question first: is
/// there something here worth keeping? The view binds these by their
/// generated names (<c>NewDocumentCommand</c> and the rest), which is why
/// they stay on the class the view is bound to.
/// </remarks>
public partial class EditorViewModel {
    /// <summary>
    /// Switches the document's line-break convention, by the name the status
    /// bar shows ("LF" or "CRLF").
    /// </summary>
    /// <param name="name">The convention's name; anything else changes nothing.</param>
    /// <remarks>
    /// A choice, not a conversion: the text keeps every break as typed, and
    /// the new convention is what the next save writes them as. The document
    /// counts itself dirty over it — the file on disk would now be written
    /// differently — until the save catches up.
    /// </remarks>
    [RelayCommand]
    private void SetNewline(string? name) {
        var wanted = name switch {
            "LF" => Shiranuix.Models.Newline.Lf,
            "CRLF" => Shiranuix.Models.Newline.CrLf,
            _ => null
        };

        if (wanted is not null) {
            Document.Newline = wanted;
        }
    }

    /// <summary>
    /// Turns word wrap on and off.
    /// </summary>
    [RelayCommand]
    private void ToggleWordWrap() => WordWrap = !WordWrap;

    /// <summary>
    /// Sets the page's lines flush left.
    /// </summary>
    [RelayCommand]
    private void AlignLeft() => TextAlign = Shiranuix.Models.TextAlign.Left;

    /// <summary>
    /// Sets the page's lines centred.
    /// </summary>
    [RelayCommand]
    private void AlignCenter() => TextAlign = Shiranuix.Models.TextAlign.Center;

    /// <summary>
    /// Sets the page's lines flush right.
    /// </summary>
    [RelayCommand]
    private void AlignRight() => TextAlign = Shiranuix.Models.TextAlign.Right;

    /// <summary>
    /// Spreads the page's wrapped lines between its rims.
    /// </summary>
    [RelayCommand]
    private void AlignJustify() => TextAlign = Shiranuix.Models.TextAlign.Justify;

    /// <summary>
    /// Creates a blank document, after confirming if the current one is dirty.
    /// </summary>
    [RelayCommand]
    private async Task NewDocumentAsync() {
        if (!await EmptyPageAsync()) {
            return;
        }

        StatusMessage = "New document";
    }

    /// <summary>
    /// How long a file task may run before the veil goes over the window.
    /// </summary>
    /// <remarks>
    /// Long enough that a small save is over before the eye would notice —
    /// a veil flashing on every Ctrl+S would be the nuisance it was meant
    /// to prevent — and short enough that a slow one is seen at once.
    /// </remarks>
    private const int VeilDelayMilliseconds = 300;

    /// <summary>
    /// Runs a file task with the veil over the window, showing it only if the
    /// task outlasts a flicker.
    /// </summary>
    /// <typeparam name="T">What the task hands back.</typeparam>
    /// <param name="message">What the veil says is happening.</param>
    /// <param name="task">The work, already pointed off the drawing thread.</param>
    /// <returns>Whatever the task handed back.</returns>
    private async Task<T> VeiledAsync<T>(string message, Func<Task<T>> task) {
        BusyMessage = message;
        var overdue = Task.Delay(VeilDelayMilliseconds);
        var running = task();

        try {
            // Whichever ends first decides whether the veil is ever drawn: a
            // task that finishes inside the delay leaves the window exactly
            // as it was, and one still running lifts the veil for the rest
            // of its span — the finally is what guarantees it comes down,
            // faulted task or not.
            if (await Task.WhenAny(running, overdue) == overdue) {
                IsBusy = true;
            }

            return await running;
        } finally {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Hands a file to the document behind the veil, and the status line says
    /// what came of it.
    /// </summary>
    /// <param name="path">The file to read in.</param>
    /// <returns>What came of it, including a reason on failure.</returns>
    /// <remarks>
    /// The reading runs off the thread that draws the window — a large file
    /// used to stall the whole frame while it was combed through — and the
    /// veil keeps the text still until it lands: the file's window becomes the
    /// document's zero, so a keystroke arriving mid-read would leave the
    /// ledger balanced against a baseline that had already moved.
    ///
    /// The typeface the file is set in is settled before any of that, since
    /// a face this machine does not carry cannot be asked about once the
    /// words are already on the page — and answered with nothing, nothing
    /// is opened and nothing went wrong either.
    /// </remarks>
    private async Task<DocumentOutcome> ReadIntoAsync(string path) {
        if (IsPdf(path)) {
            return AnnouncePdf(path);
        }

        // What the file says it is set in has to be settled before it lands:
        // a face this machine does not carry cannot be asked about once the
        // words are already on the page. This costs a few bytes at a file's
        // head and nothing at all for a plain file, so it runs before the
        // veil rather than under it — the question is the writer's, and a
        // veil saying "opening" behind it would be a second thing to read.
        var said = await Document.ReadSettingAsync(path);
        var settled = await SettleTypefaceAsync(said);

        if (settled is null && said.Set is not null) {
            // The file named a face nobody here can draw, and nobody chose
            // another: nothing opens, and nothing went wrong either.
            StatusMessage = "Opening was cancelled.";
            return DocumentOutcome.CalledOff;
        }

        var outcome = await VeiledAsync(
            $"Opening {System.IO.Path.GetFileName(path)}…",
            () => Document.OpenAsync(path, settled));

        StatusMessage = outcome.Succeeded
            ? $"Opened {Document.Name}"
            : outcome.WasCalledOff
                ? "Opening was cancelled."
                : $"Could not open: {outcome.Detail}";

        return outcome;
    }

    /// <summary>
    /// The face a file is actually opened in: its own where this machine
    /// carries it, whatever the dialog is answered with where it does not,
    /// and nothing at all where the file names no face to begin with.
    /// </summary>
    /// <param name="said">What the file said it is set in, and a stretch of its words.</param>
    /// <returns>
    /// The face to open in, or null when the file said nothing — or when it
    /// said something and the person asked chose not to open it after all.
    /// </returns>
    private async Task<DocumentTypography?> SettleTypefaceAsync(
        (DocumentTypography? Set, string Sample) said) {
        if (said.Set is null || Typography.Carries(said.Set.FontFamily)) {
            return said.Set;
        }

        // The file's own first line is what every face is judged against —
        // its own words are the only words that say whether it will read the
        // way it was meant to. A file that gave up nothing to show still
        // gets words to show, since an empty box judges nothing.
        var sample = string.IsNullOrWhiteSpace(said.Sample)
            ? "The quick brown fox jumps over the lazy dog."
            : said.Sample;

        var picked = await _missingFonts.AskAsync(
            said.Set.FontFamily,
            said.Set.FontSize,
            sample,
            Typography.InstalledFontFamily);

        return picked is null ? null : said.Set with { FontFamily = picked };
    }

    /// <summary>
    /// Opens a file, asking for it first: the file is named before anything
    /// is asked of the writing, because a paper opened to read is not a
    /// change to what is being written.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync() {
        var path = await _dialogs.PickOpenFileAsync();
        if (path is null) {
            return;
        }

        if (IsPdf(path)) {
            AnnouncePdf(path);
            return;
        }

        if (!await ConfirmDiscardAsync()) {
            return;
        }

        _ = await ReadIntoAsync(path);
    }

    /// <summary>
    /// Opens a folder for the file tree.
    /// </summary>
    /// <remarks>
    /// Asking again when one already stands is how the tree moves. Standing
    /// somewhere else is a new start: the page is emptied the way the New
    /// command empties it, asked first - and a writer who keeps their work
    /// keeps the whole arrangement as it is, page and tree together.
    /// </remarks>
    [RelayCommand]
    private async Task OpenDirectoryAsync() {
        if (Tree is not { } tree) {
            return;
        }

        await tree.ChooseFolderAsync(CancellationToken.None);
    }

    /// <summary>
    /// Opens a file without asking, used by the recent-files list.
    /// </summary>
    /// <param name="path">The file to open.</param>
    [RelayCommand]
    private async Task OpenRecentAsync(string path) {
        if (IsPdf(path)) {
            AnnouncePdf(path);
            return;
        }

        if (!await ConfirmDiscardAsync()) {
            return;
        }

        _ = await ReadIntoAsync(path);
    }

    /// <summary>
    /// Saves, prompting for a path only when the document has never been saved.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync() {
        if (Document.Path is null) {
            await SaveAsAsync();
            return;
        }

        await SaveToAsync(Document.Path);
    }

    /// <summary>
    /// Always prompts for a path, then saves there.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsAsync() {
        var suggested = Document.Path is null ? "Untitled.txt" : Document.Name;
        var path = await _dialogs.PickSaveFileAsync(suggested);
        if (path is null) {
            return;
        }

        await SaveToAsync(path);
    }

    /// <summary>
    /// Opens a file named on the command line.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <remarks>
    /// Prompt-free by construction: <c>Shiranuix file.txt</c> is the same path
    /// Explorer takes when a document is double-clicked, and the blank document
    /// it would discard was created moments ago by this same launch and cannot
    /// have been edited.
    /// </remarks>
    public async Task OpenFromCommandLineAsync(string path) => await ReadIntoAsync(path);

    /// <summary>
    /// Hands a paper to the reader, and the status line says what came.
    /// </summary>
    /// <param name="path">The PDF to show.</param>
    /// <returns>What came of the show, for the tree's outcome.</returns>
    private DocumentOutcome AnnouncePdf(string path) {
        var outcome = _pdfViewers.Show(path);

        StatusMessage = outcome.Succeeded
            ? $"Opened {System.IO.Path.GetFileName(path)} to read."
            : $"Could not open: {outcome.Detail}";

        return outcome;
    }

    /// <summary>
    /// Whether a path names a paper: an extension of .pdf, said however it
    /// was said.
    /// </summary>
    private static bool IsPdf(string path) =>
        System.IO.Path.GetExtension(path) is { } extension
        && extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the document to a path behind the veil, and the status line
    /// says what came of it.
    /// </summary>
    /// <param name="path">The file to write.</param>
    private async Task SaveToAsync(string path) {
        var outcome = await VeiledAsync(
            $"Saving {Document.Name}…",
            () => Document.SaveAsync(path));

        StatusMessage = outcome.Succeeded
            ? $"Saved {Document.Name}"
            : $"Could not save: {outcome.Detail}";
    }

    /// <summary>
    /// Asks the user whether to discard unsaved work. Clean documents do not
    /// prompt at all.
    /// </summary>
    /// <returns>True when it is safe to proceed.</returns>
    private async Task<bool> ConfirmDiscardAsync() {
        if (!Document.IsDirty) {
            return true;
        }

        var name = Document.Path is null ? "this document" : Document.Name;
        return await _confirm.ConfirmDiscardAsync(name);
    }

    /// <summary>
    /// Empties the page for whatever comes next, asking about unsaved work
    /// first, the way the New command does.
    /// </summary>
    /// <returns>Whether the page is empty: a writer who kept their work keeps
    /// the page, and the caller should change nothing.</returns>
    /// <remarks>
    /// Public because the tree asks for it by name: standing the tree
    /// somewhere else is a new start, and it should feel exactly like the New
    /// command - asked first, and never taking work away unasked.
    /// </remarks>
    public async Task<bool> EmptyPageAsync() {
        if (!await ConfirmDiscardAsync()) {
            return false;
        }

        Document.New();
        StatusMessage = "New page.";
        return true;
    }

    /// <summary>
    /// Opens a path on the writer's behalf, with the same question about
    /// unsaved work that opening it themselves would ask — and none of it
    /// for a paper, which is shown beside the writing and replaces nothing.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <returns>What came of it, including a refusal when they declined.</returns>
    /// <remarks>
    /// Internal rather than public because only the file tree opens files
    /// this way; the writer has the toolbar, the recent list and the file
    /// picker, and none of them go through here.
    /// </remarks>
    internal async Task<DocumentOutcome> OpenPathAsync(string path) {
        if (IsPdf(path)) {
            return AnnouncePdf(path);
        }

        if (!await ConfirmDiscardAsync()) {
            return DocumentOutcome.Failed(
                "You kept the unsaved changes, so the file was not opened.");
        }

        return await ReadIntoAsync(path);
    }

    /// <summary>
    /// Opens and closes the find bar.
    /// </summary>
    [RelayCommand]
    private void ToggleFind() => Find.IsOpen = !Find.IsOpen;
}
