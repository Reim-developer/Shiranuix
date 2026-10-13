namespace Shiranuix.Services;

/// <summary>
/// The typefaces and sizes the user has picked lately.
/// </summary>
/// <remarks>
/// Kept behind an interface for the same reason as
/// <see cref="IFileService"/>: both pickers are filled from it, and a headless
/// test needs an in-memory one.
/// </remarks>
public interface IFontHistoryStore {
    /// <summary>
    /// The typefaces picked lately.
    /// </summary>
    /// <returns>The family names, most recent first and without repeats.</returns>
    IReadOnlyList<string> RecentFamilies();

    /// <summary>
    /// The point sizes picked lately.
    /// </summary>
    /// <returns>The sizes, most recent first and without repeats.</returns>
    IReadOnlyList<double> RecentSizes();

    /// <summary>
    /// Records that a typeface at a point size was picked.
    /// </summary>
    /// <param name="family">The typeface.</param>
    /// <param name="size">The point size.</param>
    void Remember(string family, double size);
}
