using Avalonia.Controls;
using Shiranuix.PluginHost;
using Shiranuix.Plugins;
using Shiranuix.Plugins.UI;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for <see cref="PanelRegistry"/>: which panels the window shows, in
/// what order, and what happens to the one that loses.
/// </summary>
public sealed class PanelRegistryTests {
    /// <summary>A panel that is only an id and an empty control.</summary>
    private sealed class Panel(string id) : IPluginPanel {
        public string Id => id;

        public string Title => id;

        public string? Glyph => null;

        public Avalonia.Media.Geometry? Icon => null;

        public Control View { get; } = new Border();
    }

    [Fact]
    public void A_Panel_Goes_In_And_The_Column_Opens_For_It() {
        var panels = new PanelRegistry();

        Assert.True(panels.Show(new Panel("file-tree.explorer")).IsSuccess);
        Assert.True(panels.IsOpen);
        Assert.True(panels.IsVisible);
        Assert.Equal("file-tree.explorer", panels.Selected!.Id);
    }

    [Fact]
    public void A_Window_With_No_Panels_Shows_No_Column() {
        var panels = new PanelRegistry();

        // Not merely closed: nothing to open, so the bar is not even there.
        panels.Toggle();
        Assert.False(panels.IsVisible);
    }

    [Fact]
    public void The_First_Panel_In_Is_The_One_Shown() {
        var panels = new PanelRegistry();

        panels.Show(new Panel("first"));
        panels.Show(new Panel("second"));

        // Opening a window with two plugins to choose between should show one
        // of them, not an empty column.
        Assert.Equal("first", panels.Selected!.Id);
        Assert.Equal(2, panels.Count);
    }

    [Fact]
    public void Choosing_Another_Panel_Shows_It() {
        var panels = new PanelRegistry();
        var second = new Panel("second");

        panels.Show(new Panel("first"));
        panels.Show(second);
        panels.Selected = second;

        Assert.True(panels.IsOpen);
        Assert.Equal("second", panels.Selected!.Id);
    }

    [Fact]
    public void A_Second_Panel_Under_The_Same_Id_Is_Refused() {
        var panels = new PanelRegistry();
        panels.Show(new Panel("first"));

        var outcome = panels.Show(new Panel("first"));

        // First id wins, exactly as on the register: a plugin cannot take
        // another's place by arriving second.
        Assert.False(outcome.IsSuccess);
        Assert.Equal(1, panels.Count);
    }

    [Fact]
    public void A_Panel_That_Does_Not_Say_Who_It_Is_Is_Refused() {
        var panels = new PanelRegistry();

        Assert.False(panels.Show(new Panel("   ")).IsSuccess);
        Assert.Equal(0, panels.Count);
    }

    [Fact]
    public void Taking_A_Panel_Out_Leaves_Another_One_Showing() {
        var panels = new PanelRegistry();
        panels.Show(new Panel("first"));
        panels.Show(new Panel("second"));

        Assert.True(panels.Hide("first").IsSuccess);
        Assert.Equal("second", panels.Selected!.Id);
    }

    [Fact]
    public void Taking_Out_A_Panel_That_Is_Not_There_Says_So() {
        var panels = new PanelRegistry();

        Assert.False(panels.Hide("nothing").IsSuccess);
    }

    [Fact]
    public void Taking_Out_The_Last_Panel_Leaves_No_Column() {
        var panels = new PanelRegistry();
        panels.Show(new Panel("only"));

        panels.Hide("only");

        Assert.False(panels.IsVisible);
        Assert.Null(panels.Selected);
    }

    [Fact]
    public void Opening_And_Closing_The_Column_Does_Not_Forget_Whose_Panels_They_Are() {
        var panels = new PanelRegistry();
        panels.Show(new Panel("first"));

        panels.Toggle();
        Assert.False(panels.IsOpen);

        panels.Toggle();
        Assert.True(panels.IsOpen);
        Assert.Equal("first", panels.Selected!.Id);
    }
}
