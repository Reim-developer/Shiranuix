namespace Shiranuix.CoreEditor.Tests.Document;

/// <summary>
/// Tests for <see cref="FindState"/>: where the find starts, how it
/// counts, and the words the bar speaks when there is nothing to show.
///
/// The search runs off the calling thread, so each test asks for its
/// outcome rather than assuming one — the state hands results back
/// through the door it was given, and the tests hand it one that runs
/// on the spot.
/// </summary>
public sealed class FindStateTests {
    private const string Prose =
        "Một ngày mùa thu, gió mang theo mùa. Mùa thu về, lá rụng đầy mặt đất.";

    [Fact]
    public void A_Query_Counts_The_Runs_And_Says_Which_Is_Shown() {
        var state = NewState(Prose, caret: 0);

        state.Query = "mùa";
        WaitFor(() => state.MatchCount == 3);

        Assert.Equal(3, state.MatchCount);
        Assert.Equal("1 of 3", state.Status);
    }

    [Fact]
    public void The_First_Run_Is_The_One_After_The_Caret() {
        var second = Prose.IndexOf("mùa", Prose.IndexOf("mùa", StringComparison.Ordinal) + 1,
            StringComparison.Ordinal);
        var state = NewState(Prose, caret: second + 1);

        state.Query = "mùa";
        WaitFor(() => state.MatchCount == 3);

        // The writer was reading toward the third run — the caret sits
        // inside the second — so that is the one the bar offers, not the
        // one at the top of the page.
        Assert.Equal("3 of 3", state.Status);
    }

    [Fact]
    public void A_Caret_Past_Every_Run_Falls_Back_To_The_First() {
        var state = NewState(Prose, caret: Prose.Length);

        state.Query = "mùa";
        WaitFor(() => state.MatchCount == 3);

        Assert.Equal("1 of 3", state.Status);
    }

    [Fact]
    public void Next_Wraps_Around_The_Last_Run() {
        var state = NewState(Prose, caret: 0);
        state.Query = "mùa";
        WaitFor(() => state.Status == "1 of 3");

        state.Next();
        state.Next();
        state.Next();

        Assert.Equal("1 of 3", state.Status);
    }

    [Fact]
    public void Previous_Wraps_Around_To_The_Last_Run() {
        var state = NewState(Prose, caret: 0);
        state.Query = "mùa";
        WaitFor(() => state.Status == "1 of 3");

        state.Previous();

        Assert.Equal("3 of 3", state.Status);
    }

    [Fact]
    public void A_Query_With_No_Run_Says_No_Results() {
        var state = NewState(Prose, caret: 0);

        state.Query = "mùa đông";

        WaitFor(() => state.Status == "No results");
        Assert.Equal(0, state.MatchCount);
        Assert.Equal(-1, state.CurrentIndex);
    }

    [Fact]
    public void A_Query_That_Will_Not_Run_Says_Invalid_Regex() {
        var state = NewState(Prose, caret: 0);
        state.UseRegex = true;

        state.Query = "[unbalanced";
        WaitFor(() => state.Status == "Invalid regex");

        Assert.Equal(0, state.MatchCount);
    }

    [Fact]
    public void Closing_Takes_The_Runs_And_The_Count_With_Them() {
        var state = NewState(Prose, caret: 0);
        state.Open();
        state.Query = "mùa";
        WaitFor(() => state.MatchCount == 3);

        state.Close();

        Assert.False(state.IsOpen);
        Assert.Equal(0, state.MatchCount);
        Assert.Equal(string.Empty, state.Status);
    }

    /// <summary>
    /// A state over a fixed text and caret, handing its results back on
    /// the spot rather than through the window's thread.
    /// </summary>
    private static FindState NewState(string text, int caret) =>
        new(() => text, () => caret, action => action());

    /// <summary>Waits, briefly, for the search off the thread to land.</summary>
    private static void WaitFor(Func<bool> condition) {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline) {
            if (condition()) {
                return;
            }

            Thread.Sleep(20);
        }

        Assert.Fail("the find never reached the state asked for");
    }
}
