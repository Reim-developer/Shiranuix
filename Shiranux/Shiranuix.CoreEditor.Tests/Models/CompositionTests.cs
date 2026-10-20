namespace Shiranuix.CoreEditor.Tests.Models;

/// <summary>
/// Tests for <see cref="Composition"/>: what an input method is composing at
/// the caret, as the view hands it over.
/// </summary>
/// <remarks>
/// The interesting cases are the ones an input method leaves out. A Vietnamese
/// or Japanese writer's keystroke arrives with a cursor position only sometimes,
/// and the fallback has to be the one the editor draws its own caret with — the
/// column the status bar prints is being compared against a caret on screen.
/// </remarks>
public sealed class CompositionTests {
    [Fact]
    public void Nothing_Composing_Is_None() {
        Assert.Equal(Composition.None, Composition.Of(null, null));
        Assert.Equal(Composition.None, Composition.Of(string.Empty, 0));
        Assert.True(Composition.None.IsEmpty);
    }

    [Fact]
    public void An_Unsaid_Cursor_Sits_At_The_End_Of_The_Composition() {
        // The fallback the presenter draws with: an input method that says
        // nothing is read as composing to its end, which is where the caret
        // looks while a diacritic is being typed.
        Assert.Equal(new Composition("â", 1), Composition.Of("â", null));
        Assert.Equal(new Composition("â", 1), Composition.Of("â", -1));
        Assert.Equal(new Composition("â", 1), Composition.Of("â", 9));
    }

    [Fact]
    public void A_Said_Cursor_Is_Taken_As_It_Is() {
        Assert.Equal(new Composition("â", 0), Composition.Of("â", 0));
        Assert.Equal(new Composition("aâ", 1), Composition.Of("aâ", 1));
    }
}
