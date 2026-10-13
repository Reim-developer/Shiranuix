using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Utilities;

namespace Shiranuix.ViewModels;

/// <summary>
/// The writing surface: one open document, its path, the commands that move
/// text in and out of it, and the requested colour theme.
/// </summary>
/// <remarks>
/// This holds no UI types at all — not one control, not one window. The three
/// things that would have forced a UI dependency (picking a file, confirming a
/// discard) are interfaces handed in at construction, so the whole view model
/// runs headlessly in the test project.
///
/// The document itself is a single <see cref="string"/>. That is the honest
/// trade for a first version: simple, correct, and fast up to a few megabytes
/// of prose. Swapping in a gap buffer or a rope later is contained entirely
/// behind <see cref="Text"/>, because nothing else here knows how the text is
/// stored.
/// </remarks>
public partial class EditorViewModel : ViewModelBase {
    /// <summary>
    /// The typeface a new document starts in. Georgia: a system serif that
    /// carries Vietnamese diacritics cleanly and reads well at length, and so
    /// needs nothing bundled.
    /// </summary>
    public const string DefaultFontFamily = "Georgia";

    /// <summary>
    /// The point size a new document starts at. Matches the default Georgia
    /// size the sheet was designed around.
    /// </summary>
    public const double DefaultFontSize = 17;

    private readonly IFileService _files;
    private readonly IFileDialogService _dialogs;
    private readonly IConfirmDialogService _confirm;
    private readonly IFontCatalog _fonts;

    private string? _savedText = string.Empty;

