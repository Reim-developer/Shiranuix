using System.Collections.Generic;
using System.Linq;
using Shiranuix.ViewModels;

namespace Shiranuix.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="EditorViewModel"/>'s face chips: bold, italic and
/// underline, the toggles behind them, and the notifications that move the
/// page when they change.
/// </summary>
/// <remarks>
/// The faces live one object in, and except where a setter is the subject,
/// they are reached through the bar's commands - the path a writer takes.
/// </remarks>
public sealed partial class EditorViewModelTests {
    [Fact]
    public void The_Face_Toggles_Turn_The_Two_Faces_On_And_Off() {
        var viewModel = CreateViewModel();

        // A page is set plain until the writer says otherwise.
        Assert.False(viewModel.Bold);
        Assert.False(viewModel.Italic);

        viewModel.ToggleBoldCommand.Execute(null);
        viewModel.ToggleItalicCommand.Execute(null);

        Assert.True(viewModel.Bold);
        Assert.True(viewModel.Italic);

        viewModel.ToggleBoldCommand.Execute(null);

        Assert.False(viewModel.Bold);
        Assert.True(viewModel.Italic);
    }

    [Fact]
    public void Turning_The_Faces_Tells_The_Page_To_Read_Again() {
        // The faces live one object in, so the change has to be raised again
        // under the names the view is bound to - the page would keep its old
        // weight and slope otherwise.
        var viewModel = CreateViewModel();
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        viewModel.Bold = true;
        viewModel.Italic = true;

        Assert.Contains(nameof(EditorViewModel.Bold), changed);
        Assert.Contains(nameof(EditorViewModel.Italic), changed);
    }

    [Fact]
    public void The_Underline_Toggle_Draws_It_And_Takes_It_Back() {
        var viewModel = CreateViewModel();

        // A page carries no underline until the writer says otherwise.
        Assert.False(viewModel.Underline);

        viewModel.ToggleUnderlineCommand.Execute(null);

        Assert.True(viewModel.Underline);
        Assert.True(_settings.Saves.Single().Underline);

        viewModel.ToggleUnderlineCommand.Execute(null);

        Assert.False(viewModel.Underline);
        Assert.False(_settings.Saves.Last().Underline);
    }

    [Fact]
    public void Drawing_The_Underline_Tells_The_Page_To_Read_Again() {
        // The underline lives one object in, so the change has to be raised
        // again under the name the strip layer is bound to - the page would
        // stay bare otherwise.
        var viewModel = CreateViewModel();
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        viewModel.Underline = true;

        Assert.Contains(nameof(EditorViewModel.Underline), changed);
    }
}
