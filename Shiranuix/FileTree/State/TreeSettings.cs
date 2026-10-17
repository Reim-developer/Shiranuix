using System.Text.Json;

namespace Shiranuix.FileTree.State;

/// <summary>
/// What the tree remembers between one run of the editor and the next: which
/// folder it stood on.
/// </summary>
/// <remarks>
/// One small file in the editor's own folder, beside the store and the log.
/// It used to live beside the tree's assembly because the tree was something
/// a writer installed; the tree is the editor's now, so the folder it stands
/// on is the editor's business like every other piece of its state.
///
/// A folder that is remembered from before the move is carried across on the
/// first read - a writer who had the tree on their documents folder should not
/// find it on nothing the first time they open the editor after the change.
///
/// Every failure here is swallowed: a settings file that cannot be read is a
/// tree that opens on the writer's document folder instead, which is a much
/// better afternoon than a tree that refuses to load.
/// </remarks>
internal static class TreeSettings {
    /// <summary>The name of the file the settings live in.</summary>
    public const string FileName = "file-tree.json";

    /// <summary>Where the file lives now: in the editor's own folder.</summary>
    public static string FilePath => Path.Combine(AppFolder.Root, FileName);

    /// <summary>
    /// Where it lived when the tree was a plugin, and a folder a writer might
    /// still have it in.
    /// </summary>
    private static string OldFilePath => Path.Combine(AppFolder.Plugins, "file-tree", FileName);

    /// <summary>
    /// Reads the folder the tree stood on last time, carrying one across from
    /// the plugin days if that is where it is.
    /// </summary>
    /// <returns>The folder, or null when there is nothing to remember.</returns>
    public static string? Read() {
        var remembered = Read(FilePath) ?? Read(OldFilePath);
        if (remembered is not null) {
            Write(remembered);
        }

        return remembered;
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
