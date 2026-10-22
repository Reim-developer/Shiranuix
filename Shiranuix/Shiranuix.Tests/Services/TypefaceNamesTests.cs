using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="TypefaceNames"/>: what a typeface file calls
/// itself, read out of the name table it keeps.
/// </summary>
/// <remarks>
/// The files are builds rather than faces off the machine, so every fact
/// is exact: names written out in records, in the languages a shelf can
/// hear, and nothing readable at all where nothing was ever written.
/// </remarks>
public sealed class TypefaceNamesTests {
    private const ushort Windows = 3;
    private const ushort Unicode = 0;
    private const ushort Mac = 1;
    private const ushort Ucs2 = 1;
    private const ushort Roman = 0;
    private const ushort Family = 1;
    private const ushort Style = 2;

    [Fact]
    public void A_File_Says_Its_Family_And_The_Style_It_Is_Cut_In() {
        var said = Say(TestFont.Named(
            (Windows, Ucs2, Family, "Liberation Sans"),
            (Windows, Ucs2, Style, "Bold Italic")));

        Assert.Equal("Liberation Sans", said?.Family);
        Assert.Equal("Bold Italic", said?.Subfamily);
    }

    [Fact]
    public void A_Style_The_File_Does_Not_Say_Is_The_Regular_One() {
        var said = Say(TestFont.Named((Windows, Ucs2, Family, "DejaVu Sans")));

        Assert.Equal("DejaVu Sans", said?.Family);
        Assert.Equal("Regular", said?.Subfamily);
    }

    [Fact]
    public void The_Windows_Spelling_Is_Heard_Above_The_Mac_One() {
        var said = Say(TestFont.Named(
            (Mac, Roman, Family, "Some Old Word"),
            (Windows, Ucs2, Family, "Some New Word")));

        Assert.Equal("Some New Word", said?.Family);
    }

    [Fact]
    public void A_File_That_Speaks_Only_Mac_Is_Still_Heard() {
        var said = Say(TestFont.Named((Mac, Roman, Family, "Helvetica")));

        Assert.Equal("Helvetica", said?.Family);
        Assert.Equal("Regular", said?.Subfamily);
    }

    [Fact]
    public void A_Unicode_Record_Is_Heard_When_No_Windows_One_Stands() {
        var said = Say(TestFont.Named((Unicode, Roman, Family, "A Unicode Family")));

        Assert.Equal("A Unicode Family", said?.Family);
    }

    [Fact]
    public void A_Collection_Answers_With_The_Face_It_Holds() {
        var said = Say(TestFont.NamedCollection(
            (Windows, Ucs2, Family, "Pair Held"),
            (Windows, Ucs2, Style, "Regular")));

        Assert.Equal("Pair Held", said?.Family);
    }

    [Fact]
    public void An_OpenType_File_Is_Heard_Like_A_TrueType_One() {
        var font = TestFont.Named((Windows, Ucs2, Family, "Some Family"));
        font[0] = (byte)'O';
        font[1] = (byte)'T';
        font[2] = (byte)'T';
        font[3] = (byte)'O';

        Assert.Equal("Some Family", Say(font)?.Family);
    }

    [Fact]
    public void Names_Spoken_With_Surrounding_Silence_Are_Heard_Clean() {
        var said = Say(TestFont.Named((Windows, Ucs2, Family, "  Georgia\0 ")));

        Assert.Equal("Georgia", said?.Family);
    }

    [Fact]
    public void A_File_With_No_Name_Table_Says_Nothing() {
        Assert.Null(Say(TestFont.Small((0x61, 0x7A))));
    }

    [Fact]
    public void A_File_With_No_Records_In_Its_Name_Says_Nothing() {
        Assert.Null(Say(TestFont.Named()));
    }

    [Fact]
    public void A_Name_Table_Cut_Away_Is_Heard_As_Nothing() {
        var font = TestFont.Named((Windows, Ucs2, Family, "Liberation Sans"));

        Assert.Null(Say(font[..(font.Length - 10)]));
    }

    [Fact]
    public void Bytes_That_Are_No_Typeface_Say_Nothing() {
        Assert.Null(Say(TestFont.Empty()));
        Assert.Null(Say([0x00, 0x01]));
        Assert.Null(Say([]));
    }

    private static TypefaceNames.Names? Say(byte[] font) =>
        TypefaceNames.Read(new MemoryStream(font, writable: false));
}
