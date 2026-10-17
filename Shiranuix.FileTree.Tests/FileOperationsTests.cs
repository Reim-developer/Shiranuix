using Shiranuix.FileTree.Io;
using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;
using Shiranuix.FileTree.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for <see cref="FileOperations"/>: making, renaming, moving and
/// deleting, and every name that is refused before the disk is touched.
/// </summary>
public sealed class FileOperationsTests {
    [Fact]
    public void Making_A_File_Puts_An_Empty_One_There() {
        using var disk = new TempTree();

        var outcome = FileOperations.CreateFile(disk.Root, "chapter-one.txt");

        Assert.True(outcome.IsSuccess);
        Assert.True(File.Exists(outcome.Value));
        Assert.Equal(string.Empty, File.ReadAllText(outcome.Value));
    }

    [Fact]
    public void Making_A_Folder_Puts_A_Folder_There() {
        using var disk = new TempTree();

        var outcome = FileOperations.CreateFolder(disk.Root, "chapters");

        Assert.True(outcome.IsSuccess);
        Assert.True(Directory.Exists(outcome.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("one/two")]
    [InlineData("one\\two")]
    public void A_Name_That_Cannot_Be_Used_Is_Refused_Before_Anything_Is_Made(string name) {
        using var disk = new TempTree();

        Assert.False(FileOperations.CreateFile(disk.Root, name).IsSuccess);
        Assert.False(FileOperations.CreateFolder(disk.Root, name).IsSuccess);
        Assert.Empty(disk.Names());
    }

    [Fact]
    public void A_Name_Already_Taken_Is_Refused_And_The_Existing_One_Is_Left_Alone() {
        using var disk = new TempTree();
        disk.WriteFile("chapter-one.txt", "the first one");

        var outcome = FileOperations.CreateFile(disk.Root, "chapter-one.txt");

        Assert.False(outcome.IsSuccess);
        Assert.Equal("the first one", File.ReadAllText(disk.PathTo("chapter-one.txt")));
    }

    [Fact]
    public void Renaming_Takes_The_Old_Name_With_It() {
        using var disk = new TempTree();
        var path = disk.WriteFile("draft.txt", "words");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        var outcome = FileOperations.Rename(node, "chapter-one.txt");

        Assert.True(outcome.IsSuccess);
        Assert.Equal("words", File.ReadAllText(outcome.Value));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Renaming_Something_Into_A_Name_That_Is_Taken_Is_Refused() {
        using var disk = new TempTree();
        disk.WriteFile("kept.txt");
        var path = disk.WriteFile("draft.txt");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        Assert.False(FileOperations.Rename(node, "kept.txt").IsSuccess);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Renaming_Something_To_Its_Own_Name_Is_Refused() {
        using var disk = new TempTree();
        var path = disk.WriteFile("draft.txt");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        Assert.False(FileOperations.Rename(node, "draft.txt").IsSuccess);
    }

    [Fact]
    public void A_Roots_Name_Is_Not_Something_That_Can_Be_Changed() {
        using var disk = new TempTree();
        var root = FileNode.Of("root", disk.Root, isDirectory: true, null);

        Assert.False(FileOperations.Rename(root, "elsewhere").IsSuccess);
        Assert.False(FileOperations.Delete(root).IsSuccess);
    }

    [Fact]
    public void Moving_Takes_The_Thing_To_The_Folder_It_Was_Told_To() {
        using var disk = new TempTree();
        var path = disk.WriteFile("draft.txt", "words");
        disk.WriteFolder("chapters");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        var outcome = FileOperations.Move(node, disk.PathTo("chapters"));

        Assert.True(outcome.IsSuccess);
        Assert.Equal("words", File.ReadAllText(disk.PathTo("chapters/draft.txt")));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void A_Folder_Is_Never_Moved_Inside_Itself() {
        using var disk = new TempTree();
        disk.WriteFolder("chapters");
        disk.WriteFolder("chapters/one");
        var chapters = FileNode.Of("chapters", disk.PathTo("chapters"), true, Parent(disk.Root));
        var one = FileNode.Of("one", disk.PathTo("chapters/one"), true, chapters);

        // The disk would allow it, and the result would be a tree containing
        // itself: a thing to refuse rather than to discover later.
        Assert.False(FileOperations.Move(one, disk.PathTo("chapters")).IsSuccess);
        Assert.True(Directory.Exists(disk.PathTo("chapters/one")));
    }

    [Fact]
    public void Moving_Onto_A_Name_That_Is_Taken_There_Is_Refused() {
        using var disk = new TempTree();
        var path = disk.WriteFile("draft.txt");
        disk.WriteFile("chapters/draft.txt");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        Assert.False(FileOperations.Move(node, disk.PathTo("chapters")).IsSuccess);
    }

    [Fact]
    public void Moving_Into_A_Folder_That_Is_Not_There_Is_Refused() {
        using var disk = new TempTree();
        var path = disk.WriteFile("draft.txt");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        Assert.False(FileOperations.Move(node, disk.PathTo("nowhere")).IsSuccess);
    }

    [Fact]
    public void Deleting_Takes_A_File_With_Its_Contents() {
        using var disk = new TempTree();
        var path = disk.WriteFile("draft.txt", "words");
        var node = FileNode.Of("draft.txt", path, isDirectory: false, Parent(disk.Root));

        Assert.True(FileOperations.Delete(node).IsSuccess);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Deleting_A_Folder_Takes_What_Was_Inside_It() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/one.txt");
        disk.WriteFile("chapters/deeper/two.txt");
        var chapters = FileNode.Of("chapters", disk.PathTo("chapters"), true, Parent(disk.Root));

        Assert.True(FileOperations.Delete(chapters).IsSuccess);
        Assert.False(Directory.Exists(disk.PathTo("chapters")));
    }

    [Theory]
    [InlineData("a", "a", true)]
    [InlineData("a", "a/b", false)]
    [InlineData("a/b", "a", true)]
    [InlineData("a", "b", false)]
    public void Whether_One_Path_Is_Inside_Another(string inner, string outer, bool expected) {
        var isInside = FileOperations.IsInside(
            Path.GetFullPath(inner), Path.GetFullPath(outer));

        Assert.Equal(expected, isInside);
    }

    private static FileNode Parent(string folder) =>
        FileNode.Of("root", folder, isDirectory: true, null);
}
