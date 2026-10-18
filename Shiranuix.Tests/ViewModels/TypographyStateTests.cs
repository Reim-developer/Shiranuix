using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="TypographyState"/>: what the pickers offer, what the
/// store keeps, and the line height that follows the size.
/// </summary>
/// <remarks>
/// All of it runs headlessly — the font catalog is faked, so no installed
/// typeface is depended on, and the store and history are in-memory. These are
/// the rules about state surviving a session, which is exactly the kind of
/// thing a screenshot cannot confirm.
/// </remarks>
public sealed class TypographyStateTests {
    private readonly FakeFontCatalog _fonts = new();
    private readonly FakeUserSettings _settings = new();
    private readonly FakeFontHistoryStore _history = new();

    private const string ExistingPath = "story.txt";
    private const string ExistingText = "Saved content.";

    private TypographyState Create() =>
        new(_fonts, _settings, _history);

    [Fact]
    public void Word_Wrap_Is_Off_Until_It_Is_Turned_On() {
        var typography = Create();

        Assert.False(typography.WordWrap);

        typography.WordWrap = true;

        Assert.True(typography.WordWrap);
        Assert.True(_settings.Saves.Single().WordWrap);
    }

    [Fact]
    public void Restoring_Applies_The_Stored_Word_Wrap() {
        _settings.Stored = new UserSettings {
            FontFamily = "Georgia",
            FontSize = 17,
            ThemeMode = ThemeMode.System,
            WordWrap = true
        };

        Assert.True(Create().WordWrap);
    }

    [Fact]
    public void Smart_Quotes_Are_On_Until_They_Are_Let_Off() {
        var typography = Create();

        Assert.True(typography.SmartQuotes);

        typography.SmartQuotes = false;

        Assert.False(typography.SmartQuotes);
        var stored = Assert.Single(_settings.Saves);
        Assert.False(stored.SmartQuotes);
    }

    [Fact]
    public void Restoring_Applies_The_Stored_Smart_Quotes() {
        _settings.Stored = new UserSettings {
            FontFamily = "Georgia",
            FontSize = 17,
            ThemeMode = ThemeMode.System,
            SmartQuotes = false
        };

        Assert.False(Create().SmartQuotes);
    }

    [Fact]
    public void The_Pair_Of_Hyphens_Takes_The_Long_Dash_Until_It_Is_Sent_Shorter() {
        var typography = Create();

        Assert.Equal(DashMode.Em, typography.DashMode);

        typography.DashMode = DashMode.En;

        Assert.Equal(DashMode.En, typography.DashMode);
        var stored = Assert.Single(_settings.Saves);
        Assert.Equal(DashMode.En, stored.DashMode);
    }

    [Fact]
    public void Restoring_Applies_The_Stored_Dash() {
        _settings.Stored = new UserSettings {
            FontFamily = "Georgia",
            FontSize = 17,
            ThemeMode = ThemeMode.System,
            DashMode = DashMode.Off
        };

        Assert.Equal(DashMode.Off, Create().DashMode);
    }

    [Fact]
    public void The_Lines_Rest_On_The_Left_Until_Someone_Moves_Them() {
        var typography = Create();

        Assert.Equal(TextAlign.Left, typography.TextAlign);

        typography.TextAlign = TextAlign.Center;

        Assert.Equal(TextAlign.Center, typography.TextAlign);
        var stored = Assert.Single(_settings.Saves);
        Assert.Equal(TextAlign.Center, stored.TextAlign);
    }

    [Fact]
    public void Restoring_Applies_The_Stored_Alignment() {
        _settings.Stored = new UserSettings {
            FontFamily = "Georgia",
            FontSize = 17,
            ThemeMode = ThemeMode.System,
            TextAlign = TextAlign.Right
        };

        Assert.Equal(TextAlign.Right, Create().TextAlign);
    }

