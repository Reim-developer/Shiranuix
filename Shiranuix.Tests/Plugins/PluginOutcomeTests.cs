using Shiranuix.Plugins;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PluginOutcome"/> and <see cref="PluginOutcome{T}"/>:
/// the answer a call gives back instead of an exception across the boundary.
/// </summary>
public sealed class PluginOutcomeTests {
    [Fact]
    public void A_Call_That_Worked_Carries_No_Reason() {
        var outcome = PluginOutcome.Ok();

        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void A_Refusal_Says_Why() {
        var outcome = PluginOutcome.Failure("That range is not in this document.");

        Assert.False(outcome.IsSuccess);
        Assert.Equal("That range is not in this document.", outcome.Error);
    }

    [Fact]
    public void A_Refusal_With_Nothing_To_Say_Is_Not_Made() {
        Assert.Throws<ArgumentException>(() => PluginOutcome.Failure("   "));
    }

    [Fact]
    public void A_Value_Rides_Home_On_A_Call_That_Worked() {
        var outcome = PluginOutcome<string>.From("chapter twelve");

        Assert.True(outcome.IsSuccess);
        Assert.Equal("chapter twelve", outcome.Value);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void Taking_The_Value_Of_A_Refusal_Is_A_Mistake_Worth_Hearing_About() {
        var outcome = PluginOutcome<int>.Failure("The writer closed the question.");

        var thrown = Assert.Throws<InvalidOperationException>(() => outcome.Value);

        Assert.Contains("The writer closed the question.", thrown.Message);
    }

    [Fact]
    public void Asking_Whether_There_Is_A_Value_Never_Throws() {
        var refused = PluginOutcome<bool>.Failure("No document is open.");
        var agreed = PluginOutcome<bool>.From(true);

        Assert.False(refused.TryGetValue(out _));
        Assert.True(agreed.TryGetValue(out var answer));
        Assert.True(answer);
    }
}
