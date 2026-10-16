using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FileTree.Io;
using FileTree.Nodes;
using FileTree.State;
using Shiranuix.Plugins.Commands;

namespace FileTree.Views;

/// <summary>
/// The tree's view, and the questions the markup cannot answer for it.
/// </summary>
/// <remarks>
/// <para>
/// Which row is open, which row is being renamed, what the arrow keys mean,
/// and what a menu item should act on. All four are the tree's state rather
/// than its drawing, so they live here and everything else is bound.
/// </para>
/// <para>
/// The keys are the editor's page keys - up and down along the rows, left and
/// right into and out of a folder - because a writer who uses one tree uses
/// every tree that way. Space marks a row rather than selecting it, which is
/// what makes "delete these four" a thing a writer can do without a mouse.
/// </para>
/// <para>
/// The two things a view alone has to do - putting a path on the clipboard and
/// opening a folder where the operating system keeps it - are here rather than
/// in the plugin, because both need a running window and neither is the
/// plugin's business.
/// </para>
/// </remarks>
public partial class FileTreeView : UserControl {
    private FileTreePlugin? _plugin;

    /// <summary>
    /// Creates the view.
    /// </summary>
    public FileTreeView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Hands the view the plugin it belongs to, so the keys and the menu have
    /// somewhere to send what the writer asked for.
    /// </summary>
    /// <param name="plugin">The plugin the tree belongs to.</param>
    public void Bind(FileTreePlugin plugin) =>
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

    private FileTreeState? TreeState => DataContext as FileTreeState;

    /// <summary>
    /// Runs one of the tree's commands by its id.
    /// </summary>
    /// <param name="id">The command's id.</param>
    /// <returns>A task that finishes when the command has.</returns>
    public async Task RunAsync(string id) {
        var command = _plugin?.Commands.FirstOrDefault(candidate => candidate.Info.Id == id);
        if (command is null) {
            return;
        }

        var outcome = await command.ExecuteAsync(CancellationToken.None);
        if (!outcome.IsSuccess && outcome.Error is { } said && !said.StartsWith("Unmarked")) {
            TreeState?.Status = said;
        }
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key == Key.Escape && TreeState is { } tree) {
            tree.Filter = string.Empty;
            e.Handled = true;
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => FireAndForget("file-tree.refresh");

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.open-folder");

    private void OnNewFileClick(object? sender, RoutedEventArgs e) => FireAndForget("file-tree.new-file");

    private void OnNewFolderClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.new-folder");

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => FireAndForget("file-tree.delete");

    private void OnMarkClick(object? sender, RoutedEventArgs e) {
        if (TreeState is { } tree && tree.Selected is { } row) {
            tree.ToggleMark(row);
        }
    }

    private void OnRenameClick(object? sender, RoutedEventArgs e) {
        if (TreeState?.Selected is { } row) {
            row.IsEditing = true;
        }
    }

    private void OnHiddenClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.toggle-hidden");

    private void OnSortNameClick(object? sender, RoutedEventArgs e) => Sort(TreeSort.Name);

    private void OnSortKindClick(object? sender, RoutedEventArgs e) => Sort(TreeSort.Kind);

    private void OnSortDateClick(object? sender, RoutedEventArgs e) => Sort(TreeSort.Date);

    private void OnSortDirectionClick(object? sender, RoutedEventArgs e) {
        TreeState?.TurnOrder();
    }

    /// <summary>
    /// Puts the row the writer chose on the clipboard, which is the only way
    /// a path reaches another program.
    /// </summary>
    /// <param name="sender">The menu item.</param>
    /// <param name="e">The event (unused).</param>
    private async void OnCopyPathClick(object? sender, RoutedEventArgs e) {
        if (TreeState?.Selected is not { } row) {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) {
            return;
        }

        await clipboard.SetTextAsync(row.FullPath);
        TreeState.Status = "Path copied.";
    }

    /// <summary>
    /// Opens the folder where the operating system keeps it, because a writer
    /// who wants to see a file in Explorer means Explorer and not this tree.
    /// </summary>
    /// <param name="sender">The menu item.</param>
    /// <param name="e">The event (unused).</param>
    private async void OnRevealClick(object? sender, RoutedEventArgs e) {
        if (TreeState?.Selected is not { } row) {
            return;
        }

        var folder = row.IsDirectory ? row.FullPath : row.Node.Parent?.FullPath;
        if (folder is null || !Directory.Exists(folder)) {
            return;
        }

        Explorer.Reveal(folder, row.IsDirectory ? null : row.FullPath);
    }

    private void OnRowSelected(object? sender, SelectionChangedEventArgs e) {
        if (TreeState is { } tree) {
            tree.Selected = e.AddedItems.OfType<FileTreeItem>().FirstOrDefault();
        }
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
            case Key.Space:
                if (tree.Selected is { } marked) {
                    tree.ToggleMark(marked);
                }

                break;
            case Key.F2:
                if (tree.Selected is { } renaming) {
                    renaming.IsEditing = true;
                }

                break;
            case Key.Delete:
                FireAndForget(tree.Marked.Count > 0 ? "file-tree.delete-marked" : "file-tree.delete");
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
        if (e.Key is Key.Escape or Key.Enter && TreeState is { } tree && tree.Selected is { } row) {
            row.IsEditing = false;
            e.Handled = true;
        }
    }

    /// <summary>
    /// The writer finished typing a new name: the disk is told, and the tree
    /// is read again, because a row that says a name the disk has not heard of
    /// is a row that is lying.
    /// </summary>
    /// <param name="sender">The editor in the row.</param>
    /// <param name="e">The event (unused).</param>
    private void OnRenameFinished(object? sender, RoutedEventArgs e) {
        if (TreeState is not { } tree || tree.Selected is not { } row) {
            return;
        }

        row.IsEditing = false;

        var wanted = (sender as TextBox)?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(wanted)
            && !string.Equals(wanted, row.Name, StringComparison.Ordinal)) {
            _ = _plugin?.RenameAsync(row.Node, wanted, CancellationToken.None);
        }
    }

    private void Sort(TreeSort sort) => TreeState?.SortBy(sort);



    private static void Step(FileTreeState tree, int by) {
        var rows = tree.All().ToArray();
        if (rows.Length == 0) {
            return;
        }

        var index = Array.IndexOf(rows, tree.Selected);
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
