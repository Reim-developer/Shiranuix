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
/// The export asks before it writes: first its own question — the typeface
/// and the size the paper is drawn in, offered over what the machine carries
/// and opened on what the last export remembered — then the file's question,
/// the same ask a save makes. Nothing is written until both are answered: a
/// dialog closed with nothing in it writes nothing.
/// </para>
/// <para>
/// The PDF carries the answered typeface and size, and the page's own
/// alignment and words; the file is the page, not the process. A choice that
/// reached paper is what the next export remembers — a choice that reached
/// nothing is not.
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
    /// The PDF is written after the answers rather than during the asks: a
    /// cancelled chooser or a cancelled file picker writes nothing, and a
    /// choice is remembered only when its paper actually landed.
    /// </remarks>
    [RelayCommand]
    private async Task ExportPdfAsync() {
        var options = await _exportDialogs.AskAsync(
            _pdfOptions.Current(Typography),
            Typography.AvailableFonts,
            Typography.FontSizes);

        if (options is null) {
            return;
        }

        var suggested = Document.Path is null
            ? "Untitled.pdf"
            : System.IO.Path.ChangeExtension(Document.Name, ".pdf");
        var path = await _dialogs.PickSaveFileAsync(suggested);

        if (path is null) {
            return;
        }

        try {
            var face = SheetPdfWriter.Write(
                path,
                System.IO.Path.GetFileNameWithoutExtension(path),
                Document.WholeText(),
                options.FontFamily,
                options.FontSize,
                Typography.TextAlign,
                TypographyState.LineHeightFactor);

            // The choice that reached paper is the one the next export opens
            // on; the row is written as it stands with only this laid over.
            _pdfOptions.Remember(options);

            StatusMessage = face == options.FontFamily
                ? $"Wrote {System.IO.Path.GetFileName(path)} to PDF."
                : $"Wrote {System.IO.Path.GetFileName(path)} to PDF in {face} — {options.FontFamily} doesn't carry every letter.";
        } catch (Exception flaw) {
            StatusMessage = $"Could not write PDF: {flaw.Message}";
        }
    }
}
