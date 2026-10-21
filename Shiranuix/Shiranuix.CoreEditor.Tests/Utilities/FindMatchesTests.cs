namespace Shiranuix.CoreEditor.Tests.Utilities;

/// <summary>
/// Tests for <see cref="FindMatches"/>: the rules that decide what counts
/// as a hit, and that a query which cannot be run says so.
/// </summary>
public sealed class FindMatchesTests {
    [Fact]
    public void Finds_Every_Occurrence_In_Order() {
        var hits = FindMatches.Find("a b a c a", "a", matchCase: true, wholeWord: true, useRegex: false, out var error);

        Assert.Equal(FindQueryError.None, error);
        Assert.Equal(3, hits.Count);
        Assert.Equal(new[] { 0, 4, 8 }, hits.Select(m => m.Start).ToArray());
        Assert.All(hits, m => Assert.Equal(1, m.Length));
    }

    [Fact]
    public void Matching_Is_Case_insensitive_By_Default_When_Asked() {
        var hits = FindMatches.Find("Hello hello HELLO", "hello", matchCase: false, wholeWord: true, useRegex: false, out _);

        Assert.Equal(3, hits.Count);
    }

    [Fact]
    public void Matching_Is_Case_sensitive_When_Asked() {
        var hits = FindMatches.Find("Hello hello HELLO", "Hello", matchCase: true, wholeWord: true, useRegex: false, out _);

        Assert.Single(hits);
    }

    [Fact]
    public void Whole_Word_Rejects_A_Substring_Hit() {
        var hits = FindMatches.Find("thereabout, not there", "there", matchCase: true, wholeWord: true, useRegex: false, out _);

        Assert.Single(hits);
    }

    [Fact]
    public void Regex_Matches_Is_Treated_Literally_When_It_Is_Off() {
        var hits = FindMatches.Find("dol...or", "dol...or", matchCase: true, wholeWord: false, useRegex: false, out _);

        Assert.Single(hits);
    }

    [Fact]
    public void Regex_Matches_When_Turned_On() {
        var hits = FindMatches.Find("cat dog cat", @"cat|dog", matchCase: true, wholeWord: true, useRegex: true, out _);

        Assert.Equal(3, hits.Count);
    }

    [Fact]
    public void An_Empty_Query_Names_Nothing() {
        var hits = FindMatches.Find("anything", "", matchCase: true, wholeWord: true, useRegex: false, out var error);

        Assert.Empty(hits);
        Assert.Equal(FindQueryError.None, error);
    }

    [Fact]
    public void A_Bad_Regex_Reports_Its_Error() {
        var hits = FindMatches.Find("anything", "[unbalanced", matchCase: true, wholeWord: false, useRegex: true, out var error);

        Assert.Empty(hits);
        Assert.Equal(FindQueryError.BadRegex, error);
    }
}
