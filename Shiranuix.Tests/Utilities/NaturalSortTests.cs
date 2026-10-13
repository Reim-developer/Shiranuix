using Shiranuix.Utilities;

namespace Shiranuix.Tests.Utilities;

/// <summary>
/// Tests for <see cref="NaturalSort"/>, which orders file names the way a
/// human expects (numeric runs compared numerically).
/// </summary>
public sealed class NaturalSortTests {
    [Fact]
    public void Numbers_Sort_Numerically_Not_Lexicographically() {
        // Lexicographic order would put page10 before page2.
        var files = new List<string> { "page10.png", "page2.png", "page1.png" };
        files.Sort(NaturalSort.Compare);

        Assert.Equal(new[] { "page1.png", "page2.png", "page10.png" }, files);
    }

    [Fact]
    public void Leading_Zeros_Are_Treated_As_The_Same_Number() {
        // "01" and "1" have equal numeric value; "1" (fewer digits) sorts first.
        var files = new List<string> { "01.png", "1.png" };
        files.Sort(NaturalSort.Compare);

        Assert.Equal(new[] { "1.png", "01.png" }, files);
    }

    [Fact]
    public void Comparison_Is_Case_Insensitive() {
        var files = new List<string> { "B.png", "a.png" };
        files.Sort(NaturalSort.Compare);

        Assert.Equal(new[] { "a.png", "B.png" }, files);
    }

    [Fact]
    public void Shorter_String_Sorts_First_When_Prefix_Matches() {
        // "ch1" and "ch1a" share the "ch1" prefix; the shorter one comes first.
        var files = new List<string> { "ch1a.png", "ch1.png" };
        files.Sort(NaturalSort.Compare);

        Assert.Equal(new[] { "ch1.png", "ch1a.png" }, files);
    }

    [Fact]
    public void Null_Sorts_Before_Any_String() {
        Assert.True(NaturalSort.Compare(null, "a") < 0);
        Assert.Equal(0, NaturalSort.Compare(null, null));
    }
}
