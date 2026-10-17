using System.IO;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Services.Pdf;

namespace Shiranuix.ViewModels;

/// <summary>
/// The part of <see cref="EditorViewModel"/> that answers for writing the
/// page out to paper.
/// </summary>
/// <remarks>
/// <para>
/// A document is told where to live first, the same as saving: nothing is
/// written until the writer has named the file. The PDF carries the page's
/// own typeface, size and alignment — everything the sheet shows that a
/// written page can show — and its words unchanged; the file is the page,
/// not the process.
/// </para>
/// <para>
/// A failure to write travels to the status line, the way every other
/// operation's does: the window keeps standing, the page keeps its words,
/// and the writer is told why nothing reached the disk.
/// </para>
/// </remarks>
public partial class EditorViewModel {
    /// <summary>Writes the page to a PDF, wherever the writer points.</summary>
    /// <remarks>
    /// The PDF is written after the answer rather than during the ask: a
    /// dialog closed with nothing in it writes nothing.
    /// </remarks>
    [RelayCommand]
    private async Task ExportPdfAsync() {
        var suggested = Document.Path is null
            ? "Untitled.pdf"
            : System.IO.Path.ChangeExtension(Document.Name, ".pdf");
        var path = await _dialogs.PickSaveFileAsync(suggested);

        if (path is null) {
            return;
        }

        try {
            SheetPdfWriter.Write(
                path,
                System.IO.Path.GetFileNameWithoutExtension(path),
                Document.Text,
                Typography.InstalledFontFamily,
                Typography.FontSize,
                Typography.TextAlign,
                TypographyState.LineHeightFactor);

            StatusMessage = $"Wrote {System.IO.Path.GetFileName(path)} to PDF.";
        } catch (Exception flaw) {
            StatusMessage = $"Could not write PDF: {flaw.Message}";
        }
    }
}
