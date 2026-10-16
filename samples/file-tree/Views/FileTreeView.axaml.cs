using Avalonia;
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
    private Carrying? _carrying;

    /// <summary>
    /// Creates the view.
    /// </summary>
    /// <remarks>
    /// The press is listened for in the tunnel phase, and for presses already
    /// marked handled. Avalonia's row control marks a click as its own before
    /// anything in the row can hear it, so a handler attached to a row's
    /// content never runs - which is why "click a folder to open it" could be
    /// wired, compiled and still do nothing. Tunnelling happens on the way
    /// down, before any control has had the chance to swallow the press.
    /// </remarks>
    public FileTreeView() {
        InitializeComponent();

        AddHandler(
            InputElement.PointerPressedEvent,
            OnRowPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        // A press that travels becomes a drag; one that stays is a click.
        AddHandler(
            InputElement.PointerPressedEvent,
            OnDragStartedByPress,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMovedWhileCarrying,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        // Drops are listened for here rather than written in markup: Avalonia
        // carries drag events as attached handlers, so a tree that wants to be
        // dropped on says so in code, on the control that holds the rows.
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnTreeDragOver);
        DragDrop.AddDropHandler(this, OnTreeDrop);

        // Long names are shortened after every layout, and only for the rows
        // that asked to be: Avalonia finds an attached property through its
        // registry, so a handler of its own would never be installed.
        //
        // Asked of this control rather than of the tree inside it, because
        // a plugin's XAML is loaded at run time and only a compiled one fills
        // in the fields its names stand for.
        LayoutUpdated += (_, _) => MiddleTrim.Trim(this);
    }

    /// <summary>What a press is holding on to, until it travels or lets go.</summary>
    /// <param name="row">The row under the press.</param>
    /// <param name="from">Where the press began.</param>
    /// <param name="press">The press a drag begins from.</param>
    private sealed record Carrying(FileTreeItem Row, Point From, PointerPressedEventArgs Press);

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

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => FireAndForget("file-tree.refresh");

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) {
        TreeState?.CollapseAll();
    }

    /// <summary>
    /// A row was pressed: a folder opens or closes, and a file opens.
    /// </summary>
    /// <param name="sender">The row's own border.</param>
    /// <param name="e">The press.</param>
    /// <remarks>
    /// One press, in both cases. Both go through the row and the tree rather
    /// than through the view: the twisty, the keys and the double press all
    /// say the same things about the same rows, so they cannot disagree about
    /// what open means.
    /// </remarks>
    private void OnRowPressed(object? sender, PointerPressedEventArgs e) {
        if (TreeState is not { } tree) {
            return;
        }

        // The row is found from whatever was actually pressed rather than from
        // the sender, because the press may be swallowed on the way up: this
        // handler is attached in the tunnel phase precisely so that it hears
        // every press in the tree, including the ones the row control marks as
        // its own.
        if (RowAt(e.Source) is not { } row) {
            return;
        }

        // The right button belongs to the menu, and a second button is not a
        // request to open anything.
        var pressed = e.GetCurrentPoint(this).Properties;
        if (!pressed.IsLeftButtonPressed || pressed.IsRightButtonPressed || row.IsEditing) {
            return;
        }

        tree.Selected = row;

        if (row.IsDirectory) {
            if (row.IsExpanded) {
                tree.Collapse(row);
            } else {
                tree.Expand(row);
            }

            return;
        }

        _ = OpenSelected(row);
    }

    /// <summary>
    /// The row a press landed on, if it landed on one.
    /// </summary>
    /// <param name="source">Whatever the press was reported on.</param>
    /// <returns>The row, or null.</returns>
    /// <remarks>
    /// Walks up from whatever was pressed - a symbol, a name, the row's own
    /// border - until it finds something whose data is a row. Anything else in
    /// the tree (the heading, a row's buttons) has no row and is not a request
    /// about one.
    /// </remarks>
    private static FileTreeItem? RowAt(object? source) {
        for (var walked = source as Avalonia.StyledElement; walked is not null; walked = walked.Parent) {
            if (walked.DataContext is FileTreeItem row) {
                return row;
            }
        }

        return null;
    }

    /// <summary>
    /// The heading's folder button: the tree asking where it should stand
    /// next. It goes through the tree's own command rather than calling the
    /// work, so the button and the palette row are one thing - the same door,
    /// pressed from two places.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The press.</param>
    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.open-folder");

    private void OnNewFileClick(object? sender, RoutedEventArgs e) => FireAndForget("file-tree.new-file");

    private void OnNewFolderClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.new-folder");

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => FireAndForget("file-tree.delete");

    private void OnMoveToClick(object? sender, RoutedEventArgs e) =>
        FireAndForget("file-tree.move-to");

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

    /// <summary>
    /// A press that turns into a drag: the row is picked up and carried.
    /// </summary>
    /// <param name="sender">The tree.</param>
    /// <param name="e">The press, still going down.</param>
    /// <remarks>
    /// Nothing is dragged until the pointer has actually travelled, so a
    /// click stays a click - which matters here, because a click is how a file
    /// is opened and how a folder is opened, and a tree that started dragging
    /// on the first pixel would open nothing.
    /// </remarks>
    private void OnDragStartedByPress(object? sender, PointerPressedEventArgs e) {
        if (RowAt(e.Source) is not { } row || row.IsEditing) {
            return;
        }

        var press = e.GetCurrentPoint(this);
        if (!press.Properties.IsLeftButtonPressed || press.Properties.IsRightButtonPressed) {
            return;
        }

        _carrying = new Carrying(row, press.Position, e);
    }

    /// <summary>
    /// The pointer moved while a row was held: once it has travelled far
    /// enough, the row is picked up and carried.
    /// </summary>
    /// <param name="sender">The tree.</param>
    /// <param name="e">The movement.</param>
    /// <remarks>
    /// Nothing is dragged until the pointer travels, so a click stays a click -
    /// which matters here, because a click is how a file opens and how a
    /// folder opens.
    /// </remarks>
    private void OnPointerMovedWhileCarrying(object? sender, PointerEventArgs e) {
        if (_carrying is not { } carrying) {
            return;
        }

        if (!TreeDrag.HasTravelled(carrying.From, e.GetPosition(this))) {
            return;
        }

        _carrying = null;

        _ = DragDrop.DoDragDropAsync(
            carrying.Press,
            TreeDrag.Pack([carrying.Row]),
            DragDropEffects.Move);
    }

    /// <summary>
    /// A press ended without travelling: it was a click, and nothing is being
    /// carried any more.
    /// </summary>
    /// <param name="sender">The tree.</param>
    /// <param name="e">The release (unused).</param>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        _carrying = null;

    /// <summary>
    /// Something is being carried over the tree: say whether it may be put
    /// down here, and refuse where it must not be.
    /// </summary>
    /// <param name="sender">The tree.</param>
    /// <param name="e">The drag.</param>
    private void OnTreeDragOver(object? sender, DragEventArgs e) {
        var carried = TreeDrag.Carried(e.DataTransfer).FirstOrDefault();

        e.DragEffects = carried is not null
            && RowAt(e.Source) is { } under
            && TreeDrag.CanDropOn(carried, under)
                ? DragDropEffects.Move
                : DragDropEffects.None;

        e.Handled = true;
    }

    /// <summary>
    /// Something has been put down: move it, and read the tree again so what
    /// is on screen is where things actually are.
    /// </summary>
    /// <param name="sender">The tree.</param>
    /// <param name="e">The drop.</param>
    private async void OnTreeDrop(object? sender, DragEventArgs e) {
        var carried = TreeDrag.Carried(e.DataTransfer).FirstOrDefault();
        if (carried is null
            || RowAt(e.Source) is not { } under
            || _plugin is not { } plugin
            || TreeState is not { } tree) {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        if (!TreeDrag.CanDropOn(carried, under)) {
            tree.Status = $"'{carried.Name}' cannot go into '{under.Name}'.";
            return;
        }

        await TreeDrag.MoveAsync(plugin, carried, under).ConfigureAwait(true);
        if (tree.Status.Length == 0) {
            tree.Status = $"Dropped {carried.Name} in {under.Name}.";
        }
    }

    private Task OpenSelected(FileTreeItem row) =>
        _plugin is null
            ? Task.CompletedTask
            : _plugin.OpenFileAsync(row.FullPath, CancellationToken.None);

    private void FireAndForget(string id) => _ = RunAsync(id);
}
