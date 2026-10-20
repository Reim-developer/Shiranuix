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
/// lines and the way each of them stands — the very map the screen
/// drew from, so a centred heading reaches paper centred; the file is
/// the page, not the process. A choice that reached paper is what the
/// next export remembers — a choice that reached nothing is not.
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
            var face = await VeiledAsync(
                $"Writing {System.IO.Path.GetFileName(path)} to PDF…",
                () => WritePaperAsync(path, options));

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

    /// <summary>
    /// Composes the whole page and draws it onto paper, off the thread that
    /// draws the window.
    /// </summary>
    /// <param name="path">The paper to write.</param>
    /// <param name="options">The typeface and the size it is drawn in.</param>
    /// <returns>
    /// The typeface the paper landed in — a face short of a letter is
    /// swapped for one that carries it, and the writer is told which.
    /// </returns>
    /// <remarks>
    /// Both halves are the heavy kind. The page is composed out of the file
    /// — every line the window was not showing — and the sheets are drawn
    /// from that whole, so a document past what memory holds used to hold
    /// the frame still for as long as both of them took. The two leave as
    /// one span under one veil, so the writer meets a single wait and not
    /// two. The map is taken here, on the thread that owns it, the way a
    /// save takes its moves before the work goes: the pen off there asks
    /// about a map it was handed, never about the document.
    /// </remarks>
    private Task<string> WritePaperAsync(string path, Shiranuix.Models.PdfExportOptions options) {
        var lines = Document.Alignments;
        var title = System.IO.Path.GetFileNameWithoutExtension(path);

        return Task.Run(async () => {
            var text = await Document.WholeTextAsync();

            return SheetPdfWriter.Write(
                path,
                title,
                text,
                options.FontFamily,
                options.FontSize,
                lines,
                TypographyState.LineHeightFactor);
        });
    }
}
