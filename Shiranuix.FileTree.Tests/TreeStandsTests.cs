using Shiranuix.FileTree;
using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.FileTree.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for the one thing the tree refuses to do: exist without somewhere to
/// stand.
/// </summary>
public sealed class TreeStandsTests {
    [Fact]
    public async Task A_Tree_With_No_Folder_Says_It_Has_None() {
        var tree = new FileTreeState();

        Assert.False(tree.HasRoot);
        Assert.Empty(tree.RootChildren);
        Assert.Empty(tree.Items);
        Assert.Equal(string.Empty, tree.RootName);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Standing_On_A_Folder_Is_What_Makes_There_Be_A_Tree() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);

        Assert.True(tree.HasRoot);
        Assert.Single(tree.Items);
        Assert.Equal(["draft.txt"], tree.RootChildren.Select(row => row.Name));
    }

    [Fact]
    public async Task Standing_On_A_Folder_That_Is_Not_There_Changes_Nothing() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);

        var refused = await tree.OpenRootAsync(Path.Combine(disk.Root, "nowhere"));

        Assert.False(refused.IsSuccess);
        Assert.True(tree.HasRoot);
        Assert.Equal(disk.Root, tree.Root?.FullPath);
    }

    [Fact]
    public async Task A_Folder_That_Goes_Away_Does_Not_Take_The_Column_With_It() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var tree = new FileTreeState();
        await tree.OpenRootAsync(disk.Root);
        var stood = tree.Root;

        Directory.Delete(disk.Root, recursive: true);
        var reread = await tree.RefreshAsync();

        // The folder is gone, so there is nothing to re-read and the column
        // does not empty itself: a writer who has a side bar open should not
        // have it taken away by something that happened elsewhere. The rows
        // stay, and the tree says it could not read.
        Assert.False(reread.IsSuccess);
        Assert.True(tree.HasRoot);
        Assert.Equal(stood!.FullPath, tree.Root?.FullPath);
        Assert.Equal(["draft.txt"], tree.RootChildren.Select(row => row.Name));
    }
}
