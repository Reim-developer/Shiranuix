using Avalonia.Controls;
using Shiranuix.Graphs.Dialogs;

namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Draws the relationship map's strike-out question with the same dialog
/// every other warning in the application is drawn in, over the window the
/// map hands to it.
/// </summary>
/// <remarks>
/// The prompt itself — the words, the two buttons, the answer pressed back
/// — is the application's own and shared by every confirmation it makes.
/// What crosses from the map is only where the thing must stand: the map's
/// window, so that a strike-out confirmed leaves the writer in the map
/// rather than waking the writing behind it.
/// </remarks>
public sealed class WindowConfirmQuestion : IConfirmQuestion {
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Creates the question over the service every prompt here is shown through.
    /// </summary>
    /// <param name="dialogs">Where the application draws its prompts.</param>
    public WindowConfirmQuestion(IDialogService dialogs) =>
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    /// <inheritdoc />
    public async Task<bool> Ask(Window owner, string title, string message, string confirmLabel) {
        var answer = await _dialogs.ShowAsync(new DialogRequest<bool>(
            title,
            message,
            [
                new DialogButton<bool>("Keep", false, IsDefault: true, IsCancel: true),
                new DialogButton<bool>(confirmLabel, true)
            ]), owner);

        // A dismissed prompt strikes nothing out.
        return answer == true;
    }
}
