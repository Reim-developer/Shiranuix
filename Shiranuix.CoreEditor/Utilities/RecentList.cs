using System.Collections.ObjectModel;

namespace Shiranuix.CoreEditor;

/// <summary>
/// A list that keeps the N most recently used items, newest first.
/// </summary>
/// <remarks>
/// Re-using an item moves it to the front rather than adding a second entry,
/// and the list cannot outgrow the room the interface has for it.
///
/// Holds the recent-files list in the view model. It is generic because the
/// rule itself is not about files: anything a writer reaches for again —
/// a typeface, a size, a folder — wants the same "latest first, capped"
/// behaviour, and the limit is the caller's to name.
/// </remarks>
public sealed class RecentList<T> : ObservableCollection<T>
    where T : notnull {
    private readonly int _limit;

    /// <summary>
    /// Creates a list that keeps at most <paramref name="limit"/> items.
    /// </summary>
    /// <param name="limit">The most items the list will hold.</param>
    public RecentList(int limit) {
        _limit = limit;
    }

    /// <summary>
    /// Moves an item to the front, adding it when it is not there yet.
    /// </summary>
    /// <param name="item">The item to move to the front.</param>
    /// <remarks>
    /// Anything past the limit drops off the end, so the list stays the size
    /// it was made to be.
    /// </remarks>
    public void MoveToFront(T item) {
        var index = IndexOf(item);
        if (index >= 0) {
            RemoveAt(index);
        }

        Insert(0, item);

        while (Count > _limit) {
            RemoveAt(Count - 1);
        }
    }
}
