using Avalonia;
using Avalonia.Controls;
using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Shows dialogs as windows over the application's main window.
/// </summary>
/// <remarks>
/// The window that draws them is <see cref="Views.DialogWindow"/>, whose
/// markup holds nothing about any one dialog: the title, the message and the
/// buttons all come from the request through a view model. So this knows how
/// to show a dialog, and nothing about what a dialog is for — a discard
/// prompt and a file picker are the same walk to here.
///
/// The owner window is handed in as a <see cref="Func{Window}"/> rather than
/// captured at construction, for the same reason as the other two services:
/// the container is still building the window when it builds this.
/// </remarks>
public sealed class WindowDialogService : IDialogService {
    private readonly Func<Window> _window;

    /// <summary>
    /// Creates the service over a way of asking for the window.
    /// </summary>
    /// <param name="window">Resolves the window to centre the dialog on.</param>
    public WindowDialogService(Func<Window> window) =>
        _window = window ?? throw new ArgumentNullException(nameof(window));

    /// <inheritdoc />
    public async Task<TResult?> ShowAsync<TResult>(DialogRequest<TResult> request) {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new Views.DialogWindow();
        var viewModel = new DialogViewModel<TResult>(request, result => dialog.Close(result));
        dialog.DataContext = viewModel;
        dialog.Title = request.Title;

        return await dialog.ShowDialog<TResult?>(_window());
    }
}
