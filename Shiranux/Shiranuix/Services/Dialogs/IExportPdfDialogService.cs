using Shiranuix.Models;

namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Asks the export dialog's question: the typeface and size the PDF is drawn
/// in, offered over the fonts and sizes the page's own pickers carry.
/// </summary>
/// <remarks>
/// Abstracted away from any one window because the export flow runs
/// headlessly in tests: what the view model needs is an answer, and what it
/// hears in a test is whatever the fake was told to answer. The dialog is a
/// chooser, not a file picker — the file's own question stays with
/// <see cref="IFileDialogService"/>, asked after.
/// </remarks>
public interface IExportPdfDialogService {
    /// <summary>
    /// Shows the export dialog.
    /// </summary>
    /// <param name="current">What the dialog opens on: the shape of the last export, or the page's own.</param>
    /// <param name="fonts">The typefaces offered, as the machine carries them.</param>
    /// <param name="sizes">The sizes offered, as the size picker carries them.</param>
    /// <returns>The shape answered, or null when the dialog was cancelled.</returns>
    Task<PdfExportOptions?> AskAsync(
        PdfExportOptions current,
        IReadOnlyList<string> fonts,
        IReadOnlyList<double> sizes);
}
