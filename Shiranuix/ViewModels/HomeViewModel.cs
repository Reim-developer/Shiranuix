namespace Shiranuix.ViewModels;

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Models;
using Shiranuix.Services;

/// <summary>
/// State and behaviour for the home (recent folders) screen.
/// </summary>
/// <remarks>
/// Contains no UI control types, so it is unit-testable headlessly. The view
/// observes the exposed observable state and forwards user actions to the
/// generated commands.
/// </remarks>
public partial class HomeViewModel : ViewModelBase {
    private readonly IFolderScanner _scanner;
    private readonly ILibraryRepository _repository;

    /// <summary>
    /// Recent folders, most recently opened first. Bound to the home grid.
    /// </summary>
    public ObservableCollection<LibraryEntry> Recent { get; } = new();

    /// <summary>Status line shown under the header.</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Whether there are no recent folders (drives the empty state).</summary>
    public bool IsEmpty => Recent.Count == 0;

    /// <summary>
    /// Raised when a folder has been successfully scanned and should be
    /// opened for reading. The shell subscribes to navigate to the reader.
    /// </summary>
    public event EventHandler<Chapter>? ChapterRequested;

    /// <summary>
    /// Creates a home view model.
    /// </summary>
    /// <param name="scanner">Turns a folder into a chapter.</param>
    /// <param name="repository">Backs the recent list.</param>
    public HomeViewModel(IFolderScanner scanner, ILibraryRepository repository) {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        RefreshRecent();
    }

    /// <summary>
    /// Scans <paramref name="folder"/> and, on success, records it and raises
    /// <see cref="ChapterRequested"/>.
    /// </summary>
    /// <remarks>
    /// On failure the status message is updated and the event is not raised,
    /// so the user stays on the home screen.
    /// </remarks>
    /// <param name="folder">Folder path the user chose.</param>
    [RelayCommand]
    private void OpenFolder(string? folder) {
        if (string.IsNullOrWhiteSpace(folder)) {
            Status = "No folder selected.";
            return;
        }

        Chapter? chapter = _scanner.Scan(folder);
        if (chapter is null) {
            Status = $"No images found in {Path.GetFileName(folder)}.";
            return;
        }

        _repository.RecordOpen(LibraryEntry.From(chapter, DateTimeOffset.Now));
        RefreshRecent();
        Status = $"Opened {chapter.Title} ({chapter.PageCount} pages).";
        ChapterRequested?.Invoke(this, chapter);
    }

    /// <summary>
    /// Re-reads the recent list from the repository.
    /// </summary>
    private void RefreshRecent() {
        Recent.Clear();
        foreach (LibraryEntry entry in _repository.GetRecent()) {
            Recent.Add(entry);
        }

        // Let the view toggle the empty-state placeholder.
        OnPropertyChanged(nameof(IsEmpty));
    }
}
