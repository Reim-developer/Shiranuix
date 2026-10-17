using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Models;
using Shiranuix.Persistence;
using Shiranuix.Services.Fonts;
using Shiranuix.ViewModels.Pickers;

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
    private readonly IFontHistoryStore _fontHistory;
    private readonly FontSizeLadder _sizes = new();
    private readonly FontPickerSource _picker;
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
        _fontHistory = history ?? throw new ArgumentNullException(nameof(history));

        // The pickers are providers the typing state feeds: the fonts draw on
        // the history and must know the current typeface and size; the sizes
        // draw on nothing but their own ladder.
        _picker = new FontPickerSource(_fonts, _fontHistory, () => FontFamily, () => FontSize);

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
    /// Whether lines wrap within the page's width, or run past it.
    /// </summary>
    /// <remarks>
    /// Off by default. A page is what a writing tool starts in; a line that
    /// runs past the paper wants the page to scroll, not to be re-broken.
    /// </remarks>
    [ObservableProperty]
    private bool _wordWrap;

    /// <summary>
    /// Which way the page's lines are set: flush left, centred, or flush
    /// right.
    /// </summary>
    /// <remarks>
    /// Left by default: a page of prose starts where it is written. The
    /// choice is typography rather than text - like the typeface - so it
    /// survives the session instead of living in the file.
    /// </remarks>
    [ObservableProperty]
    private TextAlign _textAlign = TextAlign.Left;

    /// <summary>
    /// Whether the page's lines are set in a bold face.
    /// </summary>
    /// <remarks>
    /// Off by default. A page is written down before it is dressed up, and
    /// the two faces are how the writer dresses it: louder, or leaning.
    /// </remarks>
    [ObservableProperty]
    private bool _bold;

    /// <summary>
    /// Whether the page's lines are set in an italic face.
    /// </summary>
    [ObservableProperty]
    private bool _italic;

    /// <summary>
    /// Whether the page's lines carry an underline.
    /// </summary>
    [ObservableProperty]
    private bool _underline;

    /// <summary>
    /// The point sizes offered by the size picker.
    /// </summary>
    public IReadOnlyList<double> FontSizes => _sizes.Items;

    /// <summary>
    /// The typefaces offered by the font picker.
    /// </summary>
    public IReadOnlyList<string> AvailableFonts => _picker.Items;

    /// <summary>
    /// The line height the page is set in, following the point size.
    /// </summary>
    public double EditorLineHeight => FontSize * LineHeightFactor;

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
            WordWrap = stored?.WordWrap ?? false;
            TextAlign = stored?.TextAlign ?? TextAlign.Left;
            Bold = stored?.Bold ?? false;
            Italic = stored?.Italic ?? false;
            Underline = stored?.Underline ?? false;
        } finally {
            _restoring = false;
        }

        // A restored size outside the usual run still joins the picker's
        // ladder, and the sizes used lately join it too, so the picker opens
        // on the run of sizes this writer actually works in.
        foreach (var size in _fontHistory.RecentSizes()) {
            ((IPickerSource<double>)_sizes).Use(size);
        }

        ((IPickerSource<double>)_sizes).Use(FontSize);
        OnPropertyChanged(nameof(FontSizes));
    }

    private void Save() => _settings.Save(new UserSettings {
        FontFamily = FontFamily,
        FontSize = FontSize,
        ThemeMode = ThemeMode,
        WordWrap = WordWrap,
        TextAlign = TextAlign,
        Bold = Bold,
        Italic = Italic,
        Underline = Underline
    });

    partial void OnFontFamilyChanged(string value) {
        if (_restoring) {
            // A restored choice is not a change: nothing is written back, and
            // no font "use" is recorded for a typeface the user never picked.
            return;
        }

        Save();

        // The provider records the pick and leads with it next time.
        ((IPickerSource<string>)_picker).Use(value);
        OnPropertyChanged(nameof(AvailableFonts));
    }

    partial void OnFontSizeChanged(double value) {
        if (_restoring) {
            return;
        }

        ((IPickerSource<double>)_sizes).Use(value);
        OnPropertyChanged(nameof(FontSizes));

        // The line height follows the size, so anything sized by it — the
        // gutter's numbers as much as the page — has to read again.
        OnPropertyChanged(nameof(EditorLineHeight));

        Save();
        ((IPickerSource<string>)_picker).Use(FontFamily);
        OnPropertyChanged(nameof(AvailableFonts));
    }

    partial void OnThemeModeChanged(ThemeMode value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnWordWrapChanged(bool value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnTextAlignChanged(TextAlign value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnBoldChanged(bool value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnItalicChanged(bool value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnUnderlineChanged(bool value) {
        if (!_restoring) {
            Save();
        }
    }
}
