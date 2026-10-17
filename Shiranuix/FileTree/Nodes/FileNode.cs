namespace Shiranuix.FileTree.Nodes;

/// <summary>
/// One thing in a folder, and - once it has been opened - the things inside it.
/// </summary>
/// <remarks>
/// <para>
/// A value, and an unchanging one: a folder's contents are a moment, and the
/// moment does not change when someone types. Anything that can change about a
/// node - whether it is open, what the writer is renaming - belongs to the
/// panel beside it, not to the node.
/// </para>
/// <para>
/// Children are loaded when the node is first opened rather than when the tree
/// is first shown. A folder of ten thousand files is not a thing to read
/// because somebody opened the editor; it is a thing to read one level at a
/// time, which is what this shape makes possible.
/// </para>
/// </remarks>
public sealed class FileNode {
    private static readonly IReadOnlyList<FileNode> NoChildren = [];

    private FileNode(
        string name,
        string fullPath,
        bool isDirectory,
        DateTime modifiedUtc,
        FileNode? parent) {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        ModifiedUtc = modifiedUtc;
        Parent = parent;
    }

    /// <summary>The name a person reads, without its folder.</summary>
    public string Name { get; }

    /// <summary>Where it is, in full.</summary>
    public string FullPath { get; }

    /// <summary>Whether it can have things inside it.</summary>
    public bool IsDirectory { get; }

    /// <summary>When the disk last saw this change, as the tree read it.</summary>
    public DateTime ModifiedUtc { get; }

    /// <summary>
    /// The folder this one sits in, or null for the tree's root.
    /// </summary>
    public FileNode? Parent { get; }

    /// <summary>
    /// What is inside, once it has been read; empty for a file, and for a
    /// folder nobody has opened yet.
    /// </summary>
    public IReadOnlyList<FileNode> Children { get; private init; } = NoChildren;

    /// <summary>Whether this node's contents have been read.</summary>
    public bool IsLoaded { get; private init; }

    /// <summary>How deep it sits, the root being zero.</summary>
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>
    /// Makes a node of a file or folder that has not been opened.
    /// </summary>
    /// <param name="name">Its name.</param>
    /// <param name="path">Where it is, in full.</param>
    /// <param name="isDirectory">Whether it can have things inside it.</param>
    /// <param name="parent">The folder it sits in, or null for the root.</param>
    /// <returns>The node.</returns>
    public static FileNode Of(
        string name,
        string path,
        bool isDirectory,
        FileNode? parent,
        DateTime modifiedUtc = default) =>
        new(name, path, isDirectory, modifiedUtc, parent);

    /// <summary>
    /// The same node with its contents read.
    /// </summary>
    /// <param name="children">What is inside it.</param>
    /// <returns>The node, loaded.</returns>
    public FileNode Loaded(IReadOnlyList<FileNode> children) =>
        new(Name, FullPath, IsDirectory, ModifiedUtc, Parent) {
            Children = children,
            IsLoaded = true
        };

    /// <summary>
    /// The node's path relative to the tree's root, which is what a writer
    /// reads and what a row is sorted by.
    /// </summary>
    /// <param name="root">The tree's root path.</param>
    /// <returns>The path from the root, with no leading separator.</returns>
    public string RelativeTo(string root) {
        var separator = System.IO.Path.DirectorySeparatorChar;
        var prefix = root.EndsWith(separator) ? root : root + separator;

        return FullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? FullPath[prefix.Length..]
            : FullPath;
    }
}
