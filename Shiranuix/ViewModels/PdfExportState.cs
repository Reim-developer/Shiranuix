using Shiranuix.Models;
using Shiranuix.Persistence;

namespace Shiranuix.ViewModels;

/// <summary>
/// The shape the last export to PDF was drawn in, and where the next one
/// starts: what the export dialog remembered, asked of the store.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is on screen for this state to draw; it is only the memory the
/// export flow asks for. First asked of nothing — a row the dialog has never
/// been answered in — it falls back, field by field, to what the page itself
/// is set in, so the dialog opens the way the sheet looks. An answered dialog
/// is remembered the moment its writing lands, and the next export opens on
/// the page the last one made.
/// </para>
/// <para>
/// The row is read as it stands and only these two fields are laid over it,
/// mirroring the way every state saves: one store rides the whole row, and a
/// remember here does not own what the typography and the theme hold.
/// </para>
/// </remarks>
public sealed class PdfExportState {
    private readonly IUserSettings _settings;

    /// <summary>
    /// Creates the state over the store that keeps the choice between
    /// sessions.
    /// </summary>
    /// <param name="settings">The stored choices.</param>
    public PdfExportState(IUserSettings settings) =>
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>
    /// The options the export dialog opens with: what the last export was
    /// drawn in, or, where nothing was ever answered, the page's own.
    /// </summary>
    /// <param name="typography">The page as the screen shows it.</param>
    public PdfExportOptions Current(TypographyState typography) {
        ArgumentNullException.ThrowIfNull(typography);

        var stored = _settings.Load();

        return new PdfExportOptions(
            stored is { PdfFontFamily.Length: > 0 } ? stored.PdfFontFamily : typography.FontFamily,
            stored is { PdfFontSize: > 0 } ? stored.PdfFontSize : typography.FontSize);
    }

    /// <summary>
    /// Keeps the shape the export was just drawn in, for the next dialog to
    /// open on.
    /// </summary>
    /// <param name="options">The shape the paper carries.</param>
    /// <remarks>
    /// Everything else the row holds is read first and left as it was found;
    /// remembering does not reach into the typography's business.
    /// </remarks>
    public void Remember(PdfExportOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        var row = _settings.Load() ?? new UserSettings();
        row.PdfFontFamily = options.FontFamily;
        row.PdfFontSize = options.FontSize;
        _settings.Save(row);
    }
}
