using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileTree.Nodes;
using Shiranuix.Plugins;

namespace FileTree.State;

/// <summary>
/// The tree's orders, and the rows a writer has marked on the way to doing
/// something to several things at once.
/// </summary>
/// <remarks>
/// Two small questions, kept in a file of their own because that is all they
/// are: what order the rows are in, and which of them the writer has marked.
/// Marking is what makes "delete these four" possible without a mouse, and
/// an order is what makes a folder of two hundred files findable at all.
/// </remarks>
public sealed partial class FileTreeState {

    /// <summary>
    /// What a folder shows, once the filter has had its say.
    /// </summary>
    /// <param name="node">The folder to ask about.</param>
    /// <returns>Its children the writer should see.</returns>
    public IReadOnlyList<FileNode> ChildrenOf(FileNode node) {
        ArgumentNullException.ThrowIfNull(node);

        var visible = TreeFilter.VisibleChildren(node, Filter);

        // Folders first whatever the order is: a writer looking for a file
        // should not have to look past a wall of folders to find it, even in a
        // tree sorted by date.
        IEnumerable<FileNode> ordered = Sort switch {
            TreeSort.Kind => visible
                .OrderBy(child => Path.GetExtension(child.Name), StringComparer.OrdinalIgnoreCase)
                .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase),
            TreeSort.Date => visible.OrderBy(child => child.ModifiedUtc),
            _ => visible.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
        };

        var grouped = ordered
            .OrderBy(child => child.IsDirectory ? 0 : 1)
            .ToArray();

        return Descending ? [.. grouped.Reverse()] : grouped;
    }

    /// <summary>What the tree's rows are in.</summary>
    [ObservableProperty]
    private TreeSort _sort = TreeSort.Name;

    /// <summary>Whether that order runs the other way.</summary>
    [ObservableProperty]
    private bool _descending;

    /// <summary>The rows the writer has marked, in the order they marked them.</summary>
    public ObservableCollection<FileTreeItem> Marked { get; } = [];

    /// <summary>Marks a row, or unmarks it if it was already marked.</summary>
    /// <param name="item">The row to mark.</param>
    /// <returns>The outcome: Ok when marked, and a sentence when unmarked.</returns>
    public PluginOutcome ToggleMark(FileTreeItem item) {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsMarked) {
            Marked.Remove(item);
            item.IsMarked = false;

            return PluginOutcome.Failure($"'{item.Name}' is no longer marked.");
        }

        Marked.Add(item);
        item.IsMarked = true;

        return PluginOutcome.Ok();
    }

    /// <summary>Forgets every marked row.</summary>
    public void ClearMarks() {
        foreach (var item in Marked) {
            item.IsMarked = false;
        }

        Marked.Clear();
    }

    /// <summary>
    /// Puts the rows in another order, and says which.
    /// </summary>
    /// <param name="sort">The order to use.</param>
    /// <returns>The outcome.</returns>
    public PluginOutcome SortBy(TreeSort sort) {
        Sort = sort;

        if (Root is not null) {
            RebuildItems(keepRows: true);
        }

        OnPropertyChanged(nameof(Items));
        return PluginOutcome.Ok();
    }

    /// <summary>
    /// Turns the order the other way round.
    /// </summary>
    /// <returns>The outcome.</returns>
    public PluginOutcome TurnOrder() {
        Descending = !Descending;

        if (Root is not null) {
            RebuildItems(keepRows: true);
        }

        OnPropertyChanged(nameof(Items));
        return PluginOutcome.Ok();
    }
}
