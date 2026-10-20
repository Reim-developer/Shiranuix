using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Narika.Services.Install;

/// <summary>
/// The real folder picker, backed by the window's
/// <see cref="TopLevel.StorageProvider"/>.
/// </summary>
/// <remarks>
/// The window arrives as a <see cref="Func{Window}"/> rather than as a
/// window. The container builds this service for the view model, and builds
/// the window from that view model, so asking for the window *at
/// construction time* would send the container round its own circle. The
/// delegate is called when a dialog is actually shown, by which point the
/// window exists — the same break in the circle the editor's own pickers
/// are built on.
/// </remarks>
public sealed class WindowFolderPickerService : IFolderPickerService {
    private readonly Func<Window> _window;

    /// <summary>
    /// Creates the service over a way of asking for the window.
    /// </summary>
    /// <param name="window">Resolves the window that owns the picker.</param>
    public WindowFolderPickerService(Func<Window> window) =>
        _window = window ?? throw new ArgumentNullException(nameof(window));

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync() {
        var folders = await _window().StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions {
                Title = "Choose where Shiranuix will be installed"
            });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
