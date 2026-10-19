using System.Text;
using Shiranuix.CoreFormat;
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
    /// How each file on the fake disk is set: the typeface and point size a
    /// <c>.shiranux</c> carries. A plain file carries nothing, so it is never
    /// in here — the same rule the real service follows.
    /// </summary>
    public Dictionary<string, DocumentTypography> Typography { get; } =
        new(StringComparer.OrdinalIgnoreCase);

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
    /// <remarks>
    /// The fake's characters stand in for bytes one for one — an anchor's
    /// two numbers always agree here — so the walks need no encoding, only
    /// the line arithmetic the real ones verify byte by byte.
    /// </remarks>
    public string ReadFrom(string path, LineAnchor start, int chars) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        var text = Contents(path);
        var from = Math.Clamp(start.Offset, 0, text.Length);
        var count = Math.Clamp(chars, 0, text.Length - from);

        return text[from..(from + count)];
    }

    /// <inheritdoc />
    public LineRead ReadLines(string path, LineAnchor start, int lines) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        var text = Contents(path);
        var from = Math.Clamp(start.Offset, 0, text.Length);

        var found = 0;
        var index = from;

        while (found < lines && index < text.Length) {
            if (text[index] == '\n') {
                found++;
            }

            index++;
        }

        var end = index;
        var body = text[from..end];

        // The window holds no newline that ends its last line — the same
        // trim the real service makes.
        if (body.Length > 0 && body[^1] == '\n') {
            body = body[..^1];
        }

        return new LineRead(
            body,
            new LineMove(new LineAnchor(end, end), found),
            found >= lines && end < text.Length);
    }

    /// <inheritdoc />
    public LineMove MoveLines(string path, LineAnchor from, int lines) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        var text = Contents(path);
        var position = Math.Clamp(from.Offset, 0, text.Length);

        if (lines > 0) {
            var found = 0;

            for (var index = position; found < lines && index < text.Length; index++) {
                if (text[index] == '\n') {
                    found++;
                    position = index + 1;
                }
            }

            return new LineMove(new LineAnchor(position, position), found);
        }

        var moved = 0;
        var current = position;

        while (moved < -lines && current > 0) {
            var search = current - 2;
            var found = search >= 0 ? text.LastIndexOf('\n', search) : -1;

            // With no break before this line, the one it steps onto is the
            // file's first — which is still a line gone by.
            current = found >= 0 ? found + 1 : 0;
            moved++;
        }

        return new LineMove(new LineAnchor(current, current), -moved);
    }

    /// <inheritdoc />
    public LineMove LineStartOf(string path, LineAnchor near, int offset) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        var text = Contents(path);
        var at = Math.Clamp(offset, 0, text.Length);
        var anchor = Math.Clamp(near.Offset, 0, text.Length);

        // The line holding the character begins after the last break below it.
        var search = at - 1;
        var found = search >= 0 ? text.LastIndexOf('\n', search) : -1;
        var start = found >= 0 ? found + 1 : 0;

        var below = 0;

        for (var index = start; index < anchor; index++) {
            if (text[index] == '\n') {
                below++;
            }
        }

        var above = 0;

        for (var index = anchor; index < start; index++) {
            if (text[index] == '\n') {
                above++;
            }
        }

        return new LineMove(
            new LineAnchor(start, start),
            start < anchor ? -below : above);
    }

    /// <inheritdoc />
    public ScanResult WriteCombined(
        string targetPath,
        string? sourcePath,
        IReadOnlyList<TextReplacement> replacements,
        Newline newline,
        DocumentTypography? typography = null) {
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

        var written = newline.Apply(text.ToString());
        Files[targetPath] = written;
        Writes.Add(targetPath);

        // Only a file that can hold a setting holds one: a .txt target takes
        // the letters and leaves the face standing where it was, which in
        // practice means it drops it. A .shiranux that is handed no face
        // keeps the one the file it was written through already carried —
        // the same rule the real service follows.
        if (Utf8File.IsShiranux(targetPath)) {
            if (typography is not null) {
                Typography[targetPath] = typography;
            } else if (sourcePath is not null && Typography.TryGetValue(sourcePath, out var carried)) {
                Typography[targetPath] = carried;
            } else {
                Typography.Remove(targetPath);
            }
        } else {
            Typography.Remove(targetPath);
        }

        // What the real service hands back: the counts of the file it just
        // wrote, taken from the same pass.
        var stats = TextStatistics.Measure(written);

        return new ScanResult(
            stats.Words,
            stats.Characters,
            stats.Lines,
            Newline.Detect(written));
    }

    /// <inheritdoc />
    public DocumentTypography? ReadTypography(string path) {
        if (FailReadsWith is not null) {
            throw FailReadsWith;
        }

        if (!Utf8File.IsShiranux(path)) {
            return null;
        }

        // A path with nothing behind it is a missing file, however the
        // question was asked — the same answer the reads give.
        _ = Contents(path);
        return Typography.GetValueOrDefault(path);
    }

    /// <inheritdoc />
    public string ReadCombined(string? sourcePath, IReadOnlyList<TextReplacement> replacements) {
        var source = sourcePath is null ? string.Empty : Contents(sourcePath);
        var text = new StringBuilder();
        var cursor = 0;

        foreach (var replacement in replacements) {
            if (replacement.Start < cursor) {
                throw new InvalidDataException(
                    "Replacements must come in the order they fall in the file.");
            }

            text.Append(source, cursor, replacement.Start - cursor);
            text.Append(replacement.Text);
            cursor = replacement.Start + replacement.Length;
        }

        text.Append(source, cursor, source.Length - cursor);

        return text.ToString();
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
