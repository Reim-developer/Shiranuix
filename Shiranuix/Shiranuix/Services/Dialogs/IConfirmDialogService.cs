namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Asks the user a yes/no question that blocks further work.
/// </summary>
/// <remarks>
/// Separate from <see cref="IFileDialogService"/> because the answers are not
/// interchangeable: a file picker returns a path, this returns consent. Keeping
/// them apart means a test can stub one without stubbing the other.
/// </remarks>
public interface IConfirmDialogService {
    /// <summary>
    /// Asks whether to discard unsaved work.
    /// </summary>
    /// <param name="fileName">The file about to be discarded, for the prompt.</param>
    /// <returns>True when the user chose to discard.</returns>
    Task<bool> ConfirmDiscardAsync(string fileName);
}
