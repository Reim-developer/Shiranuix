using FileTree.Io;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for <see cref="TreeFilter"/>: what typing in the tree's box leaves
/// showing, and what it leaves hidden.
/// </summary>
public sealed class TreeFilterTests {
    [Fact]
    public void Nothing_Typed_Shows_Everything() {
        var root = MemoryTree();

        Assert.Equal(root.Children, TreeFilter.VisibleChildren(root, null));
        Assert.Equal(root.Children, TreeFilter.VisibleChildren(root, "   "));
    }

    [Fact]
    public void What_Matches_Is_Shown() {
        var root = MemoryTree();

        var visible = TreeFilter.VisibleChildren(root, "chapters");

        Assert.Equal(["chapters"], visible.Select(node => node.Name));
    }

    [Fact]
    public void A_Folder_Whose_Child_Matches_Stays_Showing_So_The_Match_Can_Be_Seen() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/nine.txt");

        // The whole tree read, as it would be after somebody opened everything.
        var root = OpenedTree(disk.Root);

        // "chapters" does not contain "nine"; the file inside it does. Hiding
        // the folder would hide the only match in the tree.
        Assert.True(TreeFilter.LeadsToAMatch(root, "nine"));
        Assert.Single(TreeFilter.VisibleChildren(root, "nine"));
    }

    [Fact]
    public void A_Closed_Folder_Is_Not_Opened_Just_To_Be_Filtered() {
        using var disk = new TempTree();
        disk.WriteFile("chapters/nine.txt");

        // Only the root read, as it is when the tree has just been shown.
        var root = DirectoryScanner.Open(
            FileNode.Of("root", disk.Root, isDirectory: true, null), ScanOptions.Default);

        // The filter reads what the tree knows. Opening a folder per keystroke
        // would turn a typing box into a scan of the disk.
        Assert.False(TreeFilter.LeadsToAMatch(root, "nine"));
        Assert.Empty(TreeFilter.VisibleChildren(root, "nine"));
    }

    [Fact]
    public void Matching_Ignores_Case() {
        var root = MemoryTree();

        Assert.Single(TreeFilter.VisibleChildren(root, "CHAPTERS"));
        Assert.Single(TreeFilter.VisibleChildren(root, "Two.TXT"));
    }

    [Fact]
    public void What_Matches_Nothing_Shows_Nothing() {
        var root = MemoryTree();

        Assert.Empty(TreeFilter.VisibleChildren(root, "nothing here"));
    }

    /// <summary>
    /// A small tree built in memory: a folder, a file and another file.
    /// </summary>
    /// <returns>The root, loaded.</returns>
    private static FileNode MemoryTree() {
        var root = FileNode.Of("root", "/stories", isDirectory: true, null);

        var chapters = FileNode.Of("chapters", "/stories/chapters", isDirectory: true, root);
        var draft = FileNode.Of("draft.txt", "/stories/draft.txt", isDirectory: false, root);
        var two = FileNode.Of("Two.txt", "/stories/two.txt", isDirectory: false, root);

        return root.Loaded([chapters, draft, two]);
    }

    /// <summary>
    /// A tree over a real folder with every folder in it read, which is what a
    /// tree looks like after somebody has walked it.
    /// </summary>
    /// <param name="rootPath">Where the tree stands.</param>
    /// <returns>The root, fully read.</returns>
    private static FileNode OpenedTree(string rootPath) {
        var root = DirectoryScanner.Open(
            FileNode.Of("root", rootPath, isDirectory: true, null), ScanOptions.Default);

        foreach (var child in root.Children.Where(node => node.IsDirectory)) {
            root = Replace(root, child, DirectoryScanner.Open(child, ScanOptions.Default));
        }

        return root;
    }

    /// <summary>
    /// Puts a loaded node in place of the one it was, the way a tree does when
    /// a folder is opened.
    /// </summary>
    /// <param name="parent">The folder to put it in.</param>
    /// <param name="was">The node it replaces.</param>
    /// <param name="now">The loaded node.</param>
    /// <returns>The same folder, with the new node in place of the old.</returns>
    private static FileNode Replace(FileNode parent, FileNode was, FileNode now) =>
        parent.Loaded([.. parent.Children.Select(child =>
            ReferenceEquals(child, was) ? now : child)]);
}
