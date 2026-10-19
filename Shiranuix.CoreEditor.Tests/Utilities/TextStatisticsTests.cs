namespace Shiranuix.CoreEditor.Tests.Utilities;

/// <summary>
/// Tests for <see cref="TextStatistics"/>, the three counters in the status bar.
/// </summary>
/// <remarks>
/// The Vietnamese cases are the ones that matter. Splitting on ASCII letters
/// rather than whitespace would collapse "người đó" into one word and strand
/// the diacritics, so the suite pins both behaviours down.
/// </remarks>
public sealed class TextStatisticsTests {
    [Fact]
    public void Empty_Text_Measures_To_Zero() {
        var result = TextStatistics.Measure(string.Empty);

        Assert.Equal(new TextStatisticsResult(0, 0, 0), result);
    }

    [Fact]
    public void Null_Measures_As_Empty() {
        var result = TextStatistics.Measure(null);

        Assert.Equal(new TextStatisticsResult(0, 0, 0), result);
    }

    [Fact]
    public void Words_Are_Runs_Of_NonWhitespace() {
        // Tabs, newlines and runs of spaces all separate words the same way.
        var result = TextStatistics.Measure("one\ttwo   three\nfour");

        Assert.Equal(4, result.Words);
    }

    [Fact]
    public void Vietnamese_Syllables_Split_By_Space_Are_Separate_Words() {
        // "người đó" is two words to a writer, even though each syllable is
        // itself meaningful. Whitespace is the only delimiter.
        var result = TextStatistics.Measure("người đó");

        Assert.Equal(2, result.Words);
    }

    [Fact]
    public void Diacritics_Do_Not_Split_A_Word() {
        // A word made entirely of non-ASCII characters still counts once.
        var result = TextStatistics.Measure("chẳng");

        Assert.Equal(1, result.Words);
    }

    [Fact]
    public void Punctuation_Attaches_To_Its_Word() {
        var result = TextStatistics.Measure("yes, quite so.");

        Assert.Equal(3, result.Words);
    }

    [Fact]
    public void Whitespace_Only_Text_Has_No_Words() {
        // 3 spaces + newline + tab + 2 spaces.
        var result = TextStatistics.Measure("   \n\t  ");

        Assert.Equal(0, result.Words);
        Assert.Equal(7, result.Characters);
    }

    [Fact]
    public void Lines_Are_One_More_Than_Newlines() {
        var result = TextStatistics.Measure("a\nb\nc");

        Assert.Equal(3, result.Lines);
        Assert.Equal(5, result.Characters);
    }

    [Fact]
    public void Text_Without_Newlines_Is_Single_Line() {
        var result = TextStatistics.Measure("one line");

        Assert.Equal(1, result.Lines);
    }

    [Fact]
    public void Trailing_Newline_Yields_An_Empty_Final_Line() {
        // A file ending in "\n" shows a blank line in the editor, so the
        // counter must see it rather than silently trimming.
        var result = TextStatistics.Measure("a\n");

        Assert.Equal(2, result.Lines);
    }

    [Fact]
    public void Single_Newline_Is_Two_Empty_Lines() {
        var result = TextStatistics.Measure("\n");

        Assert.Equal(2, result.Lines);
        Assert.Equal(0, result.Words);
    }

    [Fact]
    public void Characters_Count_Everything_Including_Whitespace() {
        // The status bar says "characters"; a writer counts keystrokes and
        // spaces, which is exactly what Length reports. Diacritics count as
        // one each, so a Vietnamese syllable of four letters is four.
        var result = TextStatistics.Measure("hi there");

        Assert.Equal(8, result.Characters);
    }
}
