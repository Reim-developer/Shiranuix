using Shiranuix.Services;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory file service, so tests never touch the disk.
/// </summary>
public sealed class FakeFileService : IFileService {
    /// <summary>
    /// The virtual disk: path to contents.
    /// </summary>
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Paths written during the test, in order.
    /// </summary>
    public List<string> Writes { get; } = new();

    /// <summary>
    /// When set, every write throws this. Models a locked or read-only file.
    /// </summary>
    public Exception? FailWritesWith { get; set; }

    /// <summary>
    /// When set, every read throws this. Models a missing or corrupt file.
    /// </summary>
    public Exception? FailReadsWith { get; set; }

    /// <inheritdoc />
    public string ReadAllText(string path) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        return Files.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException("Not in the fake disk.", path);
    }

    /// <inheritdoc />
    public void WriteAllText(string path, string text) {
        if (FailWritesWith is not null) {
            throw FailWritesWith;
        }

        Files[path] = text;
        Writes.Add(path);
    }

    /// <inheritdoc />
    public bool Exists(string path) => Files.ContainsKey(path);
}
