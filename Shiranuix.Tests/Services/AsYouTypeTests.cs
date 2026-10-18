using Shiranuix.Models;
using Shiranuix.Services;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="AsYouType"/>: what a stroke becomes on the page.
/// </summary>
/// <remarks>
/// All of it runs without a window: the tool answers with the turn, and the
/// window's ear is what carries it out. These are the rules about sides and
/// sizes - which is exactly the kind of thing typing cannot prove but a
/// table can.
/// </remarks>
public sealed class AsYouTypeTests {
    private static readonly TypingRules Curly = new(true, DashMode.Em);

    [Fact]
    public void A_Quote_With_Nothing_Before_It_Takes_Its_Opening() {
        var turn = AsYouType.Stroke('"', null, Curly);

        Assert.Equal("\u201C", turn?.Text);
        Assert.Equal(0, turn?.Replaces);
    }

    [Fact]
    public void A_Quote_After_A_Word_Takes_Its_Closing() {
        Assert.Equal("\u201D", AsYouType.Stroke('"', 't', Curly)?.Text);
    }

    [Fact]
    public void A_Quote_After_A_Number_Takes_Its_Closing() {
        Assert.Equal("\u201D", AsYouType.Stroke('"', '0', Curly)?.Text);
    }

    [Fact]
    public void A_Tick_After_A_Word_Takes_Its_Closing() {
        Assert.Equal("\u2019", AsYouType.Stroke('\'', 't', Curly)?.Text);
    }

    [Fact]
    public void A_Tick_With_Nothing_Before_It_Takes_Its_Opening() {
        Assert.Equal("\u2018", AsYouType.Stroke('\'', null, Curly)?.Text);
    }

    [Fact]
    public void A_Quote_After_A_Pause_Takes_Its_Closing() {
        Assert.Equal("\u201D", AsYouType.Stroke('"', ',', Curly)?.Text);
    }

    [Fact]
    public void A_Quote_After_A_Dash_Goes_On_Opening() {
        Assert.Equal("\u201C", AsYouType.Stroke('"', '\u2014', Curly)?.Text);
    }

    [Fact]
    public void A_Quote_After_An_Open_Bracket_Goes_On_Opening() {
        Assert.Equal("\u201C", AsYouType.Stroke('"', '(', Curly)?.Text);
    }

    [Fact]
    public void A_Quote_After_A_Shut_Speech_Opens_Again() {
        Assert.Equal("\u201C", AsYouType.Stroke('"', '\u201D', Curly)?.Text);
    }

    [Fact]
    public void A_Quote_Doubled_Takes_Its_Closing() {
        Assert.Equal("\u201D", AsYouType.Stroke('"', '\u201C', Curly)?.Text);
    }

    [Fact]
    public void A_Tick_Doubled_Takes_Its_Closing() {
        Assert.Equal("\u2019", AsYouType.Stroke('\'', '\u2018', Curly)?.Text);
    }

    [Fact]
    public void A_Quote_The_Settings_Leave_Alone_Stays_As_Typed() {
        var rules = new TypingRules(SmartQuotes: false, Dash: DashMode.Em);

        Assert.Null(AsYouType.Stroke('"', null, rules));
        Assert.Null(AsYouType.Stroke('\'', 't', rules));
    }

    [Fact]
    public void A_Pair_Of_Hyphens_Takes_The_Long_Dash_The_Settings_Say() {
        var turn = AsYouType.Stroke('-', '-', Curly);

        Assert.Equal("\u2014", turn?.Text);
        Assert.Equal(1, turn?.Replaces);
    }

    [Fact]
    public void A_Pair_Of_Hyphens_Takes_The_Short_Dash_The_Settings_Say() {
        var rules = new TypingRules(SmartQuotes: true, Dash: DashMode.En);

        Assert.Equal("\u2013", AsYouType.Stroke('-', '-', rules)?.Text);
    }

    [Fact]
    public void A_Pair_Of_Hyphens_The_Settings_Leave_Alone_Stay_Hyphens() {
        var rules = new TypingRules(SmartQuotes: true, Dash: DashMode.Off);

        Assert.Null(AsYouType.Stroke('-', '-', rules));
    }

    [Fact]
    public void A_Lone_Hyphen_Stays_A_Hyphen() {
        Assert.Null(AsYouType.Stroke('-', 'w', Curly));
    }

    [Fact]
    public void A_Hyphen_After_A_Dash_Stands_As_Typed() {
        Assert.Null(AsYouType.Stroke('-', '\u2014', Curly));
    }
}
