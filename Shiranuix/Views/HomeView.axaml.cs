using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the home screen.
/// </summary>
/// <remarks>
/// Only handles the one thing that must live in the view: opening the native
/// folder picker. The chosen path is handed to the view model's
/// <c>OpenFolderCommand</c>; all state and decisions stay in
/// <see cref="HomeViewModel"/>.
/// </remarks>
public partial class HomeView : UserControl {
    /// <summary>
    /// Creates the control.
    /// </summary>
    public HomeView() {
        InitializeComponent();
    }

    /// <summary>
    /// Opens a folder picker and, on selection, tells the view model to open
    /// that folder.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">Event args.</param>
    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e) {
        if (DataContext is not HomeViewModel viewModel) {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider) {
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await provider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions {
                Title = "Open a folder of images",
                AllowMultiple = false
            });

        if (folders.Count == 0) {
            return;
        }

        // Resolve a concrete filesystem path so the scanner can read it.
        string? path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) {
            viewModel.OpenFolderCommand.Execute(path);
        }
    }
}