    [Fact]
    public void A_Typeface_The_Machine_Does_Not_Hold_Is_Brought_Down_To_One_It_Does() {
        var typography = Create();

        // The fake catalog's list is short and known: Futura is not on it,
        // Verdana is. The page cannot fall back at drawing time, so it says
        // now which typeface it would really draw in.
        typography.FontFamily = "Futura";
        Assert.Equal(TypographyState.DefaultFontFamily, typography.InstalledFontFamily);

        typography.FontFamily = "Verdana";
        Assert.Equal("Verdana", typography.InstalledFontFamily);
    }

    [Fact]
    public void A_First_Run_Rests_On_The_Defaults() {
        var typography = Create();

        Assert.Equal(TypographyState.DefaultFontFamily, typography.FontFamily);
        Assert.Equal(TypographyState.DefaultFontSize, typography.FontSize);
        Assert.Equal(ThemeMode.System, typography.ThemeMode);
        Assert.Equal(TextAlign.Left, typography.TextAlign);
    }

    [Fact]
    public void Restoring_Applies_The_Stored_Choices() {
        _settings.Stored = new UserSettings {
            FontFamily = "Verdana",
            FontSize = 24,
            ThemeMode = ThemeMode.Dark
        };

        var typography = Create();

        Assert.Equal("Verdana", typography.FontFamily);
        Assert.Equal(24, typography.FontSize);
        Assert.Equal(ThemeMode.Dark, typography.ThemeMode);
    }

    [Fact]
    public void Restoring_Does_Not_Write_What_It_Just_Read() {
        // Restoring is not changing: going through the properties without a
        // guard here would rewrite the row on every launch, and record a font
        // "use" for a typeface the user never picked this session.
        _settings.Stored = new UserSettings {
            FontFamily = "Verdana",
            FontSize = 24,
            ThemeMode = ThemeMode.Light
        };

        Create();

        Assert.Empty(_settings.Saves);
        Assert.Empty(_history.Uses);
    }

    [Fact]
    public void A_Restored_Size_Outside_The_Ladder_Joins_The_Picker() {
        _settings.Stored = new UserSettings {
            FontFamily = "Georgia",
            FontSize = 15.5,
            ThemeMode = ThemeMode.System
        };

        var typography = Create();

        Assert.Contains(15.5, typography.FontSizes);
    }

    [Fact]
    public void Changing_The_Typeface_Stores_It_And_Records_The_Use() {
        var typography = Create();

        typography.FontFamily = "Verdana";

        var stored = Assert.Single(_settings.Saves);
        Assert.Equal("Verdana", stored.FontFamily);
        // One row, always: the save is an update, not a second entry.
        Assert.Equal(1, stored.Id);
        Assert.Equal(new[] { "Verdana" }, _history.Uses.Select(use => use.Family).ToArray());
    }

    [Fact]
    public void Changing_The_Size_Stores_It_And_Joins_The_Ladder() {
        var typography = Create();

        typography.FontSize = 24;

        var stored = Assert.Single(_settings.Saves);
        Assert.Equal(24, stored.FontSize);
        Assert.Contains(24d, typography.FontSizes);
    }

    [Fact]
    public void Changing_The_Theme_Stores_It() {
        var typography = Create();

        typography.ThemeMode = ThemeMode.Dark;

        var stored = Assert.Single(_settings.Saves);
        Assert.Equal(ThemeMode.Dark, stored.ThemeMode);
    }

    [Fact]
    public void The_Font_Picker_Leads_With_The_Typefaces_Used_Lately() {
        // Remembered oldest first: the store hands back most recent first, so
        // Verdana — picked last — should lead the picker.
        _history.Remember("Georgia", 17);
        _history.Remember("Verdana", 24);

        var typography = Create();

        var fonts = typography.AvailableFonts.ToList();
        Assert.Equal("Verdana", fonts[0]);
        Assert.Equal("Georgia", fonts[1]);
    }

    [Fact]
    public void Re_Picking_A_Typeface_Moves_It_To_The_Front() {
        _history.Remember("Georgia", 17);
        _history.Remember("Verdana", 24);

        var typography = Create();
        // Consolas, not the document's own Georgia: setting the picker to the
        // typeface it already shows is no change at all, and would record
        // nothing.
        typography.FontFamily = "Consolas";

        Assert.Equal("Consolas", typography.AvailableFonts.First());
    }

