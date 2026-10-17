using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.FileTree.State;

namespace Shiranuix.FileTree.Nodes;

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
/// are asked of the tree in the order it wants them, rebuilt when it does.
/// </para>
/// </remarks>
public sealed partial class FileTreeItem : ObservableObject {
    private readonly FileTreeState _tree;
    private IReadOnlyList<int>? _guides;

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

    /// <summary>
    /// One entry per folder above this row, for the guide lines that show how
    /// deep it sits.
    /// </summary>
    /// <remarks>
    /// The view draws a line per entry; the row only says how many there are.
    /// Built once, because the tree binds to it: a row at the fourth level
    /// answers the same thing every time it is asked.
    /// </remarks>
    public IReadOnlyList<int> Guides => _guides ??= [.. Enumerable.Range(0, Depth)];

    /// <summary>Whether the row shows a guide line at all.</summary>
    public bool HasGuides => Depth > 0;

    /// <summary>What this row shows beneath it, in the tree's own order.</summary>
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

        // What was here is taken down before anything is built, because a row
        // that is still in the collection is the row that survives: asking the
        // collection after clearing it always answers "nothing", which is how a
        // tree ends up replacing every row on every read and losing whatever
        // the writer had open.
        var existing = Children.ToDictionary(row => row.FullPath, StringComparer.OrdinalIgnoreCase);
        Children.Clear();

        foreach (var child in wanted) {
            if (existing.Remove(child.FullPath, out var row)) {
                row.Rebuild(child);
                Children.Add(row);
            } else {
                Children.Add(new FileTreeItem(_tree, child, this));
            }
        }

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsDirectory));
        OnPropertyChanged(nameof(Depth));
        OnPropertyChanged(nameof(Guides));
        OnPropertyChanged(nameof(HasGuides));
    }

    /// <summary>
    /// A row that opened: the tree is told, so it can read what is inside.
    /// </summary>
    /// <param name="value">Whether the row is open now.</param>
    /// <remarks>
    /// Opening is not a decision this row makes alone. Whether a row opens is
    /// the writer's business, but what being open *means* - reading the
    /// folder, adding it to what stays open over a refresh - is the tree's,
    /// and Avalonia's twisty sets this same property when it is clicked.
    /// Going through here means the twisty, a click, and a key all open a row
    /// the same way, which is the whole reason a twisty that does something
    /// else is a bug that takes two clicks to notice.
    /// </remarks>
    partial void OnIsExpandedChanged(bool value) {
        if (value) {
            _tree.Opened(this);
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
