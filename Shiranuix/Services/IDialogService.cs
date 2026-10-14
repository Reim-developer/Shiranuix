namespace Shiranuix.Services;

/// <summary>
/// Shows a dialog and reports the answer it was given.
/// </summary>
/// <remarks>
/// One interface for every dialog, whatever the answer: the buttons and what
/// they answer are described in the request, and the window that draws them is
/// decided by the implementation. Nothing here knows what a "discard" is.
/// </remarks>
public interface IDialogService {
    /// <summary>
    /// Shows a dialog over the window it is given, and waits for the answer.
    /// </summary>
    /// <typeparam name="TResult">What the dialog answers with.</typeparam>
    /// <param name="request">What to show.</param>
    /// <returns>
    /// The answer pressed, or null when the window was closed without one.
    /// </returns>
    Task<TResult?> ShowAsync<TResult>(DialogRequest<TResult> request);
}
