using FileTree.Io;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;

namespace FileTree.Commands;

/// <summary>
/// The commands the tree offers, in the order the settings list them.
/// </summary>
/// <remarks>
/// The tree's own verbs, and nothing that belongs to the editor: opening a
/// file is the editor's door, deleting is the editor's question, and both are
/// reached through the plugin rather than around it. What is left here is
/// what only a tree can do.
/// </remarks>
internal static class TreeCommands {
    /// <summary>
    /// Every command the tree offers.
    /// </summary>
    /// <param name="plugin">The plugin they act on.</param>
    /// <returns>The commands, each already bound to its own work.</returns>
    public static IReadOnlyList<IPluginCommand> All(FileTreePlugin plugin) {
        ArgumentNullException.ThrowIfNull(plugin);

        return [
            // Opening a folder is not one of the tree's commands. It is the
            // editor's own, asked for by name in the palette, and it stands
            // this tree on the folder by hearing about it - one command for one
            // thing, so the palette holds one row for it rather than two a
            // writer cannot tell apart.
            //
            // The tree still moves by itself: the button in its own heading
            // asks where the tree should stand next, which is the tree's own
            // question and not the editor's.

            new TreeCommand(
                "file-tree.refresh", "Refresh tree", "F5",
                "Reads the folder again, keeping what is open.",
                token => plugin.Tree.RefreshAsync(token)),

            new TreeCommand(
                "file-tree.new-file", "New file", null,
                "Makes an empty file in the folder that is selected, and opens it.",
                token => WithSelected(plugin, async (tree, item) => {
                    var made = FileOperations.CreateFile(FolderFor(plugin, item), "untitled.txt");

                    if (!made.IsSuccess) {
                        return made.Plain();
                    }

                    await tree.RefreshAsync(token);
                    return await plugin.OpenFileAsync(made.Value, token);
                })),

            new TreeCommand(
                "file-tree.new-folder", "New folder", null,
                "Makes a folder inside the folder that is selected.",
                token => WithSelected(plugin, async (tree, item) => {
                    var made = FileOperations.CreateFolder(FolderFor(plugin, item), "new folder");
                    if (made.IsSuccess) {
                        await tree.RefreshAsync(token);
                    }

                    return made.Plain();
                })),

            new TreeCommand(
                "file-tree.move-to", "Move to…", null,
                "Picks a folder and moves what is selected into it, after asking.",
                token => WithSelected(plugin, (_, item) =>
                    plugin.ChooseWhereToMoveAsync(item.Node, token))),

            new TreeCommand(
                "file-tree.delete", "Delete", "Delete",
                "Deletes what is selected, after asking.",
                token => WithSelected(plugin, (_, item) =>
                    plugin.DeleteAsync(item.Node, token))),

            new TreeCommand(
                "file-tree.delete-marked", "Delete marked", null,
                "Deletes everything that has been marked, after asking about each.",
                async token => {
                    var marked = plugin.Tree.Marked.ToArray();
                    if (marked.Length == 0) {
                        return PluginOutcome.Failure("Nothing is marked.");
                    }

                    var failures = 0;
                    foreach (var item in marked) {
                        var outcome = await plugin.DeleteAsync(item.Node, token);
                        if (!outcome.IsSuccess) {
                            failures++;
                        }
                    }

                    plugin.Tree.ClearMarks();
                    await plugin.Tree.RefreshAsync(token);

                    return failures == 0
                        ? PluginOutcome.Ok()
                        : PluginOutcome.Failure($"{failures} of {marked.Length} did not go.");
                }),

            new TreeCommand(
                "file-tree.sort-name", "Sort by name", null,
                "Puts the rows in name order.",
                _ => Task.FromResult(plugin.Tree.SortBy(TreeSort.Name))),

            new TreeCommand(
                "file-tree.sort-kind", "Sort by kind", null,
                "Puts the rows in by what they are, then by name.",
                _ => Task.FromResult(plugin.Tree.SortBy(TreeSort.Kind))),

            new TreeCommand(
                "file-tree.sort-date", "Sort by date", null,
                "Puts the rows in when they last changed.",
                _ => Task.FromResult(plugin.Tree.SortBy(TreeSort.Date))),

            new TreeCommand(
                "file-tree.sort-direction", "Reverse order", null,
                "Turns the order the other way round.",
                _ => Task.FromResult(plugin.Tree.TurnOrder())),

            new TreeCommand(
                "file-tree.toggle-hidden", "Show hidden files", null,
                "Shows the names that start with a dot, or does not.",
                _ => {
                    plugin.Tree.ShowHidden = !plugin.Tree.ShowHidden;
                    plugin.Tree.Status = plugin.Tree.ShowHidden
                        ? "Showing names that start with a dot."
                        : "Not showing names that start with a dot.";

                    return Task.FromResult(PluginOutcome.Ok());
                })
        ];
    }

    /// <summary>
    /// Runs a command against whatever row is selected, or against the root
    /// when nothing is: a writer who has just opened the panel and pressed
    /// "new file" means "here".
    /// </summary>
    private static async Task<PluginOutcome> WithSelected(
        FileTreePlugin plugin,
        Func<FileTreeState, FileTreeItem, Task<PluginOutcome>> work) {
        var tree = plugin.Tree;
        var selected = tree.Selected;

        if (selected is null) {
            var root = tree.Root ?? FileNode.Of("here", string.Empty, isDirectory: true, null);
            selected = new FileTreeItem(tree, root, null);
        }

        return await work(tree, selected);
    }

    private static string FolderFor(FileTreePlugin plugin, FileTreeItem item) {
        var node = item.Node;

        return node.IsDirectory
            ? node.FullPath
            : node.Parent?.FullPath ?? plugin.Tree.Root?.FullPath ?? string.Empty;
    }

    /// <summary>
    /// Drops the value an outcome was carrying, keeping what it said.
    /// </summary>
    /// <param name="outcome">The outcome, with or without a value.</param>
    /// <returns>The same outcome, without a value.</returns>
    internal static PluginOutcome Plain(this PluginOutcome<string> outcome) =>
        outcome.IsSuccess
            ? PluginOutcome.Ok()
            : PluginOutcome.Failure(outcome.Error ?? "That did not work.");
}
