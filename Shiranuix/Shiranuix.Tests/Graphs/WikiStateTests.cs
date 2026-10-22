using Avalonia.Controls;
using Avalonia.Headless;
using Shiranuix.Graphs;
using Shiranuix.Tests.Fakes;
using Shiranuix.Tests.Headless;

namespace Shiranuix.Tests.Graphs;

/// <summary>
/// The wiki's own memory of what the writer has told it: the characters it
/// holds, the account of the one that has been chosen, and the question it
/// asks before taking a character away.
/// </summary>
[Collection("Headless UI")]
public sealed class WikiStateTests {
    private readonly FakeWikiStore _wiki = new();
    private readonly FakeConfirmQuestion _confirm = new();

    [Fact]
    public void A_Character_Put_Into_The_Wiki_Comes_Back_Out_Of_It() =>
        HeadlessUi.On(() => {
            var state = NewState();

            state.Name = "Mara";
            state.BirthDate = "the night of the storm";
            state.Description = "a witch";
            state.Note = "the writer's note";
            state.SaveCommand.Execute(null);

            var character = Assert.Single(state.Characters);
            Assert.Equal("Mara", character.Name);
            Assert.Equal("the night of the storm", character.BirthDate);
            Assert.Equal("a witch", character.Description);
            Assert.Equal("the writer's note", character.Note);
        });

    [Fact]
    public void A_Character_Without_A_Name_Is_Refused_And_Keeps_Out_Of_The_Wiki() =>
        HeadlessUi.On(() => {
            var state = NewState();

            state.Name = "   ";
            state.SaveCommand.Execute(null);

            Assert.Empty(state.Characters);
            Assert.Equal("A character needs a name.", state.Error);
        });

    [Fact]
    public void Choosing_A_Character_Shows_Their_Account_On_The_Form() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "the first of spring", "a witch", "a note");

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);

            Assert.Equal("Mara", state.Name);
            Assert.Equal("the first of spring", state.BirthDate);
            Assert.Equal("a witch", state.Description);
            Assert.Equal("a note", state.Note);
        });

    [Fact]
    public void Saving_Changes_The_Character_That_Was_Chosen_And_Not_Another() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");
            _wiki.CreateCharacter("Tomas", "", "", "");

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);
            state.Name = "Mara of the Marsh";
            state.SaveCommand.Execute(null);

            var characters = _wiki.ListCharacters();
            Assert.Equal(2, characters.Count);
            Assert.Equal("Mara of the Marsh", characters.Single(character => character.Id == id).Name);
        });

    [Fact]
    public void Taking_A_Character_Out_Asks_First_And_Says_How_Many_Nodes_Go_With_Them() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");

            // A character standing in a map: the writer is told how many
            // nodes will go with them.
            var store = new FakeGraphStore(_wiki);
            var graph = store.CreateGraph("Witch And Magical");
            store.AddCharacter(graph, id, 10, 10);

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);
            state.Owner = new Window();

            state.DeleteCommand.Execute(null);

            var question = Assert.Single(_confirm.Asked);
            Assert.Contains("Mara", question);
            Assert.Contains("1 node", question);
        });

    [Fact]
    public void A_Character_Standing_In_No_Map_Is_Asked_About_Without_A_Count() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);
            state.Owner = new Window();

            state.DeleteCommand.Execute(null);

            var question = Assert.Single(_confirm.Asked);
            Assert.Contains("Mara", question);
            Assert.DoesNotContain("node", question);
        });

    [Fact]
    public void A_Character_Refused_Stays_In_The_Wiki() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");
            _confirm.Answer = false;

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);
            state.Owner = new Window();

            state.DeleteCommand.Execute(null);

            Assert.Single(_wiki.ListCharacters());
        });

    [Fact]
    public void A_Character_Agreed_To_Goes_Out_Of_The_Wiki() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);
            state.Owner = new Window();

            state.DeleteCommand.Execute(null);

            Assert.Empty(_wiki.ListCharacters());
            Assert.Null(state.Selected);
        });

    [Fact]
    public void A_New_Character_Empties_The_Form_And_Asks_For_Nothing_Yet() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);

            state.NewCharacterCommand.Execute(null);

            Assert.Null(state.Selected);
            Assert.Equal(string.Empty, state.Name);
        });

    [Fact]
    public void A_Characters_Face_Is_Kept_With_Them() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);

            state.Avatar = [1, 2, 3];
            state.SaveCommand.Execute(null);

            Assert.Equal(new byte[] { 1, 2, 3 }, _wiki.GetAvatar(id));
        });

    [Fact]
    public void A_Characters_Face_Comes_Back_With_Them_When_They_Are_Chosen_Again() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");
            _wiki.SetAvatar(id, [1, 2, 3]);

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);

            Assert.Equal(new byte[] { 1, 2, 3 }, state.Avatar);
        });

    [Fact]
    public void A_New_Character_Comes_With_No_Face() =>
        HeadlessUi.On(() => {
            var state = NewState();
            var id = _wiki.CreateCharacter("Mara", "", "", "");
            _wiki.SetAvatar(id, [1, 2, 3]);

            state.Load();
            state.Selected = state.Characters.Single(character => character.Id == id);
            state.NewCharacterCommand.Execute(null);

            Assert.Null(state.Avatar);
        });

    /// <summary>
    /// The wiki's own state, over a wiki of the test's own.
    /// </summary>
    /// <returns>The state.</returns>
    private WikiState NewState() => new(_wiki, _confirm);
}
