namespace Shiranuix.Services;

/// <summary>
/// Asks the user to choose a file to open or save.
/// </summary>
/// <remarks>
/// Abstracted away from <c>StorageProvider</c> because that type only exists
/// on a live window, which would make <see cref="ViewModels.EditorViewModel"/>
/// untestable. The real implementation lives in the window's code-behind and
/// is injected at startup.
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
}
