using Shiranuix.Plugins;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="TextRange"/> and <see cref="TextPoint"/>: the two
/// shapes a plugin says "where" and "how much" with.
/// </summary>
public sealed class TextGeometryTests {
    [Fact]
    public void A_Range_Ends_Where_The_Next_One_Starts() {
        var range = new TextRange(10, 5);

        Assert.Equal(15, range.End);
        Assert.False(range.IsEmpty);
    }

    [Fact]
    public void A_Range_Holding_Nothing_Is_Empty_And_Says_So() {
        Assert.True(TextRange.Empty.IsEmpty);
        Assert.True(new TextRange(7, 0).IsEmpty);
        Assert.Equal(7, new TextRange(7, 0).Start);
    }

    [Fact]
    public void A_Range_Starting_Below_Zero_Is_Refused() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextRange(-1, 3));
    }

    [Fact]
    public void A_Range_Running_Backwards_Is_Refused() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextRange(0, -1));
    }

    [Fact]
    public void A_Range_Is_Knowable_Only_Against_A_Text() {
        var range = new TextRange(8, 4);

        Assert.True(range.FitsIn(20));
        Assert.False(range.FitsIn(10));
    }

    [Fact]
    public void Pulling_A_Range_Back_Moves_It_To_The_End_Rather_Than_Losing_It() {
        var beyond = new TextRange(30, 10);

        var clamped = beyond.ClampTo(20);

        Assert.Equal(new TextRange(20, 0), clamped);
        Assert.True(clamped.FitsIn(20));
    }

    [Fact]
    public void Pulling_A_Range_Back_Leaves_One_That_Fits_Alone() {
        var range = new TextRange(4, 6);

        Assert.Equal(range, range.ClampTo(20));
    }

    [Fact]
    public void A_Text_Is_Never_Negative() {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextRange.Empty.ClampTo(-1));
    }

    [Fact]
    public void A_Place_Counts_Offsets_From_Zero_And_Lines_From_One() {
        var start = new TextPoint(0, 1, 1);

        Assert.Equal(0, start.Offset);
        Assert.Equal(1, start.Line);
        Assert.Equal(1, start.Column);
    }

    [Fact]
    public void A_Place_Below_Where_Counting_Starts_Is_Refused() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextPoint(-1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextPoint(0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextPoint(0, 1, 0));
    }

    [Fact]
    public void Places_And_Ranges_Are_Values_And_So_Compare_By_What_They_Say() {
        Assert.Equal(new TextRange(3, 4), new TextRange(3, 4));
        Assert.Equal(new TextPoint(9, 2, 5), new TextPoint(9, 2, 5));
        Assert.NotEqual(new TextRange(3, 4), new TextRange(4, 3));
    }
}
