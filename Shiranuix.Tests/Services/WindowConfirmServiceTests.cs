using Shiranuix.Services;
using Shiranuix.Tests.Fakes;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.Services;

/// <summary>
/// Tests for <see cref="WindowConfirmService"/>, the discard prompt's contents.
/// </summary>
/// <remarks>
/// The prompt itself is a window and a dialog request; what is worth testing
/// headlessly is the question it asks and how the answers map back — a prompt
/// that discards when the user said "keep" is the one failure that costs work.
/// </remarks>
public sealed class WindowConfirmServiceTests {
    private readonly FakeDialogService _dialogs = new();

    private WindowConfirmService CreateService() => new(_dialogs);

    [Fact]
    public async Task Discarding_Answers_True() {
        _dialogs.Result = true;
        var service = CreateService();

        Assert.True(await service.ConfirmDiscardAsync("story.txt"));
        Assert.Equal("story.txt", _dialogs.LastFileName);
    }

    [Fact]
    public async Task Keeping_The_Work_Answers_False() {
        _dialogs.Result = false;
        var service = CreateService();

        Assert.False(await service.ConfirmDiscardAsync("story.txt"));
    }

    [Fact]
    public async Task A_Dismissed_Dialog_Keeps_The_Work() {
        // The window closed without an answer - the Escape answer is keep, so
        // this is the same reply.
        _dialogs.Result = null;
        var service = CreateService();

        Assert.False(await service.ConfirmDiscardAsync("story.txt"));
    }

    [Fact]
    public async Task The_Prompt_Asks_About_The_File_By_Name() {
        var service = CreateService();

        await service.ConfirmDiscardAsync("chapters\\one.txt");

        var request = _dialogs.LastRequest;
        Assert.NotNull(request);
        Assert.Equal("Unsaved changes", request.Title);
        Assert.Contains("one.txt", request.Message);
        Assert.Contains("unsaved changes", request.Message);
    }

    [Fact]
    public async Task Keeping_Is_The_Default_And_The_Cancel_Answer() {
        var service = CreateService();

        await service.ConfirmDiscardAsync("story.txt");

        var buttons = _dialogs.LastRequest!.Buttons;
        var keep = buttons.Single(button => button.Label == "Keep editing");
        var discard = buttons.Single(button => button.Label == "Discard");

        // Losing a chapter to a stray keypress is the failure worth designing
        // against, so Enter and Escape both keep the work.
        Assert.True(keep.IsDefault);
        Assert.True(keep.IsCancel);
        Assert.False(discard.IsDefault);
        Assert.False(discard.IsCancel);

        // Keep answers false, discard answers true.
        Assert.False(keep.Result);
        Assert.True(discard.Result);
    }
}
