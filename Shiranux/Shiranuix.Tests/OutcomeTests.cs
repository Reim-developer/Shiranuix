using Shiranuix;

namespace Shiranuix.Tests;

/// <summary>
/// Tests for <see cref="Outcome"/> and <see cref="Outcome{T}"/>: the answer a
/// call gives back instead of an exception.
/// </summary>
public sealed class OutcomeTests {
    [Fact]
    public void A_Call_That_Worked_Carries_No_Reason() {
        var outcome = Outcome.Ok();

        Assert.True(outcome.IsSuccess);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void A_Refusal_Says_Why() {
        var outcome = Outcome.Failure("That range is not in this document.");

        Assert.False(outcome.IsSuccess);
        Assert.Equal("That range is not in this document.", outcome.Error);
    }

    [Fact]
    public void A_Refusal_With_Nothing_To_Say_Is_Not_Made() {
        Assert.Throws<ArgumentException>(() => Outcome.Failure("   "));
    }

    [Fact]
    public void A_Value_Rides_Home_On_A_Call_That_Worked() {
        var outcome = Outcome<string>.From("chapter twelve");

        Assert.True(outcome.IsSuccess);
        Assert.Equal("chapter twelve", outcome.Value);
        Assert.Null(outcome.Error);
    }

    [Fact]
    public void Taking_The_Value_Of_A_Refusal_Is_A_Mistake_Worth_Hearing_About() {
        var outcome = Outcome<int>.Failure("The writer closed the question.");

        var thrown = Assert.Throws<InvalidOperationException>(() => outcome.Value);

        Assert.Contains("The writer closed the question.", thrown.Message);
    }

    [Fact]
    public void Asking_Whether_There_Is_A_Value_Never_Throws() {
        var refused = Outcome<bool>.Failure("No document is open.");
        var agreed = Outcome<bool>.From(true);

        Assert.False(refused.TryGetValue(out var nothing));
        Assert.False(nothing);
        Assert.True(agreed.TryGetValue(out var something));
        Assert.True(something);
    }
}
