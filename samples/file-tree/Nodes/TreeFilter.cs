using Shiranuix.Plugins;
using Shiranuix.Plugins.Text;

namespace FileTree.Nodes;

/// <summary>
/// Which nodes a tree is showing, once a filter has had its say.
/// </summary>
/// <remarks>
/// <para>
/// Filtering a tree is not filtering a list. A folder whose name does not match
/// is not enough to hide it - the match may be twenty levels down - so the
/// whole tree is walked and the answer is which folders still lead somewhere
/// that matches.
/// </para>
/// <para>
/// A tree with a filter in it shows the path to every match, because a writer
/// looking for "chapter nine" wants to see which chapter they found, and a
/// match without its path is a bare word floating in a column.
/// </para>
/// </remarks>
public static class TreeFilter {
    /// <summary>
    /// The name of every node that matches, and the folders leading to them.
    /// </summary>
    /// <param name="node">Where to start, usually the tree's root.</param>
    /// <param name="query">
    /// What to look for, or null or blank for "show everything".
    /// </param>
    /// <returns>
    /// The children to show under <paramref name="node"/>, filtered; the whole
    /// list unchanged when there is nothing to filter by.
    /// </returns>
    /// <remarks>
    /// The filter only reads nodes that have already been loaded, because that
    /// is all a tree knows about until a folder is opened. Filtering therefore
    /// never pulls a folder off the disk to look inside it - doing that would
    /// turn a typing box into a full scan of the disk on every keystroke.
    /// </remarks>
    public static IReadOnlyList<FileNode> VisibleChildren(FileNode node, string? query) {
        ArgumentNullException.ThrowIfNull(node);

        if (string.IsNullOrWhiteSpace(query)) {
            return node.Children;
        }

        var needle = query.Trim();

        return node.Children
            .Where(child => LeadsToAMatch(child, needle, depth: 0))
            .ToArray();
    }

    /// <summary>
    /// Whether a filter would show this node at all.
    /// </summary>
    /// <param name="node">The node to judge.</param>
    /// <param name="query">What to look for.</param>
    /// <returns>True when the node matches, or has a match under it.</returns>
    public static bool LeadsToAMatch(FileNode node, string query, int depth = 0) {
        ArgumentNullException.ThrowIfNull(node);

        if (Contains(node.Name, query)) {
            return true;
        }

        // Three levels is as far as a filter reaches on its own. Past that the
        // tree has to open a folder to find out, and a filter that opens
        // folders on every keystroke is a filter nobody can type into.
        if (!node.IsDirectory || !node.IsLoaded || depth >= 3) {
            return false;
        }

        return node.Children.Any(child => LeadsToAMatch(child, query, depth + 1));
    }

    private static bool Contains(string name, string query) =>
        name.Contains(query, StringComparison.OrdinalIgnoreCase);
}
