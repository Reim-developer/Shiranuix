namespace Shiranuix.Services.Dialogs;

/// <summary>
/// The real picture picker: the app's own file picker, asked for a picture
/// and nothing else.
/// </summary>
/// <remarks>
/// The package asks for a picture by a name of its own; the app is the
/// one that knows how a file is chosen on the machine it is running on,
/// and what kinds of file a picture is.
/// </remarks>
public sealed class WindowImagePicker : Shiranuix.Graphs.Dialogs.IImagePicker {
    private readonly IFileDialogService _files;

    /// <summary>
    /// Creates the picker over the app's own file picker.
    /// </summary>
    /// <param name="files">The file picker the picture is chosen with.</param>
    public WindowImagePicker(IFileDialogService files) =>
        _files = files ?? throw new ArgumentNullException(nameof(files));

    /// <inheritdoc />
    public async Task<string?> PickAsync(string title) =>
        await _files.PickImageFileAsync(title);
}
