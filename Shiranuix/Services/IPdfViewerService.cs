using Shiranuix.ViewModels;

namespace Shiranuix.Services;

/// <summary>
/// Reads a paper beside the writing: the window that shows a PDF is not
/// asked of the document, because a PDF is not the document.
/// </summary>
/// <remarks>
/// Abstracted like the file dialogs, for the same reason: the routing runs
/// headlessly in tests, where the reader is a fake whose receipts are heard.
/// </remarks>
public interface IPdfViewerService {
    /// <summary>
    /// Shows the paper the file holds.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>What came of it, told for the status line to render.</returns>
    DocumentOutcome Show(string path);
}
