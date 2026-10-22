using Shiranuix.Graphs;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The letter a character wears in their circle: the first of their name,
/// standing up, and taken whole where the first letter takes more than one
/// character to write itself.
/// </summary>
public sealed class GraphInitialTests {
    [Fact]
    public void A_Name_Is_Worn_By_Its_First_Letter() {
        Assert.Equal("M", GraphInitial.Of("Mikoto"));
    }

    [Fact]
    public void The_Letter_Stands_Up_Whatever_Case_The_Name_Was_Written_In() {
        Assert.Equal("M", GraphInitial.Of("mara"));
    }

    [Fact]
    public void A_Name_Written_With_Space_Around_It_Is_Worn_By_Its_Letter() {
        Assert.Equal("M", GraphInitial.Of("  Mara  "));
    }

    [Fact]
    public void A_Name_With_No_Letter_In_It_Wears_Nothing() {
        Assert.Equal(string.Empty, GraphInitial.Of("   "));
        Assert.Equal(string.Empty, GraphInitial.Of(null));
    }

    [Fact]
    public void A_Letter_Outside_The_Latin_Alphabet_Is_Worn_Whole() {
        Assert.Equal("Á", GraphInitial.Of("Ánh"));
    }

    [Fact]
    public void A_Letter_That_Takes_Two_Characters_To_Write_Is_Worn_Whole() {
        // A letter beyond the basic plane is written in two units, and the
        // avatar wears both of them rather than half a letter.
        const string letter = "\U0001D49C";

        Assert.Equal(letter, GraphInitial.Of(letter + "the rest of the name"));
    }

    [Fact]
    public void A_Node_Wears_The_First_Letter_Of_Its_Name() {
        var node = new GraphNodeViewModel(1, "Mikoto", "", 0, 0);

        Assert.Equal("M", node.Initial);
    }

    [Fact]
    public void A_Node_Renamed_Wears_The_First_Letter_Of_Its_New_Name() {
        var node = new GraphNodeViewModel(1, "Mikoto", "", 0, 0);

        node.Name = "Tomi";

        Assert.Equal("T", node.Initial);
    }
}
