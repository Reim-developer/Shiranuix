namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Asks the user to choose a file to open or save.
/// </summary>
/// <remarks>
/// Abstracted away from <c>StorageProvider</c> because that type only exists
/// on a live window, which would make <see cref="ViewModels.EditorViewModel"/>
/// untestable. The real implementation is registered in the composition root
/// (<c>AddShiranuix</c>) and injected at startup, the same as every other
/// service the view model depends on.
/// </remarks>
public interface IFileDialogService {
    /// <summary>
    /// Asks for a file to open.
    /// </summary>
    /// <returns>The chosen path, or null if the user cancelled.</returns>
    Task<string?> PickOpenFileAsync();

    /// <summary>
    /// Asks for a path to save to.
    /// </summary>
    /// <param name="suggestedName">The name to prefill the dialog with.</param>
    /// <returns>The chosen path, or null if the user cancelled.</returns>
    Task<string?> PickSaveFileAsync(string suggestedName);

    /// <summary>
    /// Asks for a folder, which is a different question from asking for a
    /// file: a file tree needs somewhere to stand, not something to open.
    /// </summary>
    /// <returns>The chosen folder, or null if the user cancelled.</returns>
    Task<string?> PickFolderAsync();

    /// <summary>
    /// Asks for a picture, which is another question again: a character
    /// wears one, and the app asks for pictures of a certain kind.
    /// </summary>
    /// <param name="title">The words the picker is opened with.</param>
    /// <returns>The chosen path, or null if the user cancelled.</returns>
    Task<string?> PickImageFileAsync(string title);
}
