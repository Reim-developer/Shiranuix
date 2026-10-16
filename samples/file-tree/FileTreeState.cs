using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Shiranuix.Plugins;

namespace FileTree;

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

    /// <summary>The tree's rows, or none before a root is chosen.</summary>
    public ObservableCollection<FileTreeItem> Items { get; } = [];

    /// <summary>Where the tree stands, or null before a folder has been chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoot))]
    private FileNode? _root;

    /// <summary>Whether there is a folder to show at all.</summary>
    public bool HasRoot => Root is not null;

    /// <summary>What the writer has typed into the tree's box.</summary>
    [ObservableProperty]
    private string _filter = string.Empty;

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
    public async Task<PluginOutcome<string>> OpenRootAsync(
        string path,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(path);

        if (!Directory.Exists(path)) {
            return PluginOutcome<string>.Failure($"There is no folder at '{path}'.");
        }

        Root = DirectoryScanner.Open(
            FileNode.Of(NameOf(path), path, isDirectory: true, parent: null), Reading, Say);

        _nodes.Clear();
        _expanded.Clear();
        _expanded.Add(Root.FullPath);

        RebuildItems(keepRows: false);

        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        Status = $"Showing {NameOf(path)}.";
        OnPropertyChanged(nameof(Items));

        return PluginOutcome<string>.From(path);
    }

    /// <summary>
    /// Opens a row, reading what is inside it the first time.
    /// </summary>
    /// <param name="item">The row to open.</param>
    /// <returns>The outcome: a refusal when it holds nothing to open.</returns>
    public PluginOutcome Expand(FileTreeItem item) {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsDirectory) {
            return PluginOutcome.Failure("A file has nothing inside it.");
        }

        var loaded = DirectoryScanner.Open(item.Node, Reading, Say);
        Remember(loaded);

        // The tree is the truth and the rows are its reading, so what was read
        // goes into the tree first and the rows are rebuilt from it. A row that
        // knows more than the tree does is a row that lies the moment the tree
        // is read again.
        Root = ReplaceIn(Root, loaded.FullPath, loaded);
        RebuildItems(keepRows: true);

        var row = Find(loaded.FullPath);
        if (row is not null) {
            row.IsExpanded = true;
        }

        _expanded.Add(loaded.FullPath);
        return PluginOutcome.Ok();
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
    public async Task<PluginOutcome> RefreshAsync(CancellationToken cancellationToken = default) {
        if (Root is null) {
            return PluginOutcome.Failure("There is no folder to read again.");
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
                Expand(row);
            }
        }

        Status = "Read again.";
        return outcome.IsSuccess
            ? PluginOutcome.Ok()
            : PluginOutcome.Failure(outcome.Error ?? "The folder could not be read again.");
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
    /// Re-filters every row, without touching the disk.
    /// </summary>
    /// <param name="value">What the writer has typed.</param>
    partial void OnFilterChanged(string value) {
        RebuildItems(keepRows: true);
        OnPropertyChanged(nameof(Items));
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
            return;
        }

        var wanted = ChildrenOf(Root);
        var existing = Items.ToArray();

        Items.Clear();
        foreach (var child in wanted) {
            var row = keepRows
                ? existing.FirstOrDefault(item =>
                    string.Equals(item.FullPath, child.FullPath, StringComparison.OrdinalIgnoreCase))
                : null;

            if (row is null) {
                Items.Add(new FileTreeItem(this, child, null));
            } else {
                row.Rebuild(child);
                Items.Add(row);
            }
        }
    }

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
}
