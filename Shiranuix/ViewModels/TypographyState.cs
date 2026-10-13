using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;
using Shiranuix.Services;

namespace Shiranuix.ViewModels;

/// <summary>
/// The document's typography and the colour theme it is drawn in: what the
/// pickers offer, what was picked, and the line height the page is set at.
/// </summary>
/// <remarks>
/// Holds the three choices the store keeps — typeface, point size, theme —
/// writes them back the moment they change, and reads the history that leads
/// the pickers. The view model knows none of that: it hands this to the view
/// and asks it to cycle the theme.
///
/// Everything that has to line up with the text is derived here, from one
/// value, so the page's line height, the gutter's numbers and the pickers'
/// contents cannot end up disagreeing about it.
/// </remarks>
public sealed partial class TypographyState : ObservableObject {
    /// <summary>
    /// The typeface a new document starts in. Georgia: a system serif that
    /// carries Vietnamese diacritics cleanly and reads well at length, and so
    /// needs nothing bundled.
    /// </summary>
    public const string DefaultFontFamily = "Georgia";

    /// <summary>
    /// The point size a new document starts at. Matches the default Georgia
    /// size the sheet was designed around.
    /// </summary>
    public const double DefaultFontSize = 17;

    /// <summary>
    /// The line spacing as a multiple of the point size.
    /// </summary>
    /// <remarks>
    /// A multiple, not a constant: a constant line height is what made large
    /// type unreadable. The glyphs at 72pt are about 96px tall, and a fixed
    /// 29px line box drew every wrapped line over the one above it. At the
    /// default 17pt this is 29px, which is what the sheet was designed around.
    /// </remarks>
    public const double LineHeightFactor = 1.7;

    private readonly IFontCatalog _fonts;
    private readonly IUserSettings _settings;
    private readonly IFontHistoryStore _history;
    private readonly FontSizeLadder _sizes = new();
    private bool _restoring;

    /// <summary>
    /// Creates the typography state over the pickers' sources and the stores
    /// that keep it, restoring the stored choices.
    /// </summary>
    /// <param name="fonts">Installed typefaces, for the font picker.</param>
    /// <param name="settings">The stored font, size and theme.</param>
    /// <param name="history">The stored typeface and size history.</param>
    public TypographyState(
        IFontCatalog fonts,
        IUserSettings settings,
        IFontHistoryStore history) {
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _history = history ?? throw new ArgumentNullException(nameof(history));

        Restore();
    }

    /// <summary>
    /// The typeface the document is set in.
    /// </summary>
    /// <remarks>
    /// A string rather than an <c>Avalonia.Media.FontFamily</c>: a typeface is
    /// a UI type, and this class runs headlessly in tests. The binding
    /// converts.
    /// </remarks>
    [ObservableProperty]
    private string _fontFamily = DefaultFontFamily;

    /// <summary>
    /// The point size the document is set in.
    /// </summary>
    [ObservableProperty]
    private double _fontSize = DefaultFontSize;

    /// <summary>
    /// The requested colour theme.
    /// </summary>
    [ObservableProperty]
    private ThemeMode _themeMode = ThemeMode.System;

    /// <summary>
    /// The point sizes offered by the size picker.
    /// </summary>
    public IReadOnlyList<double> FontSizes => _sizes.Sizes;

    /// <summary>
    /// The typefaces offered by the font picker.
    /// </summary>
    /// <remarks>
    /// Recently used first: the typefaces a writer actually works in, ahead of
    /// the alphabetical run of everything installed. Read on every query, so
    /// the picker reflects the machine as it is and the history as it stands
    /// when the picker is opened. A typeface the machine does not have is still
    /// offered, so the document's real font is never absent from the list.
    /// </remarks>
    public IReadOnlyList<string> AvailableFonts {
        get {
            var picked = _history.RecentFamilies().ToList();
            var ordered = new List<string>(picked);

            ordered.AddRange(_fonts.GetFamilies().Where(family => !ordered.Contains(family)));

            if (!string.IsNullOrWhiteSpace(FontFamily) && !ordered.Contains(FontFamily)) {
                ordered.Add(FontFamily);
            }

            return ordered;
        }
    }

    /// <summary>
    /// The line height the page is set in, following the point size.
    /// </summary>
    public double EditorLineHeight => FontSize * LineHeightFactor;

    /// <summary>
    /// Cycles the theme: System → Light → Dark → System.
    /// </summary>
    public void CycleTheme() {
        ThemeMode = ThemeMode switch {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }

    private void Restore() {
        var stored = _settings.Load();

        // Restored through the properties, with the reactions held back: a
        // restored choice is not a change, so it must not write the row back
        // out on every launch or record a font "use" for a typeface the user
        // never picked this session.
        _restoring = true;
        try {
            FontFamily = string.IsNullOrWhiteSpace(stored?.FontFamily)
                ? DefaultFontFamily
                : stored.FontFamily;
            FontSize = stored is { FontSize: > 0 } ? stored.FontSize : DefaultFontSize;
            ThemeMode = stored?.ThemeMode ?? ThemeMode.System;
        } finally {
            _restoring = false;
        }

        // A restored size outside the usual run still joins the picker's
        // ladder, and the sizes used lately join it too, so the picker opens
        // on the run of sizes this writer actually works in.
        foreach (var size in _history.RecentSizes()) {
            _sizes.Ensure(size);
        }

        _sizes.Ensure(FontSize);
        OnPropertyChanged(nameof(FontSizes));
    }

    private void Save() {
        if (_restoring) {
            return;
        }

        _settings.Save(new UserSettings {
            FontFamily = FontFamily,
            FontSize = FontSize,
            ThemeMode = ThemeMode
        });
    }

    private void Remember(string family) {
        if (_restoring) {
            return;
        }

        _history.Remember(family, FontSize);
    }

    partial void OnFontFamilyChanged(string value) {
        Save();
        Remember(value);

        // The picker leads with the recently used, which just changed.
        OnPropertyChanged(nameof(AvailableFonts));
    }

    partial void OnFontSizeChanged(double value) {
        _sizes.Ensure(value);
        OnPropertyChanged(nameof(FontSizes));

        // The line height follows the size, so anything sized by it — the
        // gutter's numbers as much as the page — has to read again.
        OnPropertyChanged(nameof(EditorLineHeight));

        Save();
        Remember(FontFamily);
        OnPropertyChanged(nameof(AvailableFonts));
    }

    partial void OnThemeModeChanged(ThemeMode value) => Save();
}
