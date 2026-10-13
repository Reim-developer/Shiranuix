using Shiranuix.Services;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory recent-files store, so tests never touch the database.
/// </summary>
public sealed class FakeRecentFilesStore : IRecentFilesStore {
    /// <summary>
    /// What <see cref="Load"/> returns: newest first, the order the store
    /// hands them back in.
    /// </summary>
    public List<string> Stored { get; } = new();

    /// <summary>Every <see cref="Remember"/>, in order.</summary>
    public List<string> Remembered { get; } = new();

    /// <inheritdoc />
    public IReadOnlyList<string> Load() => Stored;

    /// <inheritdoc />
    public void Remember(string path) => Remembered.Add(path);
}
