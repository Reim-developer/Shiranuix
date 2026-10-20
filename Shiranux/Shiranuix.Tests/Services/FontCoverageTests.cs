using Shiranuix.Services.Pdf;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="FontCoverage"/>: whether a file's own reading table
/// answers for the letters a document draws.
/// </summary>
/// <remarks>
/// The files are builds rather than faces off the machine, so every fact is
/// exact: runs of held letters written out in bytes, one gap between them,
/// and one place where a run answers away from where it stands.
/// </remarks>
public sealed class FontCoverageTests {
    [Fact]
    public void A_Letter_Inside_A_Run_Is_Held() {
        var font = TestFont.Small(((int)'a', (int)'z'), (0x1EA0, 0x1EF9));

        Assert.True(FontCoverage.Covers(font, 0x1ED9));
        Assert.True(FontCoverage.Covers(font, 'm'));
    }

    [Fact]
    public void A_Letter_In_The_Gap_Between_Runs_Is_Not_Held() {
        var font = TestFont.Small(((int)'a', (int)'z'), (0x1EA0, 0x1EF9));

        Assert.False(FontCoverage.Covers(font, 0x00FF));
        Assert.False(FontCoverage.Covers(font, 0x1F600));
    }

    [Fact]
    public void A_Mark_Read_Away_From_Its_Run_Is_Heard() {
        // Two letters share one run, and the run answers away from its place:
        // the first reads the empty square, the second reads the twelfth
        // mark.
        var font = TestFont.SmallLookingAway([0, 12]);

        Assert.False(FontCoverage.Covers(font, 'a'));
        Assert.True(FontCoverage.Covers(font, 'b'));
    }

    [Fact]
    public void A_File_With_No_Reading_Table_Carries_Nothing() {
        Assert.False(FontCoverage.Covers(TestFont.Empty(), 'a'));
    }

    [Fact]
    public void All_Is_Answered_Only_When_Every_Letter_Is_Held() {
        var font = TestFont.Small(((int)'a', (int)'z'));

        Assert.True(FontCoverage.CoversAll(font, ['a', 'b', 'z']));
        Assert.False(FontCoverage.CoversAll(font, ['a', '\u1ED9']));
    }

    [Fact]
    public void A_Long_Table_Is_Read_When_The_File_Holds_One() {
        var font = TestFont.Long((0x1F600, 0x1F642));

        Assert.True(FontCoverage.Covers(font, 0x1F642));
        Assert.False(FontCoverage.Covers(font, 'a'));
    }
}
