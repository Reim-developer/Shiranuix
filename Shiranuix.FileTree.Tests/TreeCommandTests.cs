using FileTree;
using FileTree.Commands;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Testing;

namespace Shiranuix.FileTree.Tests;

/// <summary>
/// Tests for what the tree's commands say about themselves: whether they can
/// be run right now.
/// </summary>
/// <remarks>
/// The palette draws a command it cannot run as a dimmed row, and Enter on a
/// dimmed row does nothing. That is only true if a command is honest about
/// what it needs, and the two questions a writer asks most often - "where do I
/// put a new file?" and "what have I marked?" - are answered by the tree's
/// state rather than by anything inside the command.
/// </remarks>
public sealed class TreeCommandTests {
    [Fact]
    public void Opening_A_Folder_Is_The_Trees_Own_Command_And_The_Only_One() {
        var plugin = new FileTreePlugin();

        var openings = TreeCommands.All(plugin)
            .Where(command => command.Info.Id.Contains("open-folder", StringComparison.Ordinal))
            .ToArray();

        // One door, not two: the editor does not open folders, and a palette
        // holding two rows saying "open folder" is a palette holding a
        // question with two answers.
        Assert.Equal("file-tree.open-folder", Assert.Single(openings).Info.Id);
        Assert.Equal("Ctrl+Shift+O", openings[0].Info.DefaultGesture);
    }

    [Fact]
    public async Task Commands_That_Act_On_A_Folder_Say_They_Cannot_Run_Without_One() {
        var plugin = new FileTreePlugin();
        var commands = TreeCommands.All(plugin);
        Assert.False(plugin.Tree.HasRoot);

        foreach (var id in new[] {
                     "file-tree.refresh", "file-tree.new-file", "file-tree.new-folder",
                     "file-tree.move-to", "file-tree.delete", "file-tree.delete-marked",
                     "file-tree.sort-name", "file-tree.sort-kind", "file-tree.sort-date",
                     "file-tree.sort-direction", "file-tree.toggle-hidden" }) {
            Assert.False(Command(commands, id).Binding.CanExecute(null), $"{id} said it could run.");
        }

        // Opening a folder is the one command that can always be run: it is how
        // there comes to be a folder at all.
        Assert.True(Command(commands, "file-tree.open-folder").Binding.CanExecute(null));

        using var disk = new TempTree();
        disk.WriteFile("chapters/nine.txt");
        await plugin.Tree.OpenRootAsync(disk.Root);

        foreach (var id in new[] {
                     "file-tree.refresh", "file-tree.new-file", "file-tree.new-folder",
                     "file-tree.sort-name", "file-tree.toggle-hidden" }) {
            Assert.True(Command(commands, id).Binding.CanExecute(null), $"{id} said it could not run.");
        }
    }

    [Fact]
    public async Task Moving_And_Deleting_Need_A_Row_And_Not_Just_A_Folder() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var plugin = new FileTreePlugin();
        await plugin.Tree.OpenRootAsync(disk.Root);
        var commands = TreeCommands.All(plugin);

        // A folder is there, but nothing in it is chosen: "here" is what new
        // file means, while delete means this row and there is no row.
        Assert.True(Command(commands, "file-tree.new-file").Binding.CanExecute(null));
        Assert.False(Command(commands, "file-tree.delete").Binding.CanExecute(null));
        Assert.False(Command(commands, "file-tree.move-to").Binding.CanExecute(null));

        plugin.Tree.Selected = plugin.Tree.RootChildren[0];

