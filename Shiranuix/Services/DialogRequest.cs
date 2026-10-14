namespace Shiranuix.Services;

/// <summary>
/// What a dialog is asked to show: a title, a message, and the answers it
/// offers.
/// </summary>
/// <remarks>
/// A request is data. The window that draws it is the same window whatever the
/// answer type is, so a second kind of dialog is a second request and no new
/// markup.
/// </remarks>
/// <typeparam name="TResult">What the dialog answers with.</typeparam>
/// <param name="Title">The dialog's title.</param>
/// <param name="Message">What the dialog has to say.</param>
/// <param name="Buttons">The answers it offers, in the order they appear.</param>
public sealed record DialogRequest<TResult>(
    string Title,
    string Message,
    IReadOnlyList<DialogButton<TResult>> Buttons);
