using Shiranuix;
using Shiranuix.FileTree.Nodes;
using Shiranuix.FileTree.State;

namespace Shiranuix.FileTree.Commands;

/// <summary>
/// The commands the tree offers, in the order the settings list them.
/// </summary>
/// <remarks>
/// The tree's own verbs, and nothing that belongs to the editor: opening a
/// file is the editor's door, deleting is the editor's question, and both are
/// reached through the tree's host rather than around it. What is left here is
/// what only a tree can do.
/// </remarks>
public static class TreeCommands {
    /// <summary>
    /// Every command the tree offers.
    /// </summary>
    /// <param name="host">The tree they act on.</param>
    /// <returns>The commands, each already bound to its own work.</returns>
    public static IReadOnlyList<TreeCommand> All(FileTreeHost host) {
        ArgumentNullException.ThrowIfNull(host);

        return [
            // Opening a folder belongs to the tree, not to the editor: the
            // editor opens documents and has no idea what a folder is for.
            // This is the tree's own door onto it, the one place the work is
            // written, and standing there is announced on the channel, so
            // anything else that has work to do with folders hears about it
            // rather than having to ask.
            new TreeCommand(
                "file-tree.open-folder", "Open folder", "Ctrl+Shift+O",
                "Stands the tree on a folder of the writer's choosing.",
                async token => (await host.ChooseFolderAsync(token)).Plain()),

            new TreeCommand(
                "file-tree.refresh", "Refresh tree", "F5",
                "Reads the folder again, keeping what is open.",
                token => host.Tree.RefreshAsync(token), () => HasRoot(host)),

            new TreeCommand(
                "file-tree.collapse-all", "Close every folder", null,
                "Puts every folder but the one the tree stands on back together.",
                _ => {
                    host.Tree.CollapseAll();
                    return Task.FromResult(Outcome.Ok());
                }, () => HasRoot(host)),

            new TreeCommand(
                "file-tree.new-file", "New file", null,
                "Makes room for a new file's name in the tree; the file is born when the name is.",
                _ => {
                    host.Tree.BeginNewFile();
                    return Task.FromResult(Outcome.Ok());
                }, () => HasRoot(host)),

            new TreeCommand(
                "file-tree.new-folder", "New folder", null,
                "Makes room for a new folder's name in the tree; the folder is born when the name is.",
                _ => {
                    host.Tree.BeginNewFolder();
                    return Task.FromResult(Outcome.Ok());
                }, () => HasRoot(host)),

            new TreeCommand(
                "file-tree.move-to", "Move to…", null,
                "Picks a folder and moves what is selected into it, after asking.",
                token => WithSelected(host, (_, item) =>
                    host.ChooseWhereToMoveAsync(item.Node, token)), () => HasChildSelected(host)),

            new TreeCommand(
                "file-tree.delete", "Delete", "Delete",
                "Deletes what is selected, after asking.",
                token => WithSelected(host, (_, item) =>
                    host.DeleteAsync(item.Node, token)), () => HasChildSelected(host)),

            new TreeCommand(
                "file-tree.delete-marked", "Delete marked", null,
                "Deletes everything that has been marked, after asking about each.",
                async token => {
                    var marked = host.Tree.Marked.ToArray();
                    if (marked.Length == 0) {
                        return Outcome.Failure("Nothing is marked.");
                    }

                    var failures = 0;
                    foreach (var item in marked) {
                        var outcome = await host.DeleteAsync(item.Node, token);
                        if (!outcome.IsSuccess) {
                            failures++;
                        }
                    }

                    host.Tree.ClearMarks();
                    await host.Tree.RefreshAsync(token);

                    return failures == 0
                        ? Outcome.Ok()
                        : Outcome.Failure($"{failures} of {marked.Length} did not go.");
                }, () => host.Tree.Marked.Count > 0),

            new TreeCommand(
                "file-tree.sort-name", "Sort by name", null,
                "Puts the rows in name order.",
                _ => Task.FromResult(host.Tree.SortBy(TreeSort.Name)), () => HasRoot(host)),

            new TreeCommand(
                "file-tree.sort-kind", "Sort by kind", null,
                "Puts the rows in by what they are, then by name.",
                _ => Task.FromResult(host.Tree.SortBy(TreeSort.Kind)), () => HasRoot(host)),

            new TreeCommand(
                "file-tree.sort-date", "Sort by date", null,
                "Puts the rows in when they last changed.",
                _ => Task.FromResult(host.Tree.SortBy(TreeSort.Date)), () => HasRoot(host)),

            new TreeCommand(
                "file-tree.sort-direction", "Reverse order", null,
                "Turns the order the other way round.",
                _ => Task.FromResult(host.Tree.TurnOrder()), () => HasRoot(host)),

            new TreeCommand(
                "file-tree.toggle-hidden", "Show hidden files", null,
                "Shows the names that start with a dot, or does not.",
                _ => {
                    host.Tree.ShowHidden = !host.Tree.ShowHidden;
                    host.Tree.Status = host.Tree.ShowHidden
                        ? "Showing names that start with a dot."
                        : "Not showing names that start with a dot.";

                    return Task.FromResult(Outcome.Ok());
                }, () => HasRoot(host))
        ];
    }

    /// <summary>
    /// Whether there is a folder to act on at all. Everything except opening
    /// one is about a folder that is there, and says so rather than failing
    /// once a writer has pressed it.
    /// </summary>
    /// <param name="host">The tree they act on.</param>
    /// <returns>Whether the tree is standing somewhere.</returns>
    private static bool HasRoot(FileTreeHost host) => host.Tree.HasRoot;

    /// <summary>
    /// Whether a row under the folder is selected - the two commands that
    /// destroy or move something need one, and the folder the tree is
    /// standing on is not a row to destroy: it is where the writer is.
    /// </summary>
    /// <param name="host">The tree they act on.</param>
    /// <returns>Whether something below the root is selected.</returns>
    private static bool HasChildSelected(FileTreeHost host) =>
        host.Tree.Selected is { } row && !ReferenceEquals(row.Node, host.Tree.Root);

    /// <summary>
    /// Runs a command against whatever row is selected, or against the root
    /// when nothing is: a writer who has just opened the panel and pressed
    /// "new file" means "here".
    /// </summary>
    private static async Task<Outcome> WithSelected(
        FileTreeHost host,
        Func<FileTreeState, FileTreeItem, Task<Outcome>> work) {
        var tree = host.Tree;
        var selected = tree.Selected;

        if (selected is null) {
            var root = tree.Root ?? FileNode.Of("here", string.Empty, isDirectory: true, null);
            selected = new FileTreeItem(tree, root, null);
        }

        return await work(tree, selected);
    }

    /// <summary>
    /// Drops the value an outcome was carrying, keeping what it said.
    /// </summary>
    /// <param name="outcome">The outcome, with or without a value.</param>
    /// <returns>The same outcome, without a value.</returns>
    internal static Outcome Plain(this Outcome<string> outcome) =>
        outcome.IsSuccess
            ? Outcome.Ok()
            : Outcome.Failure(outcome.Error ?? "That did not work.");
}
