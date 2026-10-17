using System.Text.Json;

namespace Shiranuix.FileTree.State;

/// <summary>
/// What the tree remembers between one run of the editor and the next: which
/// folder it stood on.
/// </summary>
/// <remarks>
/// One small file in the editor's own folder, beside the store and the log.
/// The folder the tree stands on is the editor's business like every other
/// piece of its state.
///
/// Every failure here is swallowed: a settings file that cannot be read is a
/// tree that opens on the writer's document folder instead, which is a much
/// better afternoon than a tree that refuses to load.
/// </remarks>
internal static class TreeSettings {
    /// <summary>The name of the file the settings live in.</summary>
    public const string FileName = "file-tree.json";

    /// <summary>Where the file lives: in the editor's own folder.</summary>
    public static string FilePath => Path.Combine(AppFolder.Root, FileName);

    /// <summary>
    /// Reads the folder the tree stood on last time.
    /// </summary>
    /// <returns>The folder, or null when there is nothing to remember.</returns>
    public static string? Read() {
        return Read(FilePath);
    }

    /// <summary>
    /// Remembers the folder the tree is standing on.
    /// </summary>
    /// <param name="root">The folder to remember.</param>
    /// <returns>Whether it was written.</returns>
    public static bool Write(string root) {
        try {
            AppFolder.EnsureExists();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Settings(root)));

            return true;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    /// <summary>
    /// Reads one file, and says nothing when there is nothing to read.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The folder it holds, or null.</returns>
    private static string? Read(string path) {
        try {
            if (!File.Exists(path)) {
                return null;
            }

            var saved = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));

            return saved?.Root is { } root && Directory.Exists(root) ? root : null;
        } catch (Exception error) when (error is IOException or JsonException
            or UnauthorizedAccessException) {
            return null;
        }
    }

    private sealed record Settings(string Root);
}
