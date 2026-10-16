using FileTree;
using Shiranuix.Plugins.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for <see cref="DirectoryScanner"/>: what a tree shows when it reads a
/// folder, and what it says when it cannot.
/// </summary>
/// <remarks>
/// The disk here is a real one, under the temporary directory: a folder of
/// files is a thing to be tested against files, and mocking a file system
/// tests the mock.
/// </remarks>
public sealed class DirectoryScannerTests {
    [Fact]
    public void A_Folder_Comes_Back_With_Everything_In_It() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");
        disk.WriteFile("alpha.txt");
        disk.WriteFolder("chapters");
        disk.WriteFile("chapters/one.txt");

        var nodes = DirectoryScanner.Scan(disk.Root, ScanOptions.Default);

        // Reading is not ordering: what the tree shows, and in what order, is
        // the tree's own business and is tested where the tree lives.
        Assert.Equal(
            ["alpha.txt", "chapters", "notes.txt"],
            nodes.Select(node => node.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Single(nodes, node => node.IsDirectory && node.Name == "chapters");
    }

    [Fact]
    public void What_Is_Inside_A_Folder_Is_Not_Read_Until_It_Is_Opened() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/one.txt");

        var root = Root(disk);
        var chapters = root.Children.Single(node => node.Name == "chapters");

        Assert.False(chapters.IsLoaded);
        Assert.Empty(chapters.Children);
    }

    [Fact]
    public void Opening_A_Folder_Reads_What_Is_Inside_It() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/one.txt");

        var root = Root(disk);
        var chapters = DirectoryScanner.Open(
            root.Children.Single(node => node.Name == "chapters"), ScanOptions.Default);

        Assert.True(chapters.IsLoaded);
        Assert.Equal("one.txt", Assert.Single(chapters.Children).Name);

        // Root, chapters, one.txt: a node knows how deep it is because the
        // folder it sits in is part of it, and opening it does not move it.
        Assert.Equal(1, chapters.Depth);
        Assert.Equal(2, Assert.Single(chapters.Children).Depth);
    }

    [Fact]
    public void Opening_Something_That_Is_Not_A_Folder_Changes_Nothing() {
        using var disk = new TempTree();
        disk.WriteFile("notes.txt");

        var root = Root(disk);
        var file = root.Children.Single(node => node.Name == "notes.txt");

        var opened = DirectoryScanner.Open(file, ScanOptions.Default);

        Assert.False(opened.IsLoaded);
        Assert.Equal(file.FullPath, opened.FullPath);
    }

    [Fact]
    public void Names_That_Start_With_A_Dot_Are_Left_Out_Unless_Asked_For() {
        using var disk = new TempTree();
        disk.WriteFile(".hidden");
        disk.WriteFile("shown.txt");

        var plain = DirectoryScanner.Scan(disk.Root, ScanOptions.Default);
        var everything = DirectoryScanner.Scan(
            disk.Root, ScanOptions.Default with { IncludeHidden = true });

        Assert.Equal(["shown.txt"], plain.Select(node => node.Name));
        Assert.Equal([".hidden", "shown.txt"], everything.Select(node => node.Name));
    }

    [Fact]
    public void A_Folder_Of_Too_Many_Things_Says_As_Much_And_Shows_The_First() {
        using var disk = new TempTree();
        for (var index = 0; index < 5; index++) {
            disk.WriteFile($"note-{index}.txt");
        }

        var problems = new List<string>();
        var nodes = DirectoryScanner.Scan(
            disk.Root, ScanOptions.Default with { MaxEntries = 3 }, problems.Add);

        Assert.Equal(3, nodes.Count);
        Assert.Contains(problems, problem => problem.Contains("more than 3 things"));
    }

    [Fact]
    public void A_Folder_That_Is_Not_There_Says_So_And_Shows_Nothing() {
        using var disk = new TempTree();

        var problems = new List<string>();
        var nodes = DirectoryScanner.Scan(
            disk.PathTo("nowhere"), ScanOptions.Default, problems.Add);

        Assert.Empty(nodes);
        Assert.Contains(problems, problem => problem.Contains("no folder at"));
    }

    [Fact]
    public void A_Path_Is_Described_Relative_To_The_Root() {
        using var disk = new TempTree();
        var path = disk.WriteFile("chapters/one.txt");
        var node = FileNode.Of("one.txt", path, isDirectory: false, null);

        Assert.Equal("chapters/one.txt", node.RelativeTo(disk.Root));
        Assert.Equal(0, node.Depth);
    }

    /// <summary>
    /// The tree's root: a node for the folder itself, with what is in it read.
    /// </summary>
    /// <param name="disk">The folder to stand on.</param>
    /// <returns>The root, loaded.</returns>
    private static FileNode Root(TempTree disk) => DirectoryScanner.Open(
        FileNode.Of("root", disk.Root, isDirectory: true, null), ScanOptions.Default);
}
