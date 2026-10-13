using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Services;

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

        // The title names the file and carries the dirty bullet, so it moves
        // whenever the document does — by any of its own moves, too, which the
        // view model never sees.
        document.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));
        typography.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));
    }

    /// <summary>The document: its text, path, counts and recent files.</summary>
    public DocumentSession Document { get; }

    /// <summary>The document's typography and the colour theme.</summary>
    public TypographyState Typography { get; }

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

    /// <summary>
    /// The window title: "Shiranuix — filename", prefixed with a bullet when
    /// there are unsaved changes.
    /// </summary>
    public string Title => Document.IsDirty
        ? $"Shiranuix — • {Document.Name}"
        : $"Shiranuix — {Document.Name}";

    /// <summary>
    /// True while there is a document to speak of: a file has been opened, or
    /// the buffer holds something typed.
    /// </summary>
    /// <remarks>
    /// What the status bar's counts are shown against. A fresh window has no
    /// counts worth reading — "0 words, 0 characters, 0 lines" describes a
    /// blank sheet, not a document — so the bar stays empty until there is one.
    /// An opened but empty file counts, because the file is open.
    /// </remarks>
    public bool HasDocument => !Document.IsEmpty || Document.Path is not null;

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
