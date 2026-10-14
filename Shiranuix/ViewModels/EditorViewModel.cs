using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Models;
using Shiranuix.Services.Dialogs;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The window's coordinator: the commands the chrome offers, the status line
/// and title they produce, and the two pieces of state it hands between.
/// </summary>
/// <remarks>
/// <para>
/// This knows how to ask, not how to do. Picking a file, confirming a discard
/// and putting words on the status line are all it handles; the document itself
/// and its typography live in <see cref="DocumentSession"/> and
/// <see cref="TypographyState"/>, which the container builds and this
/// coordinates. The view binds those two directly, so nothing here is a store
/// of the document's or the pickers' contents.
/// </para>
/// <para>
/// It holds no UI types either — not one control, not one window. The things
/// that would have forced a UI dependency (picking a file, confirming a
/// discard) and the things that would have forced a database (settings, recent
/// files, font history) are interfaces handed in at construction, so the whole
/// view model runs headlessly in the test project.
/// </para>
/// </remarks>
public partial class EditorViewModel : ViewModelBase {
    private readonly IFileDialogService _dialogs;
    private readonly IConfirmDialogService _confirm;

    /// <summary>
    /// Creates a view model over the dialogs it needs and the state it
    /// coordinates.
    /// </summary>
    /// <param name="dialogs">File pickers.</param>
    /// <param name="confirm">Discard confirmation prompt.</param>
    /// <param name="document">The open document.</param>
    /// <param name="typography">The document's typography and theme.</param>
    public EditorViewModel(
        IFileDialogService dialogs,
        IConfirmDialogService confirm,
        DocumentSession document,
        TypographyState typography) {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Typography = typography ?? throw new ArgumentNullException(nameof(typography));

        // The states own the rules. The view still binds this class, and it
        // hears them only if the changes are raised here under the names its
        // doors carry, so each state is exposed in turn.
        Expose(document, name => {
            // The title reads the document's name and dirty state, so it moves
            // with any of them.
            OnPropertyChanged(nameof(Title));

            // Two flags are computed from the document: whether the status bar
            // has a document to count, and whether the start screen is up. The
            // composition moves the first one — an input method's keystroke is
            // the document being written before the text arrives, and the
            // Vietnamese writer's first "â" should bring the line and column
            // up with it rather than after it.
            if (name is nameof(DocumentSession.IsEmpty)
                    or nameof(DocumentSession.Path)
                    or nameof(DocumentSession.Composition)) {
                OnPropertyChanged(nameof(HasDocument));
                OnPropertyChanged(nameof(IsStartVisible));
            }

            // The status bar's line-sequence segment ticks the convention
            // that is current, so the choice and the display cannot drift.
            if (name == nameof(DocumentSession.Newline)) {
                OnPropertyChanged(nameof(NewlineIsLf));
                OnPropertyChanged(nameof(NewlineIsCrLf));
            }
        });

        Expose(typography, name => {
            // The format bar's toggle shows the word-wrap state in words.
            if (name == nameof(TypographyState.WordWrap)) {
                OnPropertyChanged(nameof(WordWrapLabel));
            }
        });
    }

    /// <summary>
    /// The document: its text, path, counts and recent files.
    /// </summary>
    public DocumentSession Document { get; }

    /// <summary>
    /// The document's typography and the colour theme.
    /// </summary>
    public TypographyState Typography { get; }

    // ===== What the view binds ===============================================
    //
    // Avalonia's compiled bindings take their property name from the path's
    // first segment - the object the view is bound to, which is this class.
    // A nested path neither writes nor refreshes: {Document.Text, Mode=TwoWay}
    // never reached the setter, and {Document.IsEmpty} never re-evaluated, both
    // of which showed up as the start screen refusing to disappear on the first
    // keystroke.
    //
    // So the view model is the surface the view binds. Every member here is a
    // one-line door into the state that owns the rule - no logic of its own -
    // and the states' changes are forwarded under the same names in the
    // constructor, so the doors stay live.

    /// <summary>The document's full text. The single source of truth for its content.</summary>
    public string Text {
        get => Document.Text;
        set => Document.Text = value;
    }

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

    /// <summary>The requested colour theme.</summary>
    public ThemeMode ThemeMode {
        get => Typography.ThemeMode;
        set => Typography.ThemeMode = value;
    }

    /// <summary>The point sizes offered by the size picker.</summary>
    public IReadOnlyList<double> FontSizes => Typography.FontSizes;

