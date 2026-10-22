using Avalonia.Controls;

namespace Shiranuix.Services.Dialogs;

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
    /// Shows a dialog over the application's own window, and waits for the answer.
    /// </summary>
    /// <typeparam name="TResult">What the dialog answers with.</typeparam>
    /// <param name="request">What to show.</param>
    /// <returns>
    /// The answer pressed, or null when the window was closed without one.
    /// </returns>
    Task<TResult?> ShowAsync<TResult>(DialogRequest<TResult> request);

    /// <summary>
    /// Shows a dialog over a window other than the application's own, and
    /// waits for the answer — the relationship map's window, for one: a
    /// prompt owned by the writing behind it wakes that writing and hands
    /// it the focus back, which is how a question about the map ends with
    /// the writer standing in the editor.
    /// </summary>
    /// <typeparam name="TResult">What the dialog answers with.</typeparam>
    /// <param name="request">What to show.</param>
    /// <param name="owner">The window to show it over.</param>
    /// <returns>
    /// The answer pressed, or null when the window was closed without one.
    /// </returns>
    Task<TResult?> ShowAsync<TResult>(DialogRequest<TResult> request, Window owner);
}
