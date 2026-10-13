using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Shiranuix.Services;

/// <summary>
/// The real file pickers, backed by the window's <see cref="TopLevel.StorageProvider"/>.
/// </summary>
public sealed class WindowFileDialogService : IFileDialogService {
    private readonly Window _window;

    /// <summary>
    /// Creates the service over a window.
    /// </summary>
    /// <param name="window">The window that owns the picker.</param>
    public WindowFileDialogService(Window window) =>
        _window = window ?? throw new ArgumentNullException(nameof(window));

    /// <inheritdoc />
    public async Task<string?> PickOpenFileAsync() {
        var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Open document",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.TextPlain, FilePickerFileTypes.All }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string suggestedName) {
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Save document",
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            FileTypeChoices = new[] { FilePickerFileTypes.TextPlain }
        });

        return file?.TryGetLocalPath();
    }
}
