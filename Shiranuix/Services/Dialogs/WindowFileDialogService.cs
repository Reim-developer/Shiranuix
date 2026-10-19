using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Shiranuix.Services.Dialogs;

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
    /// <summary>
    /// How the app's own file is named in a picker: the pattern that finds
    /// it, and the words a user picks it by.
    /// </summary>
    private static readonly FilePickerFileType Shiranux = new("Shiranuix document") {
        Patterns = new[] { "*.shiranux" }
    };

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
            FileTypeFilter = new[] {
                FilePickerFileTypes.TextPlain, Shiranux, FilePickerFileTypes.Pdf, FilePickerFileTypes.All
            }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// What the dialog offers follows the name it was handed: a PDF is a
    /// PDF and nothing else, a <c>.shiranux</c> is the app's own file with
    /// plain text beside it, and anything else — which includes every new
    /// document, prefilled as <c>Untitled.txt</c> — opens on plain text
    /// first with the app's own file one choice down. The default
    /// extension comes from the name itself, so picking a type the user
    /// already named never renames their file behind their back.
    /// </remarks>
    public async Task<string?> PickSaveFileAsync(string suggestedName) {
        var extension = Path.GetExtension(suggestedName);
        var pdf = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        var own = extension.Equals(".shiranux", StringComparison.OrdinalIgnoreCase);

        FilePickerFileType[] choices;
        string fallback;

        if (pdf) {
            choices = new[] { FilePickerFileTypes.Pdf };
            fallback = "pdf";
        } else if (own) {
            choices = new[] { Shiranux, FilePickerFileTypes.TextPlain };
            fallback = "shiranux";
        } else {
            choices = new[] { FilePickerFileTypes.TextPlain, Shiranux };
            fallback = "txt";
        }

        var file = await _window().StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "Save document",
            SuggestedFileName = suggestedName,
            DefaultExtension = extension.Length > 1 ? extension[1..] : fallback,
            FileTypeChoices = choices
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync() {
        var folders = await _window().StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
            Title = "Choose a folder"
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
