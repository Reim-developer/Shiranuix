using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;

namespace Shiranuix.ViewModels.Dialogs;

/// <summary>
/// The export dialog's own state: the typeface and size the paper is to be
/// drawn in, over the fonts and sizes it is offered.
/// </summary>
/// <remarks>
/// The window that draws this carries no judgement of its own: what was
/// chosen arrives as <see cref="PdfExportOptions"/> through the button, and
/// the service closes the window with it. Nothing here reaches the document
/// or the typography — it is the one question the export asks.
/// </remarks>
public sealed partial class ExportPdfDialogViewModel : ObservableObject {
    /// <summary>The current font when the dialog is opened.</summary>
    /// <remarks>
    /// What the last export was drawn in, or the page's own where the export
    /// dialog has never been answered — the dialog opens on it.
    /// </remarks>
    [ObservableProperty]
    private string _fontFamily;

    /// <summary>The current size when the dialog is opened.</summary>
    [ObservableProperty]
    private double _fontSize;

    /// <summary>
    /// Creates the dialog's state over the options it opens on and what it
    /// offers instead.
    /// </summary>
    /// <param name="current">The options to prefill with.</param>
    /// <param name="fonts">The typefaces offered, as the machine carries them.</param>
    /// <param name="sizes">The sizes offered, as the size picker carries them.</param>
    public ExportPdfDialogViewModel(
        PdfExportOptions current,
        IReadOnlyList<string> fonts,
        IReadOnlyList<double> sizes) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(sizes);
        if (sizes.Count == 0) {
            throw new ArgumentException("The sizes must offer at least one.", nameof(sizes));
        }

        Fonts = fonts;
        Sizes = sizes;
        _fontFamily = current.FontFamily;
        _fontSize = current.FontSize;
    }

    /// <summary>The typefaces offered.</summary>
    public IReadOnlyList<string> Fonts { get; }

    /// <summary>The sizes offered.</summary>
    public IReadOnlyList<double> Sizes { get; }
}
