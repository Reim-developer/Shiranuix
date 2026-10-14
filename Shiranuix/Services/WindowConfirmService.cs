using Shiranuix.Services;

namespace Shiranuix.Services;

/// <summary>
/// The blocking prompt shown before a document with unsaved changes is thrown
/// away.
/// </summary>
/// <remarks>
/// This class no longer builds any interface. It knows what a discard is —
/// which question to ask and which answer is which — and hands that to
/// <see cref="IDialogService"/> as a request, so the window, the buttons and
/// the wording of the markup are shared by every other dialog.
///
/// "Keep editing" is both the default and the cancel answer: Enter and Escape
/// both keep the work, because losing a chapter to a stray keypress is the
/// failure worth designing against.
/// </remarks>
public sealed class WindowConfirmService : IConfirmDialogService {
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Creates the service over the dialogs it shows through.
    /// </summary>
    /// <param name="dialogs">The service that shows dialogs.</param>
    public WindowConfirmService(IDialogService dialogs) =>
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

    /// <inheritdoc />
    public async Task<bool> ConfirmDiscardAsync(string fileName) {
        var answer = await _dialogs.ShowAsync(new DialogRequest<bool>(
            "Unsaved changes",
            $"\"{fileName}\" has unsaved changes.\nDiscard them?",
            new[] {
                new DialogButton<bool>("Keep editing", false, IsDefault: true, IsCancel: true),
                new DialogButton<bool>("Discard", true)
            }));

        // A dismissed dialog keeps the work.
        return answer == true;
    }
}
