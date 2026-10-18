using Shiranuix.Models;
using Shiranuix.Services.Dialogs;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An export dialog that answers without showing anything.
/// </summary>
public sealed class FakeExportPdfDialog : IExportPdfDialogService {
    /// <summary>
    /// Options the dialog hands back; null models the user cancelling.
    /// </summary>
    public PdfExportOptions? Result { get; set; }

    /// <summary>
    /// What each ask was offered, in order: the options the dialog was to
    /// open on, the fonts it was to offer and the sizes beside them — so a
    /// test can assert the chooser opened on the remembered choice and was
    /// primed with the page's own pickers.
    /// </summary>
    public List<(PdfExportOptions Current, IReadOnlyList<string> Fonts, IReadOnlyList<double> Sizes)> Asked { get; } = new();

    /// <inheritdoc />
    public Task<PdfExportOptions?> AskAsync(
        PdfExportOptions current,
        IReadOnlyList<string> fonts,
        IReadOnlyList<double> sizes) {
        Asked.Add((current, fonts, sizes));
        return Task.FromResult(Result);
    }
}
