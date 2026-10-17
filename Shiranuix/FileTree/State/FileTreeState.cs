using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix;
using Shiranuix.FileTree.Io;
using Shiranuix.FileTree.Nodes;

namespace Shiranuix.FileTree.State;

/// <summary>
/// Everything the tree knows and everything it can be told to do.
/// </summary>
/// <remarks>
/// <para>
/// The tree is a set of rows and a root, and this is the whole of it. Nothing
/// here draws, and nothing here waits for a window: a folder is read when it
/// is opened, a row is selected by a click or by a key, and a file is deleted
/// after the writer has been asked. That is what makes a tree testable at all,
/// and what keeps the panel beside it to nothing but a view.
/// </para>
/// <para>
/// Every move is followed by a re-read of what changed, so the tree can never
/// show something the disk no longer agrees with - including after a failure,
/// where re-reading is what tells the writer the thing is still there.
/// </para>
/// </remarks>
public sealed partial class FileTreeState : ObservableObject {
    private readonly Dictionary<string, FileNode> _nodes = [];
    private readonly HashSet<string> _expanded = [];
    private readonly Action<string>? _say;

    /// <summary>
    /// Creates the tree over a folder nothing has been read from yet.
    /// </summary>
    /// <param name="say">Told about anything the tree would rather not swallow.</param>
    public FileTreeState(Action<string>? say = null) => _say = say;

    /// <summary>The tree's rows: the root itself, once a folder is chosen.</summary>
    public ObservableCollection<FileTreeItem> Items { get; } = [];

    /// <summary>The row for the folder the tree stands on, or none yet.</summary>
    public FileTreeItem? RootRow { get; private set; }

    /// <summary>What the root shows beneath it, or nothing before a folder is chosen.</summary>
    public IReadOnlyList<FileTreeItem> RootChildren => RootRow?.Children ?? [];

    /// <summary>What the tree calls itself, as a heading.</summary>
    public string RootName => Root?.Name ?? string.Empty;

    /// <summary>Where the tree stands, or null before a folder has been chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoot))]
    private FileNode? _root;

    /// <summary>Whether there is a folder to show at all.</summary>
    public bool HasRoot => Root is not null;

    /// <summary>Whether names that start with a dot are shown.</summary>
    [ObservableProperty]
    private bool _showHidden;

    /// <summary>The row the panel's actions work on.</summary>
    [ObservableProperty]
    private FileTreeItem? _selected;

    /// <summary>A short sentence about what just happened.</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>
    /// Stands the tree on a folder and opens it.
    /// </summary>
    /// <param name="path">The folder to stand on.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome, and the folder it stands on.</returns>
    public async Task<Outcome<string>> OpenRootAsync(
        string path,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(path);

        if (!Directory.Exists(path)) {
            return Outcome<string>.Failure($"There is no folder at '{path}'.");
        }

        // Standing on the same folder again is a re-read, not a new tree: the rows
        // stay and the words in them change. Handing a writer a different set
        // of rows because they pressed F5 is how a tree loses what was open.
        var sameFolder = Root is not null && SameFolder(Root.FullPath, path);

        Root = DirectoryScanner.Open(
            FileNode.Of(NameOf(path), path, isDirectory: true, parent: null), Reading, Say);

        _nodes.Clear();
        _expanded.Clear();
        _expanded.Add(Root.FullPath);

        RebuildItems(keepRows: sameFolder);

        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        Status = $"Showing {NameOf(path)}.";
        OnPropertyChanged(nameof(Items));

        return Outcome<string>.From(path);
    }

    /// <summary>
    /// Opens a row, reading what is inside it the first time.
    /// </summary>
    /// <param name="item">The row to open.</param>
    /// <returns>The outcome: a refusal when it holds nothing to open.</returns>
    public Outcome Expand(FileTreeItem item) {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsDirectory) {
            return Outcome.Failure("A file has nothing inside it.");
        }

