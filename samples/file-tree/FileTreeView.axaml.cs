using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Shiranuix.Plugins.Commands;

namespace FileTree;

/// <summary>
/// The tree's view, and the three questions the markup cannot answer for it.
/// </summary>
/// <remarks>
/// Which row is open, which row is being renamed, and what the arrow keys
/// mean. All three are the tree's state rather than its drawing, so they live
/// here and everything else is bound. The keys are the editor's page keys -
/// up and down along the rows, left and right into and out of a folder - because
/// a writer who uses one tree uses every tree that way.
/// </remarks>
public partial class FileTreeView : UserControl {
    private FileTreePlugin? _plugin;

    /// <summary>
    /// Creates the view.
    /// </summary>
    public FileTreeView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Hands the view the plugin it belongs to, so the keys and the buttons
    /// have somewhere to send what the writer asked for.
    /// </summary>
    /// <param name="plugin">The plugin the tree belongs to.</param>
    public void Bind(FileTreePlugin plugin) =>
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

    private FileTreeState? TreeState => DataContext as FileTreeState;

    /// <summary>
    /// Runs one of the tree's commands by its id.
    /// </summary>
    /// <param name="id">The command's id.</param>
    /// <param name="parameter">What it was asked with, when it takes something.</param>
    /// <returns>A task that finishes when the command has.</returns>
    public async Task RunAsync(string id, object? parameter = null) {
        var command = _plugin?.Commands.FirstOrDefault(candidate => candidate.Info.Id == id);
        if (command is null) {
            return;
        }

        var outcome = await command.ExecuteAsync(CancellationToken.None);
        if (!outcome.IsSuccess && TreeState is not null) {
            TreeState.Status = outcome.Error ?? string.Empty;
        }
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            TreeState?.Filter = string.Empty;
            e.Handled = true;
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.refresh");

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.open-folder");

    private void OnRowSelected(object? sender, SelectionChangedEventArgs e) {
        if (TreeState is not { } tree) {
            return;
        }

        TreeState.Selected = (e.AddedItems.OfType<FileTreeItem>()).FirstOrDefault();
    }

    private void OnRowActivated(object? sender, TappedEventArgs e) {
        if (TreeState is not { } tree || tree.Selected is not { } row) {
            return;
        }

        if (row.IsDirectory) {
            // Enter on a folder does what a double click does in every tree:
            // opens it if it is closed, closes it if it is open.
            if (row.IsExpanded) {
                tree.Collapse(row);
            } else {
                tree.Expand(row);
            }

            return;
        }

        // A file opens in the editor, through the editor's own door.
        _ = OpenSelected(row);
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e) {
        var tree = TreeState;
        if (tree is null) {
            return;
        }

        switch (e.Key) {
            case Key.Down or Key.Up:
                Step(tree, e.Key == Key.Down ? 1 : -1);
                break;
            case Key.Right:
                OpenOrDescend(tree, descend: true);
                break;
            case Key.Left:
                OpenOrDescend(tree, descend: false);
                break;
            case Key.Enter:
                if (tree.Selected is { IsDirectory: false } file) {
                    _ = OpenSelected(file);
                }

                break;
            case Key.F2:
                StartRename(tree);
                break;
            case Key.Delete:
                FireAndForget("file-tree.delete");
                break;
            case Key.F5:
                FireAndForget("file-tree.refresh");
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e) {
        if (TreeState is not { } tree || tree.Selected is not { } row) {
            return;
        }

        switch (e.Key) {
            case Key.Escape:
                row.IsEditing = false;
                break;
            case Key.Enter:
                row.IsEditing = false;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// The writer finished typing a new name: the disk is told, and the tree
    /// is read again, because a row that says a name the disk has not heard
    /// of is a row that is lying.
    /// </summary>
    /// <param name=""sender"">The editor in the row.</param>
    /// <param name=""e"">The event (unused).</param>
    private void OnRenameFinished(object? sender, RoutedEventArgs e) {
        if (TreeState is not { } tree || tree.Selected is not { } row) {
            return;
        }

        row.IsEditing = false;

        var wanted = (sender as TextBox)?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(wanted) && !string.Equals(wanted, row.Name, StringComparison.Ordinal)) {
            _ = _plugin?.RenameAsync(row.Node, wanted, CancellationToken.None);
        }
    }

    private static void StartRename(FileTreeState tree) {
        if (tree.Selected is { } row) {
            row.IsEditing = true;
        }
    }

    private static void Step(FileTreeState tree, int by) {
        var rows = tree.All().ToArray();
        var index = Array.IndexOf(rows, tree.Selected);

        if (rows.Length == 0) {
            return;
        }

        var next = Math.Clamp(index + by, 0, rows.Length - 1);
        tree.Selected = rows[index < 0 ? 0 : next];
    }

    private static void OpenOrDescend(FileTreeState tree, bool descend) {
        if (tree.Selected is not { } row) {
            return;
        }

        if (descend && row.IsDirectory && !row.IsExpanded) {
            tree.Expand(row);
            return;
        }

        if (!descend && row.IsExpanded && row.IsDirectory) {
            tree.Collapse(row);
            return;
        }

        // Right on a file, left on the last row of a folder: step out to the
        // parent, which is what every tree has always done.
        if (row.Parent is { } up) {
            tree.Selected = up;
        }
    }

    private Task OpenSelected(FileTreeItem row) =>
        _plugin is null
            ? Task.CompletedTask
            : _plugin.OpenFileAsync(row.FullPath, CancellationToken.None);

    private void FireAndForget(string id) => _ = RunAsync(id);
}
