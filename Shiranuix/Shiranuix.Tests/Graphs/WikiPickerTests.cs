using Shiranuix.Graphs.Dialogs;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The picker: the cast of every story, offered by name, narrowed by what
/// the writer remembers — and a word saying where characters are made when
/// the wiki holds nobody at all.
/// </summary>
public sealed class WikiPickerTests {
    private readonly FakeWikiStore _wiki = new();

    [Fact]
    public void The_Picker_Offers_The_Cast_Of_Every_Story() {
        _wiki.CreateCharacter("Mara", "", "a witch of the fen", "");
        _wiki.CreateCharacter("Tomas", "", "the one who teaches", "");

        var viewModel = NewPicker();

        Assert.Equal(
            new[] { "Mara", "Tomas" },
            viewModel.Characters.Select(character => character.Name));
        Assert.False(viewModel.IsEmpty);
    }

    [Fact]
    public void The_Search_Narrows_The_List_By_Name_Or_By_What_The_Writer_Said() {
        _wiki.CreateCharacter("Mara", "", "a witch of the fen", "");
        _wiki.CreateCharacter("Tomas", "", "the one who teaches", "");

        var viewModel = NewPicker();

        viewModel.Search = "mara";
        Assert.Equal("Mara", Assert.Single(viewModel.Visible).Name);

        viewModel.Search = "teaches";
        Assert.Equal("Tomas", Assert.Single(viewModel.Visible).Name);

        viewModel.Search = "";
        Assert.Equal(2, viewModel.Visible.Count);
    }

    [Fact]
    public void A_Wiki_With_Nobody_In_It_Says_That_It_Is_Empty() {
        var viewModel = NewPicker();

        Assert.True(viewModel.IsEmpty);
        Assert.Empty(viewModel.Visible);
    }

    [Fact]
    public void A_Character_Made_While_The_Picker_Stands_Open_Is_In_The_List_At_Once() {
        var viewModel = NewPicker();
        Assert.True(viewModel.IsEmpty);

        _wiki.CreateCharacter("Mara", "", "a witch of the fen", "");

        Assert.False(viewModel.IsEmpty);
        Assert.Equal("Mara", Assert.Single(viewModel.Visible).Name);
    }

    [Fact]
    public void The_Picker_Asks_For_The_Wiki_Where_Characters_Are_Made() {
        var viewModel = NewPicker();
        var asked = 0;
        viewModel.WikiAsked += () => asked++;

        viewModel.OpenWikiCommand.Execute(null);

        Assert.Equal(1, asked);
    }

    /// <summary>
    /// A picker over a wiki of the test's own.
    /// </summary>
    /// <returns>The picker.</returns>
    private WikiPickerViewModel NewPicker() => new(_wiki);
}
