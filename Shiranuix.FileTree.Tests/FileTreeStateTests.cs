using FileTree.Io;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for <see cref="FileTreeState"/>: where the tree stands, what it shows
/// when a folder is opened, and what it says when the disk will not answer.
/// </summary>
public sealed class FileTreeStateTests {
    [Fact]
    public async Task Standing_The_Tree_On_A_Folder_Shows_What_Is_In_It() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");
        disk.WriteFolder("chapters");

        var tree = new FileTreeState();
        var outcome = await tree.OpenRootAsync(disk.Root);

        Assert.True(outcome.IsSuccess);
        Assert.True(tree.HasRoot);
        Assert.Equal(["chapters", "notes.txt"], tree.RootChildren.Select(item => item.Name));
    }

    [Fact]
    public async Task Standing_On_Something_That_Is_Not_A_Folder_Is_Refused() {
        using var disk = new TempTree();
        var tree = new FileTreeState();

        var outcome = await tree.OpenRootAsync(disk.PathTo("nowhere"));

        Assert.False(outcome.IsSuccess);
        Assert.False(tree.HasRoot);
    }

    [Fact]
    public async Task Opening_A_Folder_Reads_What_Is_Inside_It() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/one.txt");

        var tree = await Standing(disk);
        var chapters = tree.RootChildren.Single(item => item.Name == "chapters");

        Assert.True(tree.Expand(chapters).IsSuccess);
        Assert.True(chapters.IsExpanded);
        Assert.Equal("one.txt", Assert.Single(chapters.Children).Name);

        tree.Collapse(chapters);
        Assert.False(chapters.IsExpanded);
    }

    [Fact]
    public async Task Opening_A_File_Is_Refused_Rather_Than_Done_Quietly() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");

        var tree = await Standing(disk);
        var file = tree.RootChildren.Single(item => item.Name == "notes.txt");

        Assert.False(tree.Expand(file).IsSuccess);
        Assert.False(file.IsExpanded);
    }

    [Fact]
    public async Task Filtering_Hides_What_Does_Not_Match_And_Keeps_The_Rest() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/nine.txt");
        disk.WriteFile("draft.txt");

        var tree = await Standing(disk);

        // The folder is opened first: a filter reads what the tree has read,
        // and will not open folders to look inside them.
        tree.Expand(tree.RootChildren.Single(item => item.Name == "chapters"));
        tree.Filter = "nine";

        // "chapters" does not contain "nine"; the file inside it does.
        Assert.Equal(["chapters"], tree.RootChildren.Select(item => item.Name));

        tree.Filter = "";
        Assert.Equal(["chapters", "draft.txt"], tree.RootChildren.Select(item => item.Name));
    }

    [Fact]
    public async Task Reading_Again_Keeps_What_Was_Open_And_Finds_What_Changed() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/one.txt");

        var tree = await Standing(disk);
        tree.Expand(tree.RootChildren.Single(item => item.Name == "chapters"));

        disk.WriteFile("chapters/two.txt");
        await tree.RefreshAsync();

        var chapters = tree.RootChildren.Single(item => item.Name == "chapters");
        Assert.True(chapters.IsExpanded);
        Assert.Equal(2, chapters.Children.Count);
    }

    [Fact]
    public async Task Showing_Hidden_Names_Is_Their_Choice_To_Make() {
        using var disk = new TempTree();
        disk.WriteFile(".notes");
        disk.WriteFile("shown.txt");

        var tree = await Standing(disk);
        Assert.Equal(["shown.txt"], tree.RootChildren.Select(item => item.Name));

        tree.ShowHidden = true;
        Assert.Equal([".notes", "shown.txt"], tree.RootChildren.Select(item => item.Name));
    }

    [Fact]
    public async Task A_Row_Can_Be_Found_By_Where_It_Is() {
        using var disk = new TempTree();
        var path = disk.WriteFile("notes.txt");

        var tree = await Standing(disk);

        Assert.NotNull(tree.Find(path));
        Assert.Null(tree.Find(disk.PathTo("nowhere.txt")));
    }

    [Fact]
    public async Task Reading_A_Tree_That_Has_No_Folder_Is_Refused_Rather_Than_Guessed() {
        var tree = new FileTreeState();

        var outcome = await tree.RefreshAsync();

        Assert.False(outcome.IsSuccess);
    }

    private static async Task<FileTreeState> Standing(TempTree disk) {
        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);

        return tree;
    }
}
