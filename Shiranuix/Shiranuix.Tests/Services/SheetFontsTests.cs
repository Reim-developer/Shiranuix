using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="SheetFonts"/>: what the paper is drawn in, when the
/// page's own face can and cannot carry the writing, and what the counting
/// of a document's letters leaves out.
/// </summary>
/// <remarks>
/// The faces are stand-ins over builds, so every fact is exact: a face that
/// answers the letters asked of it carries them, and one that does not is
/// stepped over like one the machine never held.
/// </remarks>
public sealed class SheetFontsTests {
    private const int Ek = 0x1EBF; // ế, a letter the page's own may not answer

    private static readonly string[] Spares = ["Times New Roman", "Arial"];

    private static Func<string, byte[]?> Only(string family, byte[]? font) =>
        asked => asked == family ? font : null;

    private static IReadOnlySet<int> Used(params int[] letters) => new HashSet<int>(letters);

    [Fact]
    public void The_Chosen_Face_Stays_When_It_Carries_Every_Letter() {
        var face = SheetFonts.PickFace(
            "Georgia",
            Used(Ek),
            Only("Georgia", TestFont.Small((Ek, Ek))),
            Spares);

        Assert.Equal("Georgia", face);
    }

    [Fact]
    public void A_Face_That_Cannot_Carry_The_Writing_Is_Stepped_Over() {
        // Georgia is held by the machine and blind in file: carried by name,
        // silent for the letters asked of it.
        var face = SheetFonts.PickFace(
            "Georgia",
            Used(Ek),
            asked => asked switch {
                "Georgia" => TestFont.Empty(),
                "Times New Roman" => TestFont.Small((Ek, Ek)),
                _ => null,
            },
            Spares);

        Assert.Equal("Times New Roman", face);
    }

    [Fact]
    public void The_First_Spare_That_Carries_The_Writing_Is_The_Answer() {
        var face = SheetFonts.PickFace(
            "Georgia",
            Used(Ek),
            asked => asked == "Arial" ? TestFont.Small((Ek, Ek)) : TestFont.Empty(),
            Spares);

        Assert.Equal("Arial", face);
    }
    [Fact]
    public void Nothing_Carrying_Leaves_The_Choice_Standing() {
        var face = SheetFonts.PickFace(
            "Georgia",
            Used(Ek),
            _ => TestFont.Empty(),
            Spares);

        Assert.Equal("Georgia", face);
    }

    [Fact]
    public void A_Face_The_Machine_Does_Not_Hold_Is_Asked_Nothing() {
        var face = SheetFonts.PickFace(
            "Georgia",
            Used(Ek),
            Only("Times New Roman", TestFont.Small((Ek, Ek))),
            Spares);

        Assert.Equal("Times New Roman", face);
    }

    [Fact]
    public void A_Document_Of_Nothing_Holds_To_Its_Choice() {
        var face = SheetFonts.PickFace(
            "Georgia",
            SheetFonts.UsedCodepoints(string.Empty),
            _ => TestFont.Empty(),
            Spares);

        Assert.Equal("Georgia", face);
    }

    [Fact]
    public void The_Counting_Keeps_Each_Letter_Once_Astrals_Along() {
        // A lone surrogate after the pair is broken writing, and is counted
        // nowhere; a space is a letter as much as the rest.
        var used = SheetFonts.UsedCodepoints("ab \u1EBF\u0000\u007F \uD83D\uDE00 \uD800");

        Assert.True(used.SetEquals([(int)'a', (int)'b', (int)' ', Ek, 0x1F600]));
    }

    [Fact]
    public void Control_Letters_Are_Counted_Nowhere() {
        var used = SheetFonts.UsedCodepoints("\u0001\u0002\u007F\t\n");

        Assert.True(used.Count == 0);
    }
}