    /// <summary>
    /// Creates a view model over the given services.
    /// </summary>
    /// <param name="files">File I/O.</param>
    /// <param name="dialogs">File pickers.</param>
    /// <param name="confirm">Discard confirmation prompt.</param>
    /// <param name="fonts">Installed typefaces, for the font picker.</param>
    public EditorViewModel(
        IFileService files,
        IFileDialogService dialogs,
        IConfirmDialogService confirm,
        IFontCatalog fonts) {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));

        // The recent list mutates by Insert and RemoveAt; the start screen
        // shows or hides it, so both edges have to be announced.
        RecentFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentFiles));

        RefreshCounts();
    }

    /// <summary>
    /// The document's full text. This is the single source of truth for the
    /// editor's content.
    /// </summary>
    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>
    /// The path of the file backing this document, or null for an unsaved
    /// document.
    /// </summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>
    /// True when the buffer differs from the last thing read or written.
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    /// A short status line for the user ("Opened x.txt", "Saved", ...).
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Ready";

    /// <summary>
    /// The requested colour theme. Resolved onto the application's theme
    /// variant by the window's code-behind.
    /// </summary>
    [ObservableProperty]
    private ThemeMode _themeMode = ThemeMode.System;

    /// <summary>
    /// The typeface the document is set in.
    /// </summary>
    /// <remarks>
    /// Applies to the whole document rather than to a selection, because the
    /// buffer is a plain string and has nowhere to record per-run formatting.
    /// That is the same reason this is spelled out as a string and not an
    /// <c>Avalonia.Media.FontFamily</c>: a typeface is a UI type, and the view
    /// model stays free of them. The binding converts.
    /// </remarks>
    [ObservableProperty]
    private string _fontFamily = DefaultFontFamily;

    /// <summary>
    /// The point size the document is set in. Applies to the whole document,
    /// for the same reason as <see cref="FontFamily"/>.
    /// </summary>
    [ObservableProperty]
    private double _fontSize = DefaultFontSize;

    /// <summary>
    /// Word count, refreshed on every edit.
    /// </summary>
    [ObservableProperty]
    private int _wordCount;

    /// <summary>
    /// Character count, refreshed on every edit.
    /// </summary>
    [ObservableProperty]
    private int _characterCount;

    /// <summary>
    /// Line count, refreshed on every edit.
    /// </summary>
    [ObservableProperty]
    private int _lineCount;

    /// <summary>
    /// The window title: "Shiranuix — filename", prefixed with a bullet when
    /// there are unsaved changes.
    /// </summary>
    public string Title =>
        IsDirty ? $"Shiranuix — • {DocumentName}" : $"Shiranuix — {DocumentName}";

    /// <summary>
    /// The file name shown in the title bar, or "Untitled" when the document
    /// has never been saved.
    /// </summary>
    public string DocumentName =>
        FilePath is null ? "Untitled" : Path.GetFileName(FilePath);

    /// <summary>
    /// The typefaces installed on this machine, for the font picker.
    /// </summary>
    /// <remarks>
    /// Computed rather than captured at construction, so that a typeface the
    /// machine does not have (a missing Georgia, say) is still offered by the
    /// picker instead of leaving the ComboBox with nothing to show for the
    /// document's actual font. Cheap: a few hundred names.
    /// </remarks>
    public IReadOnlyList<string> AvailableFonts {
        get {
            var families = _fonts.GetFamilies();

            if (string.IsNullOrWhiteSpace(FontFamily)
                || families.Any(f => string.Equals(f, FontFamily, StringComparison.Ordinal))) {
                return families;
            }

            var withCurrent = new List<string>(families.Count + 1) { FontFamily };
            withCurrent.AddRange(families);
            return withCurrent;
        }
    }

    private readonly List<double> _fontSizes = new() {
        8d, 9d, 10d, 11d, 12d, 14d, 16d, 17d, 18d, 20d,
        22d, 24d, 28d, 32d, 36d, 48d, 72d
    };

    /// <summary>
    /// The point sizes offered by the size picker.
    /// </summary>
    /// <remarks>
    /// A list rather than a fixed array because the size in use is kept in it.
    /// A size outside the usual ladder — 17, which is what this document was
    /// set in — would otherwise have no entry to select, and the picker would
    /// read blank instead of the document's real size. Word does the same.
    /// </remarks>
    public IReadOnlyList<double> FontSizes => _fontSizes;

    /// <summary>
    /// Keeps the size in use present in the picker.
    /// </summary>
    /// <param name="value">The new size.</param>
    partial void OnFontSizeChanged(double value) => EnsureSizeInList(value);

    /// <summary>
    /// Inserts <paramref name="value"/> into <see cref="FontSizes"/> in sorted
    /// order if it is not already there, and tells listeners to read again.
    /// </summary>
    /// <param name="value">The size to make room for.</param>
    private void EnsureSizeInList(double value) {
        var index = _fontSizes.IndexOf(value);
        if (index < 0) {
            index = _fontSizes.BinarySearch(value);
            if (index < 0) {
                index = ~index;
            }

            _fontSizes.Insert(index, value);
        }

        OnPropertyChanged(nameof(FontSizes));
    }

    /// <summary>
    /// The recent-files list shown on the start screen.
    /// </summary>
    public ObservableCollection<string> RecentFiles { get; } = new();

    /// <summary>
    /// True while <see cref="RecentFiles"/> holds anything, which is when the
    /// list is worth the vertical space it takes on the start screen.
    /// </summary>
    /// <remarks>
    /// A property rather than a Count comparison in the binding, because the
    /// binding language has no comparison operators and a converter would be
    /// more moving parts than the rule itself.
    /// </remarks>
    public bool HasRecentFiles => RecentFiles.Count > 0;

    /// <summary>
    /// True while the document holds no characters at all, which is when the
    /// start screen is drawn over the empty sheet.
    /// </summary>
    /// <remarks>
    /// Exposed as a property rather than bound as a negation of
    /// <see cref="Text"/> so the rule is stated once, in code, and can be
    /// asserted directly. Only <c>IsNullOrEmpty</c> counts as empty: a
    /// document containing a single space is something the user typed.
    /// </remarks>
    public bool IsStartVisible => string.IsNullOrEmpty(Text);

    /// <summary>
    /// Cycles the theme: System → Light → Dark → System.
    /// </summary>
    [RelayCommand]
    private void CycleTheme() {
        ThemeMode = ThemeMode switch {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }

    /// <summary>
    /// Creates a blank document, after confirming if the current one is dirty.
    /// </summary>
    [RelayCommand]
    private async Task NewDocumentAsync() {
        if (!await ConfirmDiscardAsync()) {
            return;
        }

        Text = string.Empty;
        FilePath = null;
        _savedText = string.Empty;
        IsDirty = false;
        StatusMessage = "New document";
        RefreshCounts();
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
    private Task SaveAsync() => FilePath is null ? SaveAsAsync() : SaveTo(FilePath);

    /// <summary>
    /// Always prompts for a path, then saves there.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsAsync() {
        var suggested = FilePath is null ? "Untitled.txt" : Path.GetFileName(FilePath);
        var path = await _dialogs.PickSaveFileAsync(suggested);
        if (path is null) {
            return;
        }

        await SaveTo(path);
    }

    /// <summary>
    /// Opens a file named on the command line.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <remarks>
    /// Public and prompt-free because the document it would be discarding was
    /// created a moment ago by the same launch and cannot be dirty. Kept
    /// separate from <see cref="OpenAsync"/> so the normal path still asks.
    /// </remarks>
    public void OpenFromCommandLine(string path) => OpenPath(path);

    /// <summary>
    /// Opens a path, falling back to a status message if the read fails.
    /// </summary>
    /// <param name="path">The file to open.</param>
    private void OpenPath(string path) {
        try {
            Text = _files.ReadAllText(path);
        } catch (Exception ex) {
            StatusMessage = $"Could not open: {ex.Message}";
            return;
        }

        FilePath = path;
        _savedText = Text;
        IsDirty = false;
        StatusMessage = $"Opened {Path.GetFileName(path)}";
        RefreshCounts();
        Remember(path);
    }

    /// <summary>
    /// Writes the buffer to disk and marks it clean.
    /// </summary>
    /// <param name="path">The file to write.</param>
    private async Task SaveTo(string path) {
        try {
            _files.WriteAllText(path, Text);
        } catch (Exception ex) {
            StatusMessage = $"Could not save: {ex.Message}";
            return;
        }

        // Only set the path after a successful write, so a failed save does not
        // silently re-point the document at a file it never reached.
        FilePath = path;
        _savedText = Text;
        IsDirty = false;
        StatusMessage = $"Saved {Path.GetFileName(path)}";
        Remember(path);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Recomputes dirty state and the status-bar counts after an edit.
    /// </summary>
    /// <param name="value">The new text.</param>
    partial void OnTextChanged(string value) {
        IsDirty = !string.Equals(value, _savedText, StringComparison.Ordinal);
        RefreshCounts();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsStartVisible));
    }

    /// <summary>
    /// Keeps the window title in step with the path.
    /// </summary>
    /// <param name="value">The new path.</param>
    partial void OnFilePathChanged(string? value) {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(DocumentName));
    }

    /// <summary>
    /// Keeps the window title in step with the dirty flag.
    /// </summary>
    /// <param name="value">The new dirty flag.</param>
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));

    /// <summary>
    /// Recomputes the three status-bar counters from the current text.
    /// </summary>
    private void RefreshCounts() {
        var stats = TextStatistics.Measure(Text);
        WordCount = stats.Words;
        CharacterCount = stats.Characters;
        LineCount = stats.Lines;
    }

    /// <summary>
    /// Asks the user whether to discard unsaved work. Clean documents do not
    /// prompt at all.
    /// </summary>
    /// <returns>True when it is safe to proceed.</returns>
    private async Task<bool> ConfirmDiscardAsync() {
        if (!IsDirty) {
            return true;
        }

        var name = FilePath is null ? "this document" : Path.GetFileName(FilePath);
        return await _confirm.ConfirmDiscardAsync(name);
    }

    /// <summary>
    /// Moves a path to the front of the recent-files list.
    /// </summary>
    /// <param name="path">The path to remember.</param>
    private void Remember(string path) {
        var index = RecentFiles.IndexOf(path);
        if (index >= 0) {
            RecentFiles.RemoveAt(index);
        }

        RecentFiles.Insert(0, path);

        // Cap the list so it cannot grow without bound over a long session.
        while (RecentFiles.Count > 10) {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }
}
