using FileTree.Io;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for the tree's orders: what it puts first, and what the writer marked
/// on the way to deleting several things at once.
/// </summary>
public sealed class TreeOrderTests {
    [Fact]
    public async Task Folders_Come_Before_Files_Whatever_The_Order() {
        using var disk = await Ordered();

        var tree = await Standing(disk);
        tree.SortBy(TreeSort.Date);

        // Even sorted by when things last changed, a writer looking for a file
        // should not have to look past a wall of folders.
        Assert.True(tree.Items[0].IsDirectory);
    }

    [Fact]
    public async Task Sorting_By_Name_Reads_As_It_Is_Spelled() {
        using var disk = await Ordered();

        var tree = await Standing(disk);
        tree.SortBy(TreeSort.Name);

        // Case is not order: "alpha" before "Beta", the way a reader reads it.
        Assert.Equal(
            ["chapters", "alpha.txt", "Beta.txt"],
            tree.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task The_Order_Can_Be_Turned_The_Other_Way_Round() {
        using var disk = await Ordered();

        var tree = await Standing(disk);
        tree.SortBy(TreeSort.Name);
        tree.TurnOrder();

        Assert.Equal(
            ["Beta.txt", "alpha.txt", "chapters"],
            tree.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Sorting_By_Kind_Groups_The_Things_Of_A_Kind_Together() {
        using var disk = await Ordered();
        disk.WriteFile("appendix.md");

        var tree = await Standing(disk);
        tree.SortBy(TreeSort.Kind);

        // .md before .txt, folders above both.
        Assert.Equal(
            ["chapters", "appendix.md", "alpha.txt", "Beta.txt"],
            tree.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Marking_A_Row_Says_It_Is_Marked_And_Marking_It_Again_Takes_It_Back() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var tree = await Standing(disk);
        var draft = tree.Items.Single();

        tree.ToggleMark(draft);
        Assert.True(draft.IsMarked);
        Assert.Equal([draft], tree.Marked);

        tree.ToggleMark(draft);
        Assert.False(draft.IsMarked);
        Assert.Empty(tree.Marked);
    }

    [Fact]
    public async Task Several_Rows_Can_Be_Marked_At_Once() {
        using var disk = new TempTree();
        disk.WriteFile("one.txt");
        disk.WriteFile("two.txt");

        var tree = await Standing(disk);
        tree.ToggleMark(tree.Items[0]);
        tree.ToggleMark(tree.Items[1]);

        Assert.Equal(2, tree.Marked.Count);
    }

    [Fact]
    public async Task Forgetting_The_Marks_Takes_Them_Off_The_Rows_Too() {
        using var disk = new TempTree();
        disk.WriteFile("one.txt");

        var tree = await Standing(disk);
        var row = tree.Items.Single();
        tree.ToggleMark(row);

        tree.ClearMarks();

        Assert.Empty(tree.Marked);
        Assert.False(row.IsMarked);
    }

    private static async Task<FileTreeState> Standing(TempTree disk) {
        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);

        return tree;
    }

    /// <summary>
    /// A folder whose contents sort differently depending on the order asked
    /// for: one folder, and two files whose names differ in case and kind.
    /// </summary>
    private static async Task<TempTree> Ordered() {
        var disk = new TempTree();
        disk.WriteFolder("chapters");
        disk.WriteFile("alpha.txt");
        disk.WriteFile("Beta.txt");

        // Written a moment apart, so "by date" has a real order to find.
        await Task.Delay(20);
        File.SetLastWriteTimeUtc(disk.PathTo("Beta.txt"), DateTime.UtcNow);

        return disk;
    }
}
