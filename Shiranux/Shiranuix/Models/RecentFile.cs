namespace Shiranuix.Models;

/// <summary>
/// A file the user has opened, and when they last opened it.
/// </summary>
/// <remarks>
/// One row per path, so re-opening a file moves it to the front of the list
/// instead of adding a second entry for the same file.
/// </remarks>
public sealed class RecentFile {
    /// <summary>The row's primary key.</summary>
    public int Id { get; set; }

    /// <summary>The file's full path.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>When the file was last opened, in UTC.</summary>
    public DateTime LastOpenedUtc { get; set; }
}
