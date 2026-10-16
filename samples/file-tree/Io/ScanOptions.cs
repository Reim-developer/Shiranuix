namespace FileTree.Io;

/// <summary>
/// How a folder is read: what is skipped, how it is ordered, and how much of
/// it is read at once.
/// </summary>
/// <param name="IncludeHidden">
/// Whether names that start with a dot are shown. Off by default: a writer
/// browsing their own work does not want a folder of build output.
/// </param>
/// <param name="FollowLinks">
/// Whether a symbolic link to a folder is walked as one. Off by default, and
/// not only for tidiness: following one can walk in a circle.
/// </param>
/// <param name="MaxEntries">
/// How many entries one folder will be read to. A folder with a hundred
/// thousand files in it is a mistake, and a tree that tries to draw it is a
/// worse one.
/// </param>
public sealed record ScanOptions(
    bool IncludeHidden = false,
    bool FollowLinks = false,
    int MaxEntries = 5000) {
    /// <summary>The rules a plain writer gets.</summary>
    public static ScanOptions Default { get; } = new();
}
