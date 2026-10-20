namespace Narika.Services.Install;

/// <summary>
/// The folder picker the destination page asks for.
/// </summary>
public interface IFolderPickerService {
    /// <summary>
    /// Shows the machine's folder picker and returns the folder that was
    /// chosen, or nothing when the person changed their mind.
    /// </summary>
    Task<string?> PickFolderAsync();
}
