using System.Text;
using Shiranuix.Models;
using Shiranuix.Services;
using Shiranuix.Utilities;

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
    public ScanResult Scan(string path) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        var text = Contents(path);
        var stats = TextStatistics.Measure(text);

        return new ScanResult(
            stats.Words,
            stats.Characters,
            stats.Lines,
            Newline.Detect(text));
    }

    /// <inheritdoc />
    public string ReadRange(string path, int from, int to) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        var text = Contents(path);
        var start = Math.Clamp(from, 0, text.Length);
        var end = Math.Clamp(to, start, text.Length);

        return text[start..end];
    }

    /// <inheritdoc />
    public void WriteCombined(
        string targetPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        Newline newline) {
        if (FailWritesWith is not null) {
            throw FailWritesWith;
        }

        var source = sourcePath is null ? string.Empty : Contents(sourcePath);
        var text = new StringBuilder();
        var cursor = 0;

        foreach (var replacement in replacements) {
            text.Append(source, cursor, replacement.Start - cursor);
            text.Append(replacement.Text);
            cursor = replacement.Start + replacement.Length;
        }

        text.Append(source, cursor, source.Length - cursor);

        Files[targetPath] = newline.Apply(text.ToString());
        Writes.Add(targetPath);
    }

    /// <inheritdoc />
    public bool Exists(string path) => Files.ContainsKey(path);

    /// <summary>
    /// Reads a file off the virtual disk, or reports it missing the way a
    /// real one would.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The file's text.</returns>
    private string Contents(string path) =>
        Files.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException("Not in the fake disk.", path);
}
