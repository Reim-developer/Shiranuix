using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Shiranuix.Services;

/// <summary>
/// The real file pickers, backed by the window's <see cref="TopLevel.StorageProvider"/>.
/// </summary>
/// <remarks>
/// The window arrives as a <see cref="Func{Window}"/> rather than as a window.
/// The container builds this service for the view model, and builds the window
/// from that view model, so asking for the window *at construction time* would
/// send the container round its own circle. The delegate is called when a
/// dialog is actually shown, by which point the window exists — and the same
/// break in the circle is what lets the container construct everything else
/// here from constructor arguments alone.
/// </remarks>
public sealed class WindowFileDialogService : IFileDialogService {
    private readonly Func<Window> _window;

    /// <summary>
    /// Creates the service over a way of asking for the window.
    /// </summary>
    /// <param name="window">Resolves the window that owns the picker.</param>
    public WindowFileDialogService(Func<Window> window) =>
        _window = window ?? throw new ArgumentNullException(nameof(window));

    /// <inheritdoc />
    public async Task<string?> PickOpenFileAsync() {
        var files = await _window().StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "Open document",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.TextPlain, FilePickerFileTypes.All }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string suggestedName) {
        var file = await _window().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Save document",
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            FileTypeChoices = new[] { FilePickerFileTypes.TextPlain }
        });

        return file?.TryGetLocalPath();
    }
}