    [Fact]
    public void No_Typeface_Appears_In_The_Picker_Twice() {
        _history.Remember("Georgia", 17);

        var typography = Create();

        Assert.Equal(
            1,
            typography.AvailableFonts.Count(family => family == "Georgia"));
    }

    [Fact]
    public void The_Font_Picker_Offers_The_Document_s_Font_Even_When_The_Machine_Lacks_It() {
        var typography = Create();

        // A typeface the fake catalog does not report — a stand-in for a
        // document opened on a machine that never had it installed.
        typography.FontFamily = "Futura";

        Assert.Contains("Futura", typography.AvailableFonts);
        // The installed ones are still there, untouched and in order.
        var families = typography.AvailableFonts.ToList();
        Assert.True(families.IndexOf("Consolas") < families.IndexOf("Georgia"));
        Assert.Equal(_fonts.Families.Count + 1, families.Count);
    }

    [Fact]
    public void The_Size_Picker_Offers_The_Sizes_Used_Lately() {
        _history.Remember("Georgia", 30);
        _history.Remember("Georgia", 15.5);

        var typography = Create();

        Assert.Contains(30d, typography.FontSizes);
        Assert.Contains(15.5, typography.FontSizes);
    }

    [Fact]
    public void Re_Picking_A_Ladder_Size_Does_Not_Add_A_Second_One() {
        var typography = Create();

        typography.FontSize = 12;
        var before = typography.FontSizes.Count;

        typography.FontSize = 14;

        Assert.Equal(before, typography.FontSizes.Count);
        Assert.Equal(1, typography.FontSizes.Count(size => size == 14d));
    }

    [Fact]
    public void A_Size_Outside_The_Ladder_Joins_The_Picker_In_Order() {
        var typography = Create();

        typography.FontSize = 15;

        var sizes = typography.FontSizes.ToList();
        var index = sizes.IndexOf(15d);
        Assert.True(index > 0, "15 should have been inserted, not appended blindly.");
        Assert.Equal(14d, sizes[index - 1]);
        Assert.Equal(16d, sizes[index + 1]);
    }

    [Fact]
    public void Changing_The_Typeface_Is_Not_An_Edit_To_The_Document() {
        var files = new FakeFileService();
        files.Files["story.txt"] = ExistingText;
        var window = new DocumentWindow(files);
        var ledger = new EditLedger();
        var session = new DocumentSession(
            new DocumentFile(files, new FakeRecentFilesStore()), new DocumentHistory(), window, ledger,
            new WindowNavigator(window, ledger));
        session.Open("story.txt");

        var typography = Create();

        typography.FontFamily = "Verdana";
        typography.FontSize = 24;

        // A .txt file holds text and nothing else. A typeface is how this app
        // draws it, so choosing one is not something to write back.
        Assert.False(session.IsDirty);
        Assert.Equal(ExistingText, session.Text);
    }

    [Fact]
    public void The_Line_Height_Follows_The_Point_Size() {
        var typography = Create();

        // The default: what the sheet was designed around.
        Assert.Equal(
            TypographyState.LineHeightFactor * TypographyState.DefaultFontSize,
            typography.EditorLineHeight,
            4);

        // The size where a fixed line height drew every wrapped line over the
        // one above it: 72pt glyphs stand about 96px tall, so the line box has
        // to clear them.
        typography.FontSize = 72;

        Assert.True(
            typography.EditorLineHeight > 96,
            $"Expected the line box to clear a 72pt line, got {typography.EditorLineHeight}.");
        Assert.Equal(72 * TypographyState.LineHeightFactor, typography.EditorLineHeight, 4);
    }

    [Fact]
    public void Changing_The_Point_Size_Tells_The_Line_Height_To_Read_Again() {
        // The line height is a computed property, so nothing updates it on its
        // own: the gutter's numbers and the page's own spacing both size
        // themselves by it, and both would be left at the old size.
        var typography = Create();
        var changed = new List<string>();
        typography.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        typography.FontSize = 72;

        Assert.Contains(nameof(TypographyState.EditorLineHeight), changed);
    }
}
