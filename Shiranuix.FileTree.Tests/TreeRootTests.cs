using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.FileTree.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for the tree's shape: the folder it stands on is a row like any
/// other, rows keep their identity over a re-read, and closing every folder
/// leaves the root standing.
/// </summary>
public sealed class TreeRootTests {
    [Fact]
    public async Task The_Folder_The_Tree_Stands_On_Is_A_Row_Like_Any_Other() {
        using var disk = await Three();

        var tree = await Standing(disk);

        // One row, named after the folder, open from the start: a root with
        // nothing under it says nothing about where the writer is.
        var row = Assert.Single(tree.Items);
        Assert.Equal(Path.GetFileName(disk.Root.TrimEnd(Path.DirectorySeparatorChar)), row.Name);
        Assert.True(row.IsExpanded);
        Assert.Equal(["chapters", "draft.txt"], tree.RootChildren.Select(child => child.Name));
    }

    [Fact]
    public async Task Opening_A_Row_Reads_What_Is_Inside_It() {
        using var disk = await Three();

        var tree = await Standing(disk);
        var chapters = tree.RootChildren.Single(row => row.Name == "chapters");
        Assert.Empty(chapters.Children);

        // Setting the row open is the whole request; the tree answers it.
        chapters.IsExpanded = true;

        Assert.Equal(["one.txt"], chapters.Children.Select(child => child.Name));
    }

    [Fact]
    public async Task Closing_Every_Folder_Leaves_The_Root_Standing() {
        using var disk = await Three();

        var tree = await Standing(disk);
        tree.RootChildren.Single(row => row.Name == "chapters").IsExpanded = true;

        tree.CollapseAll();

        Assert.False(tree.RootChildren.Single(row => row.Name == "chapters").IsExpanded);
        Assert.True(Assert.Single(tree.Items).IsExpanded);
    }

    [Fact]
    public async Task A_Row_Is_Kept_Over_A_Re_Read_So_What_Was_Open_Stays_Open() {
        using var disk = await Three();

        var tree = await Standing(disk);
        var chapters = tree.RootChildren.Single(row => row.Name == "chapters");
        chapters.IsExpanded = true;

        // Reading the disk again must not hand the writer a different set of
        // rows: a row that is replaced takes its open state with it.
        await tree.RefreshAsync();

        var after = tree.RootChildren.Single(row => row.Name == "chapters");
        Assert.Same(chapters, after);
        Assert.True(after.IsExpanded);
    }

    [Fact]
    public async Task Every_Row_Says_How_Many_Guide_Lines_To_Draw() {
        using var disk = await Three();

        var tree = await Standing(disk);
        tree.RootChildren.Single(row => row.Name == "chapters").IsExpanded = true;

        var one = tree.Find(Path.Combine(disk.Root, "chapters", "one.txt"));
        Assert.NotNull(one);

        // The root draws none, a row one folder down draws one.
        Assert.Empty(Assert.Single(tree.Items).Guides);
        Assert.Single(tree.RootChildren.Single(row => row.Name == "chapters").Guides);
        Assert.Equal(2, one.Guides.Count);
    }

    private static async Task<FileTreeState> Standing(TempTree disk) {
        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);

        return tree;
    }

    /// <summary>
    /// A folder with a folder in it and a file beside it, the shape every tree
    /// test wants.
    /// </summary>
    private static async Task<TempTree> Three() {
        var disk = new TempTree();
        disk.WriteFolder("chapters");
        disk.WriteFile("chapters/one.txt");
        disk.WriteFile("draft.txt");

        await Task.CompletedTask;
        return disk;
    }
}