        Assert.True(Command(commands, "file-tree.delete").Binding.CanExecute(null));
        Assert.True(Command(commands, "file-tree.move-to").Binding.CanExecute(null));
    }

    [Fact]
    public async Task Deleting_Means_A_Row_Under_The_Folder_And_Not_The_Folder_Itself() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var plugin = new FileTreePlugin();
        await plugin.Tree.OpenRootAsync(disk.Root);
        var commands = TreeCommands.All(plugin);

        // The folder the tree is standing on is where the writer is, not
        // something to be destroyed by a slip of the keyboard.
        var root = new FileTreeItem(plugin.Tree, plugin.Tree.Root!, null);
        plugin.Tree.Selected = root;

        Assert.False(Command(commands, "file-tree.delete").Binding.CanExecute(null));
    }

    [Fact]
    public async Task Deleting_Marked_Says_It_Cannot_Run_Until_Something_Is_Marked() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var plugin = new FileTreePlugin();
        await plugin.Tree.OpenRootAsync(disk.Root);
        var commands = TreeCommands.All(plugin);
        var deleting = Command(commands, "file-tree.delete-marked");

        Assert.Empty(plugin.Tree.Marked);
        Assert.False(deleting.Binding.CanExecute(null));

        plugin.Tree.Marked.Add(plugin.Tree.Items[0]);

        Assert.True(deleting.Binding.CanExecute(null));
    }

    [Fact]
    public async Task Selecting_A_Row_Says_To_Whoever_Asked_That_It_Moved() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var plugin = new FileTreePlugin();
        await plugin.Tree.OpenRootAsync(disk.Root);

        foreach (var command in plugin.Commands.OfType<TreeCommand>()) {
            var asked = 0;
            command.Binding.CanExecuteChanged += (_, _) => asked++;

            // Set twice on purpose: the second time the row is already the
            // selected one, and a setter that changes nothing says nothing -
            // which is the case a loop over the commands would fall into.
            plugin.Tree.Selected = null;
            plugin.Tree.Selected = plugin.Tree.RootChildren[0];

            // A palette row or a menu item that is drawn runnable and then is
            // not is a door that opens onto nothing, so the tree has to say so.
            Assert.True(asked > 0, $"{command.Info.Id} was never told its answer moved.");
        }
    }

    [Fact]
    public async Task Marking_A_Row_Says_It_Too_Because_Marks_Are_Not_A_Property() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        var plugin = new FileTreePlugin();
        await plugin.Tree.OpenRootAsync(disk.Root);

        var deleting = plugin.Commands.Single(c => c.Info.Id == "file-tree.delete-marked");
        var asked = 0;
        deleting.Binding.CanExecuteChanged += (_, _) => asked++;

        plugin.Tree.Marked.Add(plugin.Tree.RootChildren[0]);

        // The marks are a collection, so adding to one raises a collection
        // change rather than a property change. Watching only the latter is
        // how "delete marked" stays greyed while things are marked.
        Assert.True(asked > 0);
    }

    [Fact]
    public async Task Standing_On_A_Folder_Announces_It_So_Anything_Else_Can_Hear() {
        using var disk = new TempTree();
        disk.WriteFile("draft.txt");

        // A host that offers the channel: without one the tree still stands,
        // and simply has nobody to tell.
        var channel = new StubPluginEvents();
        using var pluginFiles = new TempTree();
        var plugin = new FileTreePlugin(channel, pluginFiles.Root);

        await plugin.StandOnAsync(disk.Root, CancellationToken.None);

        // The folder is not this tree's business alone: a search across files
        // or a project view would rather hear that there is one than ask
        // whether there is.
        var announced = Assert.IsType<FolderOpened>(
            Assert.Single(channel.Announcements));

        Assert.Equal(disk.Root, announced.Path);
    }

    [Fact]
    public async Task The_Tree_Still_Stands_Where_The_Editor_Opens_A_Document() {
        var plugin = new FileTreePlugin();

        Assert.False(plugin.Tree.HasRoot);
        Assert.Empty(plugin.Panels);
    }

    /// <summary>The command an id names.</summary>
    private static IPluginCommand Command(IReadOnlyList<IPluginCommand> commands, string id) =>
        commands.Single(command => command.Info.Id == id);
}
