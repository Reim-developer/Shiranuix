namespace Shiranuix.Services;

using Shiranuix.Models;

/// <summary>
/// <see cref="ILibraryRepository"/> that keeps entries in memory only.
/// </summary>
/// <remarks>
/// Entries are de-duplicated by folder: opening the same folder again moves
/// it to the top and refreshes its page count rather than adding a duplicate.
/// The list is capped at <see cref="MaxEntries"/> so the home screen cannot
/// grow unbounded.
/// </remarks>
public sealed class InMemoryLibraryRepository : ILibraryRepository {
    /// <summary>Maximum number of remembered entries.</summary>
    public const int MaxEntries = 24;

    private readonly List<LibraryEntry> _entries = new();
    private readonly object _gate = new();

    /// <inheritdoc />
    public IReadOnlyList<LibraryEntry> GetRecent() {
        lock (_gate) {
            return _entries.ToArray();
        }
    }

    /// <inheritdoc />
    public void RecordOpen(LibraryEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate) {
            // Remove any previous entry for the same folder, then re-insert at
            // the front so the most recently opened folder is always first.
            _entries.RemoveAll(e => string.Equals(
                e.Folder, entry.Folder, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, entry);

            // Trim the tail if we exceeded the cap.
            if (_entries.Count > MaxEntries) {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }
        }
    }

    /// <inheritdoc />
    public bool Remove(string folder) {
        if (folder is null) {
            return false;
        }

        lock (_gate) {
            return _entries.RemoveAll(e => string.Equals(
                e.Folder, folder, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }
}
