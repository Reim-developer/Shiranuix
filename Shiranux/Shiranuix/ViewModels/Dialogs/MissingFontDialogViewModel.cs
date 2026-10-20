using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Shiranuix.ViewModels.Dialogs;

/// <summary>
/// The missing typeface dialog's own state: the face it says is missing,
/// the sample the faces are judged by, and the face picked instead.
/// </summary>
/// <remarks>
/// The window that draws this carries no judgement of its own — the face
/// picked arrives through the button and the service closes the window with
/// it. Nothing here reaches the document or the typography: it is the one
/// question the dialog asks.
/// </remarks>
public sealed partial class MissingFontDialogViewModel : ObservableObject {
    /// <summary>The typeface a document is set in when nothing else is installed.</summary>
    private const string FallbackFontFamily = "Georgia";

    /// <summary>The face chosen to open with.</summary>
    [ObservableProperty]
    private string _fontFamily;

    /// <summary>
    /// Creates the dialog's state over the face that is missing, the words
    /// to judge the faces by, and the faces this machine offers.
    /// </summary>
    /// <param name="missing">The typeface the file names.</param>
    /// <param name="size">The point size the file stands at.</param>
    /// <param name="sample">Some of the file's own writing.</param>
    /// <param name="initial">The face to open on, when it is offered.</param>
    /// <param name="fonts">The typefaces offered, most worth reaching for first.</param>
    /// <exception cref="ArgumentException">No typeface is offered at all.</exception>
    public MissingFontDialogViewModel(
        string missing,
        double size,
        string sample,
        string initial,
        IReadOnlyList<string> fonts) {
        ArgumentNullException.ThrowIfNull(missing);
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(fonts);
        if (fonts.Count == 0) {
            throw new ArgumentException("The dialog must offer at least one typeface.", nameof(fonts));
        }

        Missing = missing;
        FontSize = size;
        Sample = sample;
        Fonts = fonts;
        _fontFamily = Settle(fonts, initial);
    }

    /// <summary>The typeface the file names, which this machine does not have.</summary>
    public string Missing { get; }

    /// <summary>The point size the file stands at.</summary>
    public double FontSize { get; }

    /// <summary>The words every face is shown in — the file's own.</summary>
    public string Sample { get; }

    /// <summary>The typefaces offered.</summary>
    public IReadOnlyList<string> Fonts { get; }

    /// <summary>The one question the dialog asks, in the words the user reads.</summary>
    public string Message =>
        $"“{Missing}” does not come with this machine, so the file cannot be drawn in it here. " +
        "Pick a typeface to open with — the file's own words are shown as you choose.";

    /// <summary>
    /// The face to open on: the document's own where this machine has it,
    /// the well-known one where it does not, and whatever is offered where
    /// neither is.
    /// </summary>
    private static string Settle(IReadOnlyList<string> fonts, string initial) =>
        Match(fonts, initial)
        ?? Match(fonts, FallbackFontFamily)
        ?? fonts[0];

    /// <summary>
    /// An offered face matching the wanted one, however the case is set —
    /// typeface names answer to either on the machines they come from.
    /// </summary>
    private static string? Match(IReadOnlyList<string> fonts, string wanted) =>
        string.IsNullOrWhiteSpace(wanted)
            ? null
            : fonts.FirstOrDefault(font =>
                string.Equals(font, wanted, StringComparison.OrdinalIgnoreCase));
}
