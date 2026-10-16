using System.Text.Json;
using Shiranuix.Plugins;

namespace FileTree;

/// <summary>
/// What the tree remembers between one run of the editor and the next.
/// </summary>
/// <remarks>
/// A plugin keeps its own settings - that was the deal from the start, and it
/// is why the editor has no table to grow. One small JSON file beside the
/// plugin, written when something actually changes and read once at startup.
/// Every failure here is swallowed: a settings file that cannot be read is a
/// tree that opens on the writer's document folder instead, which is a much
/// better afternoon than a plugin that refuses to load.
/// </remarks>
internal static class TreeSettings {
    /// <summary>The file the settings live in, beside the plugin's assembly.</summary>
    public static string FilePath(string pluginDirectory) =>
        Path.Combine(pluginDirectory, "file-tree.json");

    /// <summary>
    /// Reads the folder the tree stood on last time.
    /// </summary>
    /// <param name="pluginDirectory">Where the plugin's own files are.</param>
    /// <returns>The folder, or null when there is nothing to remember.</returns>
    public static string? Read(string pluginDirectory) {
        try {
            var path = FilePath(pluginDirectory);
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

    /// <summary>
    /// Remembers the folder the tree is standing on.
    /// </summary>
    /// <param name="pluginDirectory">Where the plugin's own files are.</param>
    /// <param name="root">The folder to remember.</param>
    /// <returns>Whether it was written.</returns>
    public static bool Write(string pluginDirectory, string root) {
        try {
            Directory.CreateDirectory(pluginDirectory);
            File.WriteAllText(
                FilePath(pluginDirectory),
                JsonSerializer.Serialize(new Settings(root)));

            return true;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    private sealed record Settings(string Root);
}
