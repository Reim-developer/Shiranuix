using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Tests.Fakes;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="DialogCapability"/>: the editor's own questions in the
/// shape a plugin asks them, including the one it cannot answer.
/// </summary>
public sealed class DialogCapabilityTests {
    private readonly FakeDialogService _questions = new();
    private readonly FakeFileDialogService _files = new();

    [Fact]
    public async Task Confirming_Asks_With_The_Labels_It_Was_Given() {
        var dialogs = Create();
        _questions.Result = true;

        var outcome = await dialogs.ConfirmAsync(
            new PluginConfirm("Delete this file?", "Remove", "Remove it", "Keep it"),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.Value);

        var asked = Assert.Single(_questions.Asked);
        Assert.Equal("Remove", asked.Title);
        Assert.Equal(["Remove it", "Keep it"], asked.Buttons);
    }

    [Fact]
    public async Task A_Writer_Who_Closes_The_Question_Answers_Nothing() {
        var dialogs = Create();
        _questions.Result = null;

        var outcome = await dialogs.ConfirmAsync(
            new PluginConfirm("Delete this file?"), CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("closed the question", outcome.Error);
    }

    [Fact]
    public async Task Choosing_A_Folder_Hands_Back_What_The_Writer_Picked() {
        var dialogs = Create();
        _files.FolderResult = "D:\\stories";

        var outcome = await dialogs.PickFolderAsync(
            new PluginFolderPrompt("Where should the tree stand?"),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("D:\\stories", outcome.Value);
    }

    [Fact]
    public async Task A_Writer_Who_Closes_The_Picker_Chose_Nothing() {
        var dialogs = Create();
        _files.FolderResult = null;

        var outcome = await dialogs.PickFolderAsync(
            new PluginFolderPrompt("Where should the tree stand?"),
            CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("closed the picker", outcome.Error);
    }

    [Fact]
    public async Task Asking_For_A_Line_Of_Text_Says_The_Editor_Cannot_Yet() {
        var dialogs = Create();

        var outcome = await dialogs.PromptAsync(
            new PluginPrompt("What is it called?"), CancellationToken.None);

        // Said plainly rather than pretended: a plugin that wants a name asked
        // for should ask inside its own panel, which is what a file tree does.
        Assert.False(outcome.IsSuccess);
        Assert.Contains("cannot ask for a line of text", outcome.Error);
    }

    private IPluginDialog Create() => new DialogCapability(_questions, _files);
}