    /// <summary>The typefaces offered by the font picker.</summary>
    public IReadOnlyList<string> AvailableFonts => Typography.AvailableFonts;

    /// <summary>The line height the page is set in.</summary>
    public double EditorLineHeight => Typography.EditorLineHeight;

    /// <summary>
    /// Whether the editor has the caret's attention, as the view reports it.
    /// </summary>
    /// <remarks>
    /// A view-side fact, reported in rather than asked for: the TextBox's own
    /// focus is pushed here OneWayToSource, so the window tells the view model
    /// where the user is instead of the view model guessing at it. It matters
    /// because an input method composes a character before it is text — a
    /// Vietnamese writer's first "â" lives in the control and not in the
    /// document — and because clicking into a page to write on it should clear
    /// the invitation before a single character exists.
    /// </remarks>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// True while the document is empty and the editor is not being worked in,
    /// which is when the start screen is drawn over the sheet.
    /// </summary>
    /// <remarks>
    /// Both halves matter. An empty document alone is not enough: a writer
    /// composing a character through an input method has an empty buffer and is
    /// most certainly not starting. Only <c>IsNullOrEmpty</c> counts as empty —
    /// a document containing a single space is something the user typed.
    /// </remarks>
    public bool IsStartVisible => Document.IsEmpty && !IsEditing;

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
    /// A short status line for the user ("Opened x.txt", "Saved", ...).
    /// </summary>
    /// <remarks>
    /// Empty until something has happened: a fresh window has no status to
    /// report, and "Ready" was a word that took up space to say nothing.
    /// </remarks>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// True while there is a status message to show.
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string value) =>
        OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnIsEditingChanged(bool value) =>
        OnPropertyChanged(nameof(IsStartVisible));

    /// <summary>
    /// The window title: "Shiranuix — filename", prefixed with a bullet when
    /// there are unsaved changes.
    /// </summary>
    public string Title => Document.IsDirty
        ? $"Shiranuix — • {Document.Name}"
        : $"Shiranuix — {Document.Name}";

    /// <summary>
    /// True while there is a document to speak of: a file has been opened, the
    /// buffer holds something typed, or the input method is composing a
    /// character at the caret.
    /// </summary>
    /// <remarks>
    /// What the status bar's counts are shown against. A fresh window has no
    /// counts worth reading — "0 words, 0 characters, 0 lines" describes a
    /// blank sheet, not a document — so the bar stays empty until there is one.
    /// An opened but empty file counts, because the file is open. A composing
    /// keystroke counts too: with an input method on, the first character of a
    /// session lives in the composition and not yet in the text, and waiting
    /// for it to commit left the bar blank until the writer pressed space.
    /// </remarks>
    public bool HasDocument => !Document.IsEmpty
        || Document.Path is not null
        || !Document.Composition.IsEmpty;

    /// <summary>
    /// Turns word wrap on and off.
    /// </summary>
    [RelayCommand]
    private void ToggleWordWrap() => WordWrap = !WordWrap;

    /// <summary>
    /// The word-wrap state as the format bar's toggle shows it.
    /// </summary>
    public string WordWrapLabel => WordWrap ? "Wrap" : "No wrap";

    /// <summary>
    /// Creates a blank document, after confirming if the current one is dirty.
    /// </summary>
    [RelayCommand]
    private async Task NewDocumentAsync() {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        Document.New();
        StatusMessage = "New document";
    }

    /// <summary>
    /// Opens a file, after confirming if the current one is dirty.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync() {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        var path = await _dialogs.PickOpenFileAsync();
        if (path is null) {
            return;
        }

        OpenPath(path);
    }

    /// <summary>
    /// Opens a file without asking, used by the recent-files list.
    /// </summary>
    /// <param name="path">The file to open.</param>
    [RelayCommand]
    private async Task OpenRecentAsync(string path) {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        OpenPath(path);
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

        SaveTo(Document.Path);
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

        SaveTo(path);
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
    public void OpenFromCommandLine(string path) => OpenPath(path);

    /// <summary>
    /// Cycles the theme.
    /// </summary>
    [RelayCommand]
    private void CycleTheme() => Typography.CycleTheme();

    private void OpenPath(string path) {
        var outcome = Document.Open(path);

        StatusMessage = outcome.Succeeded
            ? $"Opened {Document.Name}"
            : $"Could not open: {outcome.Detail}";
    }

    private void SaveTo(string path) {
        var outcome = Document.Save(path);

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
}
