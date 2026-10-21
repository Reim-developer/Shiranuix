using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Tests.ViewModels.Dialogs;

/// <summary>
/// Tests for the missing typeface dialog's own state: which face it opens
/// on, and what it says about the one it could not draw.
/// </summary>
public sealed class MissingFontDialogViewModelTests {
    private static readonly string[] Offered = { "Segoe UI", "Consolas", "Georgia" };

    [Fact]
    public void It_Opens_On_The_Face_The_Document_Itself_Is_On() {
        // The document's own setting is what the writer was last reading in,
        // so it is the face the choice should open on rather than the one
        // a list happens to start with.
        var dialog = new MissingFontDialogViewModel(
            "Palatino Linotype", 21.5, "Mở đầu", "Consolas", Offered);

        Assert.Equal("Consolas", dialog.FontFamily);
    }

    [Fact]
    public void It_Falls_To_A_Well_Known_Face_When_The_Documents_Own_Is_Not_Offered() {
        // A document standing on a face this machine has since lost would
        // leave the choice on nothing at all, so it settles on the face the
        // app draws in by default — which, where it is not offered either,
        // leaves the list's own first.
        var dialog = new MissingFontDialogViewModel(
            "Palatino Linotype", 21.5, "Mở đầu", "Not Offered At All", Offered);

        Assert.Equal("Georgia", dialog.FontFamily);
    }

    [Fact]
    public void It_Falls_To_The_First_Face_Offered_When_Neither_Is() {
        var dialog = new MissingFontDialogViewModel(
            "Palatino Linotype", 21.5, "Mở đầu", "Not Offered At All",
            new[] { "Segoe UI", "Consolas" });

        Assert.Equal("Segoe UI", dialog.FontFamily);
    }

    [Fact]
    public void The_Words_Shown_Are_The_Files_Own_And_The_Size_It_Stands_At() {
        // The sample is drawn at the size the file was set in, so the size
        // travels with the face rather than being judged apart from it.
        var dialog = new MissingFontDialogViewModel(
            "Palatino Linotype", 21.5, "Mở đầu của truyện", "Georgia", Offered);

        Assert.Equal("Mở đầu của truyện", dialog.Sample);
        Assert.Equal(21.5, dialog.FontSize);
    }

    [Fact]
    public void It_Names_The_Face_It_Could_Not_Draw() {
        var dialog = new MissingFontDialogViewModel(
            "Palatino Linotype", 21.5, "Mở đầu", "Georgia", Offered);

        Assert.Contains("Palatino Linotype", dialog.Message);
    }

    [Fact]
    public void A_List_With_Nothing_In_It_Is_Refused() {
        // A dialog that offers no face cannot answer anything, and a
        // machine holding no typeface at all is not asked the question.
        Assert.Throws<ArgumentException>(() =>
            new MissingFontDialogViewModel("Palatino Linotype", 21.5, "Mở đầu", "Georgia", []));
    }

    [Fact]
    public void Matching_An_Offered_Face_Ignores_The_Case() {
        var dialog = new MissingFontDialogViewModel(
            "Palatino Linotype", 21.5, "Mở đầu", "seGOE ui", Offered);

        Assert.Equal("Segoe UI", dialog.FontFamily);
    }
}
