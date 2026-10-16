using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileTree.State;

namespace FileTree.Nodes;

/// <summary>
/// One row in the tree, and everything the view needs to draw it.
/// </summary>
/// <remarks>
/// <para>
/// The node underneath is a value that never changes; this is the row, and it
/// does change: it opens, it is selected, it is being renamed. Keeping the two
/// apart is what lets a folder be re-read without the view rebuilding itself
/// around a new tree - the rows stay, and the words in them change.
/// </para>
/// <para>
/// Rows hold their children rather than asking for them, because a tree view
/// binds to a collection and a method call is not a collection. The children
/// are whatever the filter allows, rebuilt when the filter changes.
/// </para>
/// </remarks>
public sealed partial class FileTreeItem : ObservableObject {
    private readonly FileTreeState _tree;

    /// <summary>
    /// Wraps a node as a row.
    /// </summary>
    /// <param name="tree">The tree the row belongs to.</param>
    /// <param name="node">The node underneath.</param>
    /// <param name="parent">The row above it, or null for the tree's own rows.</param>
    public FileTreeItem(FileTreeState tree, FileNode node, FileTreeItem? parent) {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Parent = parent;

        Children = [];
        foreach (var child in tree.ChildrenOf(node)) {
            Children.Add(new FileTreeItem(tree, child, this));
        }
    }

    /// <summary>The node underneath, as the disk last described it.</summary>
    public FileNode Node { get; internal set; }

    /// <summary>The row above this one, or null for the tree's own rows.</summary>
    public FileTreeItem? Parent { get; }

    /// <summary>What the row says: the name, or nothing while it is being read.</summary>
    public string Name => Node.Name;

    /// <summary>Whether there is anything inside this one.</summary>
    public bool IsDirectory => Node.IsDirectory;

    /// <summary>Where it is, in full.</summary>
    public string FullPath => Node.FullPath;

    /// <summary>How deep it sits, for indenting and for knowing what it is.</summary>
    public int Depth => Node.Depth;

    /// <summary>What this row shows beneath it, once the filter has had its say.</summary>
    public ObservableCollection<FileTreeItem> Children { get; }

    /// <summary>Whether the row is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    [NotifyPropertyChangedFor(nameof(IsClosed))]
    private bool _isExpanded;

    /// <summary>Whether this row is a folder the writer can open.</summary>
    public bool IsOpen => IsDirectory && IsExpanded;

    /// <summary>Whether this row is a folder that is closed.</summary>
    public bool IsClosed => IsDirectory && !IsExpanded;

    /// <summary>Whether the writer has this row selected.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Whether the writer is renaming this row, in place.</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>Whether this row is the one the panel's actions work on.</summary>
    [ObservableProperty]
    private bool _isMarked;

    /// <summary>
    /// Puts the row back on the node the tree now holds for this path, and
    /// rebuilds what it shows beneath it, keeping the rows that are still there.
    /// </summary>
    /// <param name="node">
    /// The node as it now stands, or null to keep the one the row holds.
    /// </param>
    public void Rebuild(FileNode? node = null) {
        if (node is not null) {
            Node = node;
        }

        var wanted = _tree.ChildrenOf(Node);

        Children.Clear();
        foreach (var child in wanted) {
            var existing = Children.FirstOrDefault(row =>
                string.Equals(row.FullPath, child.FullPath, StringComparison.OrdinalIgnoreCase));

            if (existing is not null) {
                existing.Rebuild(child);
                Children.Add(existing);
            } else {
                Children.Add(new FileTreeItem(_tree, child, this));
            }
        }
    }

    /// <summary>
    /// Whether a row is one of its own ancestors - which is how a drop onto a
    /// folder inside the thing being dragged is refused.
    /// </summary>
    /// <param name="maybeAncestor">The row to judge.</param>
    /// <returns>True when it is this row or somewhere above it.</returns>
    public bool IsSelfOrAncestorOf(FileTreeItem maybeAncestor) {
        for (var row = maybeAncestor; row is not null; row = row.Parent) {
            if (ReferenceEquals(row, this)) {
                return true;
            }
        }

        return false;
    }
}
