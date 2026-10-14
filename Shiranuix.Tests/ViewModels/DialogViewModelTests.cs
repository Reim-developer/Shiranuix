using System.Windows.Input;
using Shiranuix.Services;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="DialogViewModel{TResult}"/>: one window's contents,
/// whatever it is being asked.
/// </summary>
/// <remarks>
/// The dialog's surface is plain and its answer is typed, so the whole thing
/// can be exercised headlessly — no window, no click, no application.
/// </remarks>
public sealed class DialogViewModelTests {
    [Fact]
    public void The_View_Model_Shows_The_Request() {
        var request = new DialogRequest<bool>(
            "Unsaved changes",
            "\"story.txt\" has unsaved changes.\nDiscard them?",
            new[] { new DialogButton<bool>("Keep editing", false), new DialogButton<bool>("Discard", true) });

        var dialog = new DialogViewModel<bool>(request, _ => { });

        Assert.Equal("Unsaved changes", dialog.Title);
        Assert.Contains("story.txt", dialog.Message);
        Assert.Equal(new[] { "Keep editing", "Discard" },
            dialog.Buttons.Select(button => button.Label).ToArray());
    }

    [Fact]
    public void Answering_With_A_Button_Answers_With_What_It_Carries() {
        var answered = false;
        var dialog = BuildDialog(answer => answered = answer);

        dialog.Answer(dialog.Buttons.Single(button => button.Label == "Discard"));

        Assert.True(answered);
    }

    [Fact]
    public void Answering_Keeps_The_Work_When_The_Button_Says_So() {
        var answered = true;
        var dialog = BuildDialog(answer => answered = answer);

        dialog.Answer(dialog.Buttons.Single(button => button.Label == "Keep editing"));

        Assert.False(answered);
    }

    [Fact]
    public void A_Button_From_Another_Dialog_Answers_Nothing() {
        var answered = true;
        var dialog = BuildDialog(answer => answered = answer);

        // A button typed for a different answer type is not this dialog's.
        dialog.Answer(new DialogButton<int>("Something else", 7));

        Assert.True(answered);
    }

    [Fact]
    public void The_Answer_Command_Takes_The_Button_It_Is_Handed() {
        var answered = false;
        var dialog = BuildDialog(answer => answered = answer);

        dialog.AnswerCommand.Execute(dialog.Buttons.Single(button => button.Label == "Discard"));

        Assert.True(answered);
        Assert.IsAssignableFrom<ICommand>(dialog.AnswerCommand);
    }

    [Fact]
    public void The_Answers_Never_Change() {
        var dialog = BuildDialog(_ => { });
        var before = dialog.Buttons;

        Assert.Same(before, dialog.Buttons);
    }

    private static DialogViewModel<bool> BuildDialog(Action<bool> answer) =>
        new(new DialogRequest<bool>(
            "Unsaved changes",
            "\"story.txt\" has unsaved changes.",
            new[] {
                new DialogButton<bool>("Keep editing", false),
                new DialogButton<bool>("Discard", true)
            }),
            answer);
}
