namespace FileTree;

/// <summary>
/// Reads a folder into nodes, a level at a time.
/// </summary>
/// <remarks>
/// <para>
/// Every refusal here is the tree's own, not an exception: a folder nobody may
/// read is shown as a folder with nothing in it and a line in the plugin's
/// log, because a tree that stops dead at one locked folder is worse than a
/// tree that shows what it can.
/// </para>
/// <para>
/// Folders come before files, then names in order, because that is how a
/// person reads a folder - and because two runs of the same folder should
/// come out in the same order for the test that checks it.
/// </para>
/// </remarks>
public static class DirectoryScanner {
    /// <summary>
    /// Reads one folder.
    /// </summary>
    /// <param name="path">The folder to read.</param>
    /// <param name="options">What to skip and how to order it.</param>
    /// <param name="onProblem">Told about a folder that would not be read.</param>
    /// <param name="parent">The folder these are read for, or null for the root.</param>
    /// <returns>
    /// What is in it, folders first. Empty when the folder is gone, is not a
    /// folder, or would not be read - each of which the caller hears about
    /// through <paramref name="onProblem"/>.
    /// </returns>
    public static IReadOnlyList<FileNode> Scan(
        string path,
        ScanOptions options,
        Action<string>? onProblem = null,
        FileNode? parent = null) {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(path)) {
            onProblem?.Invoke($"There is no folder at '{path}'.");
            return [];
        }

        try {
            var entries = Directory
                .EnumerateFileSystemEntries(path)
                .Take(options.MaxEntries + 1)
                .ToArray();

            if (entries.Length > options.MaxEntries) {
                onProblem?.Invoke(
                    $"'{Path.GetFileName(path)}' holds more than {options.MaxEntries} things, "
                    + "so only the first of them are shown.");
            }

            // The order is the tree's business: the scanner reads, and what it
            // read is in no order at all.
            return entries
                .Take(options.MaxEntries)
                .Select(entry => Node(entry, options, parent))
                .OfType<FileNode>()
                .ToArray();
        } catch (Exception error) when (IsUnreadable(error)) {
            onProblem?.Invoke($"'{Path.GetFileName(path)}' could not be read: {error.Message}");
            return [];
        }
    }

    /// <summary>
    /// Reads what is inside a node, for the node itself.
    /// </summary>
    /// <param name="node">The folder to open.</param>
    /// <param name="options">What to skip and how to order it.</param>
    /// <param name="onProblem">Told about a folder that would not be read.</param>
    /// <returns>
    /// The same node, loaded — which is what a tree puts in place of the one it
    /// held, so its place in the tree and how deep it sits both stay right.
    /// A node that is not a folder comes back untouched rather than thrown
    /// away: nothing was opened, and nothing was lost.
    /// </returns>
    public static FileNode Open(
        FileNode node,
        ScanOptions options,
        Action<string>? onProblem = null) {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(options);

        if (!node.IsDirectory || !Directory.Exists(node.FullPath)) {
            return node;
        }

        return node.Loaded(Scan(node.FullPath, options, onProblem, node));
    }

    /// <summary>
    /// Whether a name is one the tree leaves out unless asked.
    /// </summary>
    /// <param name="name">The name to judge.</param>
    /// <returns>True when the name starts with a dot.</returns>
    public static bool IsHidden(string name) =>
        name.StartsWith('.') && name.Length > 1;

    private static FileNode? Node(string entry, ScanOptions options, FileNode? parent) {
        var name = Path.GetFileName(entry);

        if (string.IsNullOrEmpty(name) || (!options.IncludeHidden && IsHidden(name))) {
            return null;
        }

        var attributes = File.GetAttributes(entry);
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);

        if (isDirectory && !options.FollowLinks && attributes.HasFlag(FileAttributes.ReparsePoint)) {
            // A link to a folder is shown but not walked: following one can
            // walk in a circle, and a circle is not a tree.
            isDirectory = true;
        }

        var modified = File.GetLastWriteTimeUtc(entry);

        return FileNode.Of(name, entry, isDirectory, parent, modified);
    }

    private static bool IsUnreadable(Exception error) =>
        error is UnauthorizedAccessException or IOException or System.Security.SecurityException;
}
