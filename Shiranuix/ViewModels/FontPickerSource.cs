using Shiranuix.Persistence;
using Shiranuix.Services.Fonts;

namespace Shiranuix.ViewModels;

/// <summary>
/// The font picker: the typefaces used lately, then everything installed, and
/// the document's own never missing.
/// </summary>
/// <remarks>
/// Read fresh on every query, so the picker reflects the machine as it is and
/// the history as it stands when it is opened. The current typeface and size
/// are handed in as functions because they are the typing state's to know, not
/// this provider's.
/// </remarks>
public sealed class FontPickerSource : IPickerSource<string> {
    private readonly IFontCatalog _catalog;
    private readonly IFontHistoryStore _history;
    private readonly Func<string> _current;
    private readonly Func<double> _size;

    /// <summary>
    /// Creates the picker over the machine's typefaces and the stored history.
    /// </summary>
    /// <param name="catalog">Installed typefaces, for the list.</param>
    /// <param name="history">The stored typeface and size picks.</param>
    /// <param name="current">The typeface in use, so it is never absent.</param>
    /// <param name="size">The point size in use, recorded with each pick.</param>
    public FontPickerSource(
        IFontCatalog catalog,
        IFontHistoryStore history,
        Func<string> current,
        Func<double> size) {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _size = size ?? throw new ArgumentNullException(nameof(size));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Items {
        get {
            var picked = _history.RecentFamilies().ToList();
            var ordered = new List<string>(picked);

            // The installed ones follow, minus any the history already led
            // with, so no typeface appears twice.
            ordered.AddRange(_catalog.GetFamilies().Where(family => !ordered.Contains(family)));

            // A typeface the machine does not have (a missing Georgia, say) is
            // still offered, so the document's real font is never absent.
            var current = _current();
            if (!string.IsNullOrWhiteSpace(current) && !ordered.Contains(current)) {
                ordered.Add(current);
            }

            return ordered;
        }
    }

    /// <inheritdoc />
    public void Use(string family) => _history.Remember(family, _size());
}
