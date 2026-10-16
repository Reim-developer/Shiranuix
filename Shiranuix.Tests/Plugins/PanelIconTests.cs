using Lucide.Avalonia;
using Shiranuix.Plugins.UI;

namespace Shiranuix.Tests.Plugins;

/// <summary>
/// Tests for the names panels give their icons, and for the two panels that
/// ship with the editor.
/// </summary>
/// <remarks>
/// An icon that quietly draws nothing is worse than an icon that draws the
/// wrong thing, because nothing says it failed. So a name nobody has heard of
/// comes back as a question mark, and the two sample panels' own names are
/// checked here rather than left to be noticed in a window.
/// </remarks>
public sealed class PanelIconTests {
    [Fact]
    public void A_Name_In_Any_Spelling_Finds_The_Same_Icon() {
        var expected = LucideIconKind.FolderTree;

        Assert.Equal(expected, PanelIcons.Resolve("FolderTree"));
        Assert.Equal(expected, PanelIcons.Resolve("folder-tree"));
        Assert.Equal(expected, PanelIcons.Resolve("folder tree"));
        Assert.Equal(expected, PanelIcons.Resolve("  Folder_Tree  "));
    }

    [Fact]
    public void No_Name_At_All_Leaves_The_Tab_To_The_Glyph() {
        Assert.Null(PanelIcons.Resolve(null));
        Assert.Null(PanelIcons.Resolve(string.Empty));
        Assert.Null(PanelIcons.Resolve("   "));
    }

    [Fact]
    public void A_Name_That_Names_Nothing_Asks_The_Question() {
        // A typo should be a question mark on the tab, not an empty tab: one
        // can be noticed and the other just sits there.
        Assert.Equal(LucideIconKind.CircleQuestionMark, PanelIcons.Resolve("no-such-icon"));
    }

    [Fact]
    public void Every_Icon_In_The_Set_Can_Be_Named_The_Way_A_Panel_Names_It() {
        // The lookup is built from the set, so every name in the set has to
        // find itself. If this ever fails, the normalising has drifted from
        // the names the set actually uses.
        foreach (var kind in Enum.GetValues<LucideIconKind>()) {
            Assert.Equal(kind, PanelIcons.Resolve(kind.ToString()));
        }
    }

    [Fact]
    public void The_Sample_Panels_Name_An_Icon_That_Exists() {
        Assert.Equal(LucideIconKind.FolderTree, PanelIcons.Resolve("folder-tree"));
        Assert.Equal(LucideIconKind.Clock, PanelIcons.Resolve("clock"));
    }
}
