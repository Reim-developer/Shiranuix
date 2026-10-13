namespace Shiranuix.ViewModels;

/// <summary>
/// The point sizes offered by the size picker: a fixed ladder that also makes
/// room for the size in use.
/// </summary>
/// <remarks>
/// A list rather than a fixed array because the size in use is kept in it. A
/// size outside the usual ladder — 17, which is what a document was set in —
/// would otherwise have no entry to select, and the picker would read blank
/// instead of the document's real size. Word does the same.
/// </remarks>
public sealed class FontSizeLadder {
    private readonly List<double> _sizes;

    /// <summary>
    /// Creates the ladder with the usual run of point sizes.
    /// </summary>
    public FontSizeLadder() {
        _sizes = new() {
            8d, 9d, 10d, 11d, 12d, 14d, 16d, 17d, 18d, 20d,
            22d, 24d, 28d, 32d, 36d, 48d, 72d
        };
    }

    /// <summary>
    /// The sizes, in order.
    /// </summary>
    public IReadOnlyList<double> Sizes => _sizes;

    /// <summary>
    /// Inserts a size into the ladder in order if it is not already there.
    /// </summary>
    /// <param name="value">The size to make room for.</param>
    public void Ensure(double value) {
        var index = _sizes.IndexOf(value);
        if (index < 0) {
            index = _sizes.BinarySearch(value);
            if (index < 0) {
                index = ~index;
            }

            _sizes.Insert(index, value);
        }
    }
}
