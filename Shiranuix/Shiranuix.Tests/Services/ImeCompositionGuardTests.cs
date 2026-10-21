using Shiranuix.Services;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="ImeCompositionGuard"/>, the ceiling that commits a
/// composition before the input method freezes on it.
/// </summary>
/// <remarks>
/// What is worth testing headlessly is the decision — the commit itself is a
/// notification the input method answers in its own time, and the editor
/// already has tests for text arriving. The line the tests hold is the one
/// between a composition the input method can still take and one it cannot:
/// too late and the freeze wins, too early and ordinary typing gets cut.
/// </remarks>
public sealed class ImeCompositionGuardTests {
    private static ImeCompositionGuard CreateService() => new();

    [Fact]
    public void A_Composition_Under_The_Ceiling_Is_Left_To_The_Input_Method() {
        var guard = CreateService();

        Assert.False(guard.ShouldComplete(null));
        Assert.False(guard.ShouldComplete(string.Empty));
        Assert.False(guard.ShouldComplete(new string('a', ImeCompositionGuard.CommitLength - 1)));
    }

    [Fact]
    public void A_Composition_At_The_Ceiling_Is_Committed_Immediately() {
        var guard = CreateService();

        Assert.True(guard.ShouldComplete(new string('a', ImeCompositionGuard.CommitLength)));
        Assert.True(guard.ShouldComplete(new string('a', ImeCompositionGuard.CommitLength + 1)));
    }

    [Fact]
    public void The_Ceiling_Stays_Under_The_Input_Methods_Own_Limit() {
        // The input method freezes on its thirtieth character; the commit has
        // to fire with room to spare for a keystroke racing it.
        Assert.True(ImeCompositionGuard.CommitLength < 30);
    }

    [Fact]
    public void Completing_Without_A_Window_Sends_Nothing() {
        var guard = CreateService();

        // No window, no input context: the ask simply does not go out, and
        // nothing about it throws.
        guard.Complete(IntPtr.Zero);
    }
}
