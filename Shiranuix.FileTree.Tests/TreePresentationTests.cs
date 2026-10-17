using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.FileTree.Testing;
using Shiranuix.FileTree.Views;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for the things the tree does that the disk cannot be asked
/// about: what a name looks like when there is no room for all of it, what
/// a row shows while it is being named, and what a drag is allowed to be
/// put down on.
/// </summary>
public sealed class TreePresentationTests {
    [Fact]
    public void A_Name_That_Fits_Is_Left_Exactly_As_It_Is() {
        var name = "draft.txt";

        Assert.Equal(name, MiddleTrim.Shorten(name, name.Length));
        Assert.Equal(name, MiddleTrim.Shorten(name, name.Length + 20));
    }

    [Fact]
    public void A_Long_Name_Keeps_Its_Beginning_And_Its_End() {
        var name = "notes for the chapter about installation.txt";
        var shown = MiddleTrim.Shorten(name, 20);

        // Cutting at the end loses the only part that says what kind of file
        // this is, so both ends survive: the beginning and the extension.
        Assert.StartsWith(name[..9], shown);
        Assert.EndsWith(name[^10..], shown);
        Assert.Contains('…', shown);
        Assert.True(shown.Length < name.Length);
    }

    [Fact]
    public void A_Name_Too_Short_To_Cut_Is_Left_Alone() {
        var name = "abc";

        // Two dots and nothing worth keeping is worse than the whole name.
        Assert.Equal(name, MiddleTrim.Shorten(name, 2));
    }

    [Fact]
    public async Task A_File_May_Be_Dropped_On_A_Folder_But_Not_On_A_File() {
        using var disk = await Rows();
        var tree = await Standing(disk);

        var file = tree.RootChildren.Single(row => row.Name == "draft.txt");
        var folder = tree.RootChildren.Single(row => row.Name == "chapters");

        Assert.True(TreeDrag.CanDropOn(file, folder));
        Assert.False(TreeDrag.CanDropOn(file, file));
    }

    [Fact]
    public async Task A_Folder_Is_Never_Dropped_Inside_Itself() {
        using var disk = await Rows();
        var tree = await Standing(disk);

        var chapters = tree.RootChildren.Single(row => row.Name == "chapters");
        chapters.IsExpanded = true;
        var inside = chapters.Children.Single(row => row.Name == "one.txt");

        // The disk would allow both of these; the result would be a tree that
        // contains itself.
        Assert.False(TreeDrag.CanDropOn(chapters, chapters));
        Assert.False(TreeDrag.CanDropOn(chapters, inside));
    }

    [Fact]
    public async Task A_Row_Is_Never_Dropped_Into_The_Folder_It_Is_Already_In() {
        using var disk = await Rows();
        var tree = await Standing(disk);

        var draft = tree.RootChildren.Single(row => row.Name == "draft.txt");

        // Nowhere to go, so the caret never appears over its own parent.
        Assert.False(TreeDrag.CanDropOn(draft, tree.Items[0]));
    }

    [Fact]
    public void A_Press_Becomes_A_Drag_Only_Once_The_Pointer_Has_Travelled() {
        var start = new Avalonia.Point(10, 10);

        Assert.False(TreeDrag.HasTravelled(start, new Avalonia.Point(11, 11)));
        Assert.False(TreeDrag.HasTravelled(start, new Avalonia.Point(10, 13)));

        // A drag down a list of folders is almost entirely vertical, and one
        // that had to move sideways as well would never begin.
        Assert.True(TreeDrag.HasTravelled(start, new Avalonia.Point(10, 40)));
        Assert.True(TreeDrag.HasTravelled(start, new Avalonia.Point(40, 10)));
    }

    [Fact]
    public async Task A_Folder_Being_Named_Hides_Its_Name_And_Takes_It_Back() {
        using var disk = await Rows();
        var tree = await Standing(disk);

        var folder = tree.RootChildren.Single(row => row.IsDirectory);

        // A row shows one face at a time: the editor lands where the name
        // was, so the name has stepped aside while the editor is there.
        Assert.True(folder.ShowsFolderName);
        Assert.False(folder.ShowsFileName);
        folder.IsEditing = true;
        Assert.False(folder.ShowsFolderName);
        Assert.False(folder.ShowsFileName);
        folder.IsEditing = false;
        Assert.True(folder.ShowsFolderName);
    }

    [Fact]
    public async Task A_File_Being_Named_Hides_Its_Name_In_The_Same_Way() {
        using var disk = await Rows();
        var tree = await Standing(disk);

        var file = tree.RootChildren.Single(row => row.Name == "draft.txt");

        Assert.True(file.ShowsFileName);
        Assert.False(file.ShowsFolderName);
        file.IsEditing = true;
        Assert.False(file.ShowsFileName);
        Assert.False(file.ShowsFolderName);
        file.IsEditing = false;
        Assert.True(file.ShowsFileName);
    }

    private static async Task<FileTreeState> Standing(TempTree disk) {
        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);

        return tree;
    }

    private static async Task<TempTree> Rows() {
        var disk = new TempTree();
        disk.WriteFolder("chapters");
        disk.WriteFile("chapters/one.txt");
        disk.WriteFile("draft.txt");

        await Task.CompletedTask;
        return disk;
    }
}
