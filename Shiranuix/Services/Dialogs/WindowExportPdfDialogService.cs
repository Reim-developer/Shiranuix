using Avalonia.Controls;
using Shiranuix.Models;
using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Shows the export dialog as a window over the application's main window.
/// </summary>
/// <remarks>
/// The same walk as the other window dialogs: the window arrives as a
/// <see cref="Func{Window}"/> rather than captured, because the container is
/// still building the window when it builds this. The chooser window is made
/// new each ask — exports repeat, and a window closed with an answer cannot
/// be asked again — and is closed with the options it gathered, or with
/// nothing when it was cancelled.
/// </remarks>
public sealed class WindowExportPdfDialogService : IExportPdfDialogService {
    private readonly Func<Window> _window;

    /// <summary>
    /// Creates the service over a way of asking for the window.
    /// </summary>
    /// <param name="window">Resolves the window that owns the dialog.</param>
    public WindowExportPdfDialogService(Func<Window> window) =>
        _window = window ?? throw new ArgumentNullException(nameof(window));

    /// <inheritdoc />
    public async Task<PdfExportOptions?> AskAsync(
        PdfExportOptions current,
        IReadOnlyList<string> fonts,
        IReadOnlyList<double> sizes) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(sizes);

        var dialog = new Views.ExportPdfDialog(
            new ExportPdfDialogViewModel(current, fonts, sizes));

        return await dialog.ShowDialog<PdfExportOptions?>(_window());
    }
}
