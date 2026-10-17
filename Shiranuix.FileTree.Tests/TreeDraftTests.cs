using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.FileTree.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for the tree's drafts: the rows that hover in the tree asking for a
/// name, which are nothing on the disk until the name is given, and nothing
/// at all if the name is taken back.
/// </summary>
public sealed class TreeDraftTests {
    [Fact]
    public async Task A_New_File_Is_A_Name_Waiting_Under_The_Root() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");

        var tree = await Standing(disk);
        var draft = tree.BeginNewFile();

        Assert.True(draft.IsDraft);
        Assert.False(draft.IsDirectory);
        Assert.Equal("untitled.txt", draft.Name);
        Assert.True(draft.IsEditing);
        Assert.Same(draft, tree.Draft);
        Assert.Same(tree.RootRow, draft.Parent);
        Assert.Same(draft, tree.RootRow!.Children[0]);
        Assert.Same(draft, tree.Selected);
        Assert.Equal("Naming a new file.", tree.Status);
    }

    [Fact]
    public async Task A_New_Folder_Waits_Under_The_Selected_Folder() {
        using var disk = new TempTree();
        disk.WriteFolder("chapters");

        var tree = await Standing(disk);
        var chapters = tree.RootChildren.Single(item => item.Name == "chapters");
        tree.Selected = chapters;

        var draft = tree.BeginNewFolder();

        Assert.True(draft.IsDirectory);
        Assert.Equal("new folder", draft.Name);
        Assert.True(chapters.IsExpanded);
        Assert.Same(draft, chapters.Children[0]);
    }

    [Fact]
    public async Task A_New_File_Waits_Beside_A_Selected_File() {
        using var disk = new TempTree();
        disk.WriteFile("story.txt");

        var tree = await Standing(disk);
        var story = tree.All().Single(item => item.Name == "story.txt");
        tree.Selected = story;

        var draft = tree.BeginNewFile();

        Assert.False(draft.IsDraft is false);
        Assert.Same(tree.RootRow, draft.Parent);
    }

    [Fact]
    public async Task Only_One_Name_Waits_At_A_Time() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");

        var tree = await Standing(disk);
        var first = tree.BeginNewFile();
        var second = tree.BeginNewFolder();

        Assert.Same(second, tree.Draft);
        Assert.NotSame(first, tree.Draft);
        Assert.DoesNotContain(first, tree.RootRow!.Children);
        Assert.Same(second, tree.RootRow.Children[0]);
    }

    [Fact]
    public async Task Turning_Away_Leaves_Nothing_Behind() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");

        var tree = await Standing(disk);
        var draft = tree.BeginNewFile();

        tree.DiscardDraft();

        Assert.Null(tree.Draft);
        Assert.DoesNotContain(draft, tree.RootRow!.Children);
        Assert.Same(tree.RootRow, tree.Selected);

        // A second letting-go is a quiet no, not an error.
        tree.DiscardDraft();
        Assert.Null(tree.Draft);
    }

    [Fact]
    public async Task A_Root_That_Was_Never_The_Draft_Is_Left_Alone() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");

        var tree = await Standing(disk);
        var draft = tree.BeginNewFile();
        var notes = tree.All().Single(item => item.Name == "notes.txt");

        tree.DiscardDraft(notes);

        Assert.Same(draft, tree.Draft);
        Assert.Contains(draft, tree.RootRow!.Children);
        Assert.True(draft.IsEditing);
    }

    [Fact]
    public async Task Choosing_A_Path_Selects_The_Row_That_Owns_It() {
        using var disk = new TempTree();
        disk.WriteFile("story.txt");
        disk.WriteFolder("chapters");

        var tree = await Standing(disk);

        tree.Select(disk.PathTo("story.txt"));

        Assert.Equal("story.txt", tree.Selected?.Name);
    }

    private static async Task<FileTreeState> Standing(TempTree disk) {
        var tree = new FileTreeState();
        var outcome = await tree.OpenRootAsync(disk.Root);

        Assert.True(outcome.IsSuccess);

        return tree;
    }
}
