namespace Shiranuix.CoreEditor.Tests.Models;

/// <summary>
/// Tests for <see cref="TextPosition"/>, where a caret's offset puts it in a
/// text.
/// </summary>
/// <remarks>
/// The interesting cases are the ones a writer hits without thinking: a break
/// in the middle of a line, a carriage return that is only half a break, and an
/// offset a stale binding offered past the end of the text.
/// </remarks>
public sealed class TextPositionTests {
    [Fact]
    public void An_Empty_Text_Starts_At_Line_One_Column_One() {
        Assert.Equal(new TextPosition(1, 1), TextPosition.Of(string.Empty, 0));
    }

    [Fact]
    public void Columns_Count_From_One_Not_Zero() {
        Assert.Equal(new TextPosition(1, 1), TextPosition.Of("abc", 0));
        Assert.Equal(new TextPosition(1, 4), TextPosition.Of("abc", 3));
    }

    [Fact]
    public void A_Newline_Moves_To_The_Next_Line() {
        var text = "alpha\nbeta";

        Assert.Equal(new TextPosition(1, 6), TextPosition.Of(text, 5));
        Assert.Equal(new TextPosition(2, 1), TextPosition.Of(text, 6));
        Assert.Equal(new TextPosition(2, 5), TextPosition.Of(text, 10));
    }

    [Fact]
    public void Several_Newlines_Count_Every_Line() {
        Assert.Equal(new TextPosition(4, 1), TextPosition.Of("a\nb\nc\nd", 6));
    }

    [Fact]
    public void An_Offset_Past_The_End_Is_Clamped() {
        // A stale binding can offer one; the position is a place in the text, so
        // it cannot leave it.
        Assert.Equal(new TextPosition(2, 2), TextPosition.Of("a\nb", 99));
    }

    [Fact]
    public void A_Carriage_Return_Is_Half_A_Break() {
        // "one\r\ntwo": the return is not a break of its own, so the caret after
        // it is still on the first line.
        var text = "one\r\ntwo";

        Assert.Equal(new TextPosition(1, 4), TextPosition.Of(text, 3));
        Assert.Equal(new TextPosition(1, 5), TextPosition.Of(text, 4));
        Assert.Equal(new TextPosition(2, 1), TextPosition.Of(text, 5));
    }

    [Fact]
    public void A_Document_Starts_At_The_Same_Place() {
        Assert.Equal(new TextPosition(1, 1), TextPosition.Start);
        Assert.Equal(new TextPosition(1, 1), TextPosition.Of("anything", 0));
    }
}
