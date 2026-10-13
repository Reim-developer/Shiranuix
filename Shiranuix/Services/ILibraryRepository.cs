namespace Shiranuix.Services;

using Shiranuix.Models;

/// <summary>
/// Stores the set of folders the user has opened (the "recent" list shown on
/// the home screen).
/// </summary>
/// <remarks>
/// This is the seam where persistence plugs in. The in-memory implementation
/// keeps everything in a list for the current session; a later
/// <c>SqliteLibraryRepository</c> will back the same interface with a
/// database file so the list survives restarts. The UI does not change when
/// that happens.
/// </remarks>
public interface ILibraryRepository {
    /// <summary>Gets the known entries, most recently opened first.</summary>
    IReadOnlyList<LibraryEntry> GetRecent();

    /// <summary>
    /// Records that a folder was just opened, moving it to the top of the
    /// recent list.
    /// </summary>
    /// <param name="entry">The entry to record.</param>
    void RecordOpen(LibraryEntry entry);

    /// <summary>
    /// Removes an entry by its folder.
    /// </summary>
    /// <param name="folder">Folder whose entry should be removed.</param>
    /// <returns><c>true</c> if an entry was removed.</returns>
    bool Remove(string folder);
}
