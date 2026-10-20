using System.Linq;
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
///
/// The writing asks for it as an <see cref="IDocumentTypography"/>, which
/// is family, size and the word that either moved — the three things a
/// document carries into its file. What is installed, what the preview
/// does and which theme the page is drawn in stay here.
/// </remarks>
public sealed partial class TypographyState : ObservableObject, IDocumentTypography {
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
    /// Whether the quotes the writer types take their curly selves: the
    /// opening one after a pause, the closing one after a word.
    /// </summary>
    /// <remarks>
    /// True by default: the page is written in prose, and prose wears the
    /// curly ones. The choice is how the writing is typed rather than how
    /// it is drawn - like the typeface - so it survives the session instead
    /// of living in the file.
    /// </remarks>
    [ObservableProperty]
    private bool _smartQuotes = true;

    /// <summary>
    /// The dash a pair of hyphens becomes as the writer types.
    /// </summary>
    /// <remarks>
    /// Em by default: in prose the pair stands for the dash that breaks a
    /// sentence, and the sentence takes the long one.
    /// </remarks>
    [ObservableProperty]
    private DashMode _dashMode = DashMode.Em;

    /// <summary>
    /// Whether the empty sheet is greeted by the start screen: the heading,
    /// the words under it, New and Open, and the files the writer came back
    /// to.
    /// </summary>
    /// <remarks>
    /// True by default: a page that greets is how a writing tool opens, until
    /// the writer says otherwise. The choice is the sheet's presence rather
    /// than its ink — like the choices beside it — so it survives the
    /// session instead of living in the file.
    /// </remarks>
    [ObservableProperty]
    private bool _showWelcomeMessage = true;

    /// <summary>
    /// The point sizes offered by the size picker.
    /// </summary>
    public IReadOnlyList<double> FontSizes => _sizes.Items;

    /// <summary>
    /// The typefaces offered by the font picker.
    /// </summary>
    public IReadOnlyList<string> AvailableFonts => _picker.Items;

    /// <summary>
    /// The typeface the page is drawn in, brought down to one this machine
    /// does hold.
    /// </summary>
    /// <remarks>
    /// A choice remembered from a machine that had the typeface installed
    /// does not answer for this machine: the machine that opens the file
    /// next may never have carried it. Anything that cannot fall back at
    /// drawing time answers with this instead - the writer's own choice when
    /// the machine holds it, the quality default otherwise.
    /// </remarks>
    public string InstalledFontFamily =>
        _fonts.GetFamilies().Any(family =>
            string.Equals(family, FontFamily, StringComparison.OrdinalIgnoreCase))
            ? FontFamily
            : DefaultFontFamily;

    /// <summary>
    /// Whether this machine carries anything under a typeface's name: the
    /// question a file's own typeface has to answer before the file can be
    /// drawn in it here.
    /// </summary>
    /// <param name="family">The typeface name to look for.</param>
    /// <returns>
    /// True when something installed answers to that name, however the case
    /// is set; false for a name nothing answers to, and for no name at all.
    /// </returns>
    public bool Carries(string family) =>
        !string.IsNullOrWhiteSpace(family)
        && _fonts.GetFamilies().Any(installed =>
            string.Equals(installed, family, StringComparison.OrdinalIgnoreCase));

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
            SmartQuotes = stored?.SmartQuotes ?? true;
            DashMode = stored?.DashMode ?? DashMode.Em;
            ShowWelcomeMessage = stored?.ShowWelcomeMessage ?? true;
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

    private void Save() {
        // The row is read as it stands and only this state's fields are laid
        // over it, so whatever another state remembers — the export dialog's
        // own choice, say — stays standing: a save here does not own the row
        // whole.
        var row = _settings.Load() ?? new UserSettings();

        row.FontFamily = FontFamily;
        row.FontSize = FontSize;
        row.ThemeMode = ThemeMode;
        row.WordWrap = WordWrap;
        row.TextAlign = TextAlign;
        row.SmartQuotes = SmartQuotes;
        row.DashMode = DashMode;
        row.ShowWelcomeMessage = ShowWelcomeMessage;
        _settings.Save(row);
    }

    partial void OnFontFamilyChanged(string value) {
        if (_restoring) {
            // A restored choice is not a change: nothing is written back, and
            // no font "use" is recorded for a typeface the user never picked.
            return;
        }

        Save();

        // The provider records the pick and leads with it next time the offers
        // are read — when the picker is built. The rows themselves keep still
        // here: announcing the offers again would swap the picker's list while
        // its selection is still being written back, and the row under the
        // held index is no longer the pick, so the write-back would land the
        // old typeface and the choice never settles.
        ((IPickerSource<string>)_picker).Use(value);
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

    partial void OnSmartQuotesChanged(bool value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnDashModeChanged(DashMode value) {
        if (!_restoring) {
            Save();
        }
    }

    partial void OnShowWelcomeMessageChanged(bool value) {
        if (!_restoring) {
            Save();
        }
    }
}
