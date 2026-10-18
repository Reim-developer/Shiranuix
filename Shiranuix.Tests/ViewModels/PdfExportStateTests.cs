using Shiranuix.Models;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for the export dialog's remembered choice: what it opens on when
/// the store has nothing, and what a remember lands on the row.
/// </summary>
public sealed class PdfExportStateTests {
    [Fact]
    public void A_Chooser_Never_Answered_Opens_On_The_Page_Itself() {
        var settings = new FakeUserSettings();
        var typography = new TypographyState(new FakeFontCatalog(), settings, new FakeFontHistoryStore());
        var state = new PdfExportState(settings);

        Assert.Equal(
            new PdfExportOptions(TypographyState.DefaultFontFamily, TypographyState.DefaultFontSize),
            state.Current(typography));
    }

    [Fact]
    public void A_Stored_Choice_Opens_The_Chooser_And_Not_The_Page() {
        var settings = new FakeUserSettings {
            Stored = new UserSettings { PdfFontFamily = "Times New Roman", PdfFontSize = 12 },
        };
        var typography = new TypographyState(new FakeFontCatalog(), settings, new FakeFontHistoryStore());
        var state = new PdfExportState(settings);

        // The page is set in Georgia at the default size, and stays there:
        // the chooser opens on what the last export was drawn in.
        Assert.Equal(new PdfExportOptions("Times New Roman", 12), state.Current(typography));
        Assert.Equal(TypographyState.DefaultFontFamily, typography.FontFamily);
    }

    [Fact]
    public void A_Family_Stored_Alone_Asks_The_Page_For_The_Size() {
        var settings = new FakeUserSettings {
            Stored = new UserSettings { PdfFontFamily = "Times New Roman" },
        };
        var typography = new TypographyState(new FakeFontCatalog(), settings, new FakeFontHistoryStore());
        var state = new PdfExportState(settings);

        Assert.Equal(
            new PdfExportOptions("Times New Roman", TypographyState.DefaultFontSize),
            state.Current(typography));
    }

    [Fact]
    public void A_Size_Stored_Alone_Asks_The_Page_For_The_Family() {
        var settings = new FakeUserSettings {
            Stored = new UserSettings { PdfFontSize = 12 },
        };
        var typography = new TypographyState(new FakeFontCatalog(), settings, new FakeFontHistoryStore());
        var state = new PdfExportState(settings);

        Assert.Equal(
            new PdfExportOptions(TypographyState.DefaultFontFamily, 12),
            state.Current(typography));
    }

    [Fact]
    public void A_Choice_Remembered_Lands_On_The_Row_Leaving_The_Rest_Standing() {
        var settings = new FakeUserSettings {
            Stored = new UserSettings {
                FontFamily = "Verdana",
                FontSize = 11,
                ThemeMode = ThemeMode.Dark,
                WordWrap = true,
                TextAlign = TextAlign.Right,
                PdfFontFamily = "Georgia",
                PdfFontSize = 17,
            },
        };
        var state = new PdfExportState(settings);

        state.Remember(new PdfExportOptions("Times New Roman", 12));

        var row = Assert.IsType<UserSettings>(settings.Stored);
        Assert.Equal("Times New Roman", row.PdfFontFamily);
        Assert.Equal(12, row.PdfFontSize);
        Assert.Equal("Verdana", row.FontFamily);
        Assert.Equal(11, row.FontSize);
        Assert.Equal(ThemeMode.Dark, row.ThemeMode);
        Assert.True(row.WordWrap);
        Assert.Equal(TextAlign.Right, row.TextAlign);
        Assert.Single(settings.Saves);
    }

    [Fact]
    public void A_Remember_Into_An_Unanswered_Store_Makes_The_Row() {
        var settings = new FakeUserSettings();
        var state = new PdfExportState(settings);

        state.Remember(new PdfExportOptions("Verdana", 9));

        Assert.NotNull(settings.Stored);
        Assert.Equal("Verdana", settings.Stored!.PdfFontFamily);
        Assert.Equal(9, settings.Stored.PdfFontSize);
    }
}
