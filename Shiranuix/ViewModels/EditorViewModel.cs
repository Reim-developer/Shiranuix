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
/// This holds no UI types at all — not one control, not one window. The things
/// that would have forced a UI dependency (picking a file, confirming a
/// discard, enumerating installed typefaces) and the things that would have
/// forced a database (settings, recent files, font history) are interfaces
/// handed in at construction, so the whole view model runs headlessly in the
/// test project.
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
    private readonly IUserSettings _settings;
    private readonly IRecentFilesStore _recentFiles;
    private readonly IFontHistoryStore _fontHistory;

    private readonly DirtyTracker<string> _document = new(string.Empty);
    private readonly FontSizeLadder _sizeLadder = new();

    /// <summary>
    /// Creates a view model over the given services.
    /// </summary>
    /// <param name="files">File I/O.</param>
    /// <param name="dialogs">File pickers.</param>
    /// <param name="confirm">Discard confirmation prompt.</param>
    /// <param name="fonts">Installed typefaces, for the font picker.</param>
    /// <param name="settings">The user's stored typography and theme.</param>
    /// <param name="recentFiles">The stored recent-files list.</param>
    /// <param name="fontHistory">The stored typeface and size history.</param>
    public EditorViewModel(
        IFileService files,
        IFileDialogService dialogs,
        IConfirmDialogService confirm,
        IFontCatalog fonts,
        IUserSettings settings,
        IRecentFilesStore recentFiles,
        IFontHistoryStore fontHistory) {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _recentFiles = recentFiles ?? throw new ArgumentNullException(nameof(recentFiles));
        _fontHistory = fontHistory ?? throw new ArgumentNullException(nameof(fontHistory));

        // Restored straight into the fields rather than the properties: a
        // restored choice is not a change, and going through the property
        // would write it straight back out and record a font "use" for a
        // typeface the user never picked this session.
        var stored = _settings.Load();
        _fontFamily = string.IsNullOrWhiteSpace(stored?.FontFamily) ? DefaultFontFamily : stored.FontFamily;
        _fontSize = stored is { FontSize: > 0 } ? stored.FontSize : DefaultFontSize;
        _themeMode = stored?.ThemeMode ?? ThemeMode.System;

        // A restored size outside the usual run still joins the picker's
        // ladder, so the size the document was left in is offered rather than
        // reading blank. The sizes used lately join it too, so the picker
        // opens on the run of sizes this writer actually works in.
        foreach (var size in _fontHistory.RecentSizes()) {
            _sizeLadder.Ensure(size);
        }

        _sizeLadder.Ensure(_fontSize);
        OnPropertyChanged(nameof(FontSizes));

        // The store hands back newest first, which is the order the start
        // screen shows.
        foreach (var path in _recentFiles.Load()) {
            RecentFiles.Add(path);
        }

        // The recent list mutates by Insert and RemoveAt; the start screen
        // shows or hides it, so both edges have to be announced.
        RecentFiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecentFiles));

        // The document's dirty state and status-bar counts are recomputed
        // whenever the text moves — the tracker is the single place that knows
        // when that happens.
        _document.Changed += (_, _) => {
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(IsStartVisible));
            RefreshCounts();
        };

        RefreshCounts();
    }

    /// <summary>
    /// The path of the file backing this document, or null for an unsaved
    /// document.
    /// </summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>
    /// True when the buffer differs from the last thing read or written.
    /// </summary>
    /// <remarks>
    /// Read from a <see cref="DirtyTracker{T}"/> rather than stored here, so the
    /// flag and the text it describes cannot drift apart: the tracker compares
    /// against the last point the text was saved at, and nothing else has to
    /// know that point exists.
    /// </remarks>
    public bool IsDirty => _document.IsDirty;

    /// <summary>
    /// The document's full text. This is the single source of truth for the
    /// editor's content.
    /// </summary>
    /// <remarks>
    /// Held in a <see cref="DirtyTracker{T}"/> so the dirty flag and the counts
    /// that follow the text are recomputed in one place. Setting it is the only
    /// way the buffer moves; everything else — the save point, the status
    /// message — reacts to that.
    /// </remarks>
    public string Text {
        get => _document.Value;
        set => _document.Value = value;
    }

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
    /// The typefaces offered by the font picker.
    /// </summary>
    /// <remarks>
    /// Recently used first: the typefaces a writer actually works in, ahead of
    /// the alphabetical run of everything installed. Both lists are read
    /// rather than captured at construction, so the picker reflects the
    /// machine as it is and the history as it stands when it is opened.
    /// </remarks>
    public IReadOnlyList<string> AvailableFonts {
        get {
            var picked = _fontHistory.RecentFamilies().ToList();
            var ordered = new List<string>(picked);

            // The installed ones follow, minus any the history already led
            // with, so no typeface appears twice.
            ordered.AddRange(_fonts.GetFamilies().Where(family => !ordered.Contains(family)));

            // A typeface the machine does not have (a missing Georgia, say) is
            // still offered, so the document's real font is never absent from
            // the picker.
            if (!string.IsNullOrWhiteSpace(FontFamily) && !ordered.Contains(FontFamily)) {
                ordered.Add(FontFamily);
            }

            return ordered;
        }
    }

    /// <summary>
    /// The point sizes offered by the size picker.
    /// </summary>
    public IReadOnlyList<double> FontSizes => _sizeLadder.Sizes;

    /// <summary>
    /// Keeps the size in use present in the picker.
    /// </summary>
    /// <param name="value">The new size.</param>
    partial void OnFontSizeChanged(double value) {
        _sizeLadder.Ensure(value);
        OnPropertyChanged(nameof(FontSizes));

        // A size the user picked is part of the history the pickers are
        // filled from, and part of the settings the next session restores.
        _settings.Save(CurrentSettings());
        _fontHistory.Remember(FontFamily, value);
        OnPropertyChanged(nameof(AvailableFonts));
    }

    /// <summary>
    /// How many paths the recent-files list keeps. Matches what the start
    /// screen has room for, so the list cannot outgrow the space it is given.
    /// </summary>
    private const int RecentFilesLimit = 10;

    /// <summary>
    /// The recent-files list shown on the start screen.
    /// </summary>
    /// <remarks>
    /// A <see cref="RecentList{T}"/> so the "newest first, capped" rule lives in
    /// the list itself rather than in every method that touches it.
    /// </remarks>
    public RecentList<string> RecentFiles { get; } = new(RecentFilesLimit);

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

        FilePath = null;
        _document.Reset(string.Empty);
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
        _document.MarkSaved();
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
        _document.MarkSaved();
        StatusMessage = $"Saved {Path.GetFileName(path)}";
        Remember(path);

        await Task.CompletedTask;
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
    /// Stores the new typeface and records it in the history.
    /// </summary>
    /// <param name="value">The new typeface.</param>
    /// <remarks>
    /// The picker is told to read again because the history it leads with has
    /// just changed.
    /// </remarks>
    partial void OnFontFamilyChanged(string value) {
        _settings.Save(CurrentSettings());
        _fontHistory.Remember(value, FontSize);
        OnPropertyChanged(nameof(AvailableFonts));
    }

    /// <summary>
    /// Stores the new theme.
    /// </summary>
    /// <param name="value">The new theme.</param>
    partial void OnThemeModeChanged(ThemeMode value) => _settings.Save(CurrentSettings());

    /// <summary>
    /// The current choices, in the shape the store persists them.
    /// </summary>
    /// <returns>The typography and theme as one settings row.</returns>
    private UserSettings CurrentSettings() => new() {
        FontFamily = FontFamily,
        FontSize = FontSize,
        ThemeMode = ThemeMode
    };

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
        RecentFiles.MoveToFront(path);

        // The store keeps its own copy, capped to the same ten, so the list
        // outlives the session that built it.
        _recentFiles.Remember(path);
    }
}
