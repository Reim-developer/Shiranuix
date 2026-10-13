using Shiranuix.Services;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory font history, so tests never touch the database.
/// </summary>
public sealed class FakeFontHistoryStore : IFontHistoryStore {
    /// <summary>
    /// The recorded picks, newest first. A pick is a typeface at a point size,
    /// which is the pairing the real store keeps one row per.
    /// </summary>
    public List<(string Family, double Size)> Uses { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> RecentFamilies() =>
        Uses.Select(use => use.Family).Distinct().ToList();

    /// <inheritdoc />
    public IReadOnlyList<double> RecentSizes() =>
        Uses.Select(use => use.Size).Distinct().ToList();

    /// <inheritdoc />
    public void Remember(string family, double size) => Uses.Insert(0, (family, size));
}