        // Opening is one thing, said in one place: the row says it is open, and
        // the tree reads what is inside. Two code paths that both opened rows
        // is two chances for them to disagree about what open means.
        item.IsExpanded = true;
        return Outcome.Ok();
    }

    /// <summary>
    /// A row has opened: reads what is inside it, the first time.
    /// </summary>
    /// <param name="item">The row that opened.</param>
    /// <remarks>
    /// Called by the row itself rather than by the code that asked it to open,
    /// so opening from a key, from a click or from Avalonia's own twisty all
    /// arrive here and none of them can be the one that forgets to read.
    /// </remarks>
    public void Opened(FileTreeItem item) {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsDirectory) {
            return;
        }

        _expanded.Add(item.FullPath);
        if (item.Node.IsLoaded) {
            return;
        }

        var loaded = DirectoryScanner.Open(item.Node, Reading, Say);
        Remember(loaded);

        // The tree is the truth and the rows are its reading, so what was read
        // goes into the tree first and the row is rebuilt from it. A row that
        // knows more than the tree does is a row that lies the moment the tree
        // is read again.
        Root = ReplaceIn(Root, loaded.FullPath, loaded);
        Find(loaded.FullPath)?.Rebuild(loaded);
    }

    /// <summary>
    /// Closes a row.
    /// </summary>
    /// <param name="item">The row to close.</param>
    public void Collapse(FileTreeItem item) {
        ArgumentNullException.ThrowIfNull(item);

        item.IsExpanded = false;
        _expanded.Remove(item.FullPath);
    }

    /// <summary>
    /// Closes every folder but the root, the way a tree closes when a writer
    /// has lost their place in it.
    /// </summary>
    public void CollapseAll() {
        foreach (var row in All()
            .Where(row => row.IsDirectory && !ReferenceEquals(row, RootRow))
            .ToArray()) {
            row.IsExpanded = false;
        }

        _expanded.Clear();
        if (RootRow is not null) {
            _expanded.Add(RootRow.FullPath);
        }

        Status = "Closed every folder.";
    }

    /// <summary>
    /// Whether a row is open.
    /// </summary>
    /// <param name="item">The row to judge.</param>
    /// <returns>True when the writer has it open.</returns>
    public bool IsExpanded(FileTreeItem item) => _expanded.Contains(item.FullPath);

    /// <summary>
    /// Reads the tree again from the disk, keeping the rows that are still
    /// there and the folders that are still open.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>The outcome of re-reading the root.</returns>
    public async Task<Outcome> RefreshAsync(CancellationToken cancellationToken = default) {
        if (Root is null) {
            return Outcome.Failure("There is no folder to read again.");
        }

        // What was open is remembered before the root is read again, because
        // reading it again stands the tree on the same folder and forgets
        // everything under it otherwise.
        var wasOpen = _expanded
            .Where(path => !string.Equals(path, Root.FullPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var outcome = await OpenRootAsync(Root.FullPath, cancellationToken);

        // A folder that was open is open again: re-reading is not an excuse to
        // close everything the writer had open.
        foreach (var path in wasOpen) {
            if (Find(path) is { } row && row.IsDirectory) {
                // Told to open again rather than opened again: a row that
                // survived the re-read is already open, and asking a row to be
                // what it already is asks nothing - while the node under it is
                // new and has not been read.
                Opened(row);
            }
        }

        Status = "Read again.";
        return outcome.IsSuccess
            ? Outcome.Ok()
            : Outcome.Failure(outcome.Error ?? "The folder could not be read again.");
    }


    /// <summary>
    /// The row at a path, if the tree has read one.
    /// </summary>
    /// <param name="path">Where the row is.</param>
    /// <returns>The row, or null.</returns>
    public FileTreeItem? Find(string path) => All().FirstOrDefault(
        item => string.Equals(item.FullPath, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every row on show, in the order they are drawn.
    /// </summary>
    /// <returns>The rows.</returns>
    public IEnumerable<FileTreeItem> All() {
        foreach (var item in Items) {
            yield return item;

            foreach (var child in Descendants(item)) {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Hidden names are read from the disk, not remembered: turning them on is
    /// a new reading of the same folder.
    /// </summary>
    /// <param name="value">Whether they are shown.</param>
    partial void OnShowHiddenChanged(bool value) {
        if (HasRoot) {
            _ = RefreshAsync();
        }
    }

    private void RebuildItems(bool keepRows) {
        if (Root is null) {
            RootRow = null;
            Items.Clear();
            OnPropertyChanged(nameof(RootName));
            return;
        }

        var root = RootRow;
        if (root is null || !keepRows || !SameFolder(root.FullPath, Root.FullPath)) {
            // A different folder, or a first reading of this one: the row is
            // new, and it is open, because a root with nothing under it says
            // nothing about where the writer is.
            root = new FileTreeItem(this, Root, null);
            RootRow = root;
            root.IsExpanded = true;
        } else {
            root.Rebuild(Root);
        }

        Items.Clear();
        Items.Add(root);
        OnPropertyChanged(nameof(RootName));
    }

    /// <summary>
    /// Whether two paths name the same folder, however they are written.
    /// </summary>
    /// <param name="left">One path.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when they are the same folder.</returns>
    private static bool SameFolder(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<FileTreeItem> Descendants(FileTreeItem item) {
        foreach (var child in item.Children) {
            yield return child;

            foreach (var below in Descendants(child)) {
                yield return below;
            }
        }
    }

    private ScanOptions Reading => ScanOptions.Default with { IncludeHidden = ShowHidden };

    /// <summary>
    /// Puts a node in place of the one at that path, rebuilding the folders
    /// above it so every row in the tree sees the same thing.
    /// </summary>
    /// <param name="parent">Where to start looking.</param>
    /// <param name="path">Which path to replace.</param>
    /// <param name="replacement">What replaces it.</param>
    /// <returns>The subtree with the node replaced, or the same subtree when
    /// the path was not in it.</returns>
    private static FileNode? ReplaceIn(FileNode? parent, string path, FileNode replacement) {
        if (parent is null) {
            return null;
        }

        if (string.Equals(parent.FullPath, replacement.FullPath, StringComparison.OrdinalIgnoreCase)) {
            return replacement;
        }

        if (!parent.IsDirectory || !parent.IsLoaded) {
            return parent;
        }

        var changed = false;
        var children = new List<FileNode>(parent.Children.Count);

        foreach (var child in parent.Children) {
            var rebuilt = ReplaceIn(child, path, replacement);
            changed |= !ReferenceEquals(rebuilt, child);
            children.Add(rebuilt ?? child);
        }

        return changed ? parent.Loaded(children) : parent;
    }

    private void Remember(FileNode node) {
        _nodes[node.FullPath] = node;

        foreach (var child in node.Children) {
            Remember(child);
        }
    }

    private void Say(string problem) {
        _say?.Invoke(problem);
        Status = problem;
    }

    private static string NameOf(string path) {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return trimmed.Length == 0 ? path : Path.GetFileName(trimmed);
    }

    /// <summary>
    /// What a folder shows, once it has been sorted.
    /// </summary>
    /// <param name="node">The folder to ask about.</param>
    /// <returns>Its children the writer should see.</returns>
    public IReadOnlyList<FileNode> ChildrenOf(FileNode node) {
        ArgumentNullException.ThrowIfNull(node);

        // Folders first whatever the order is: a writer looking for a file
        // should not have to look past a wall of folders to find it, even in a
        // tree sorted by date.
        IEnumerable<FileNode> ordered = Sort switch {
            TreeSort.Kind => node.Children
                .OrderBy(child => Path.GetExtension(child.Name), StringComparer.OrdinalIgnoreCase)
                .ThenBy(child => child.Name, StringComparer.OrdinalIgnoreCase),
            TreeSort.Date => node.Children.OrderBy(child => child.ModifiedUtc),
            _ => node.Children.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
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
    public Outcome ToggleMark(FileTreeItem item) {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IsMarked) {
            Marked.Remove(item);
            item.IsMarked = false;

            return Outcome.Failure($"'{item.Name}' is no longer marked.");
        }

        Marked.Add(item);
        item.IsMarked = true;

        return Outcome.Ok();
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
    public Outcome SortBy(TreeSort sort) {
        Sort = sort;

        if (Root is not null) {
            RebuildItems(keepRows: true);
        }

        OnPropertyChanged(nameof(Items));
        return Outcome.Ok();
    }

    /// <summary>
    /// Turns the order the other way round.
    /// </summary>
    /// <returns>The outcome.</returns>
    public Outcome TurnOrder() {
        Descending = !Descending;

        if (Root is not null) {
            RebuildItems(keepRows: true);
        }

        OnPropertyChanged(nameof(Items));
        return Outcome.Ok();
    }
}
