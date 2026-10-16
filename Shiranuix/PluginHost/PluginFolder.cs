namespace Shiranuix.PluginHost;

/// <summary>
/// Where plugins are filed, and how the editor looks for them.
/// </summary>
/// <remarks>
/// One folder under the editor's own data directory, one sub-folder per
/// plugin, each holding that plugin's assembly, whatever else it needs beside
/// it, and its <c>manifest.json</c>. The sub-folder's name is the plugin's own
/// id, which is checked when the manifest is read — a plugin filed under
/// another plugin's name is a mistake worth catching before any code runs.
/// </remarks>
public static class PluginFolder {
    /// <summary>The manifest every plugin folder carries.</summary>
    public const string ManifestName = "manifest.json";

    /// <summary>
    /// The folder plugins are looked for in.
    /// </summary>
    public static string Root { get; } = AppFolder.Plugins;

    /// <summary>
    /// Every folder in <paramref name="root"/> that might hold a plugin,
    /// sorted by name so that the log tells the same story twice.
    /// </summary>
    /// <param name="root">The folder to look in.</param>
    /// <returns>The folders, or an empty list when there are none yet.</returns>
    public static IReadOnlyList<string> Candidates(string root) {
        ArgumentNullException.ThrowIfNull(root);

        if (!Directory.Exists(root)) {
            return [];
        }

        return Directory
            .GetDirectories(root)
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The manifest inside a plugin folder.
    /// </summary>
    /// <param name="directory">The plugin's folder.</param>
    /// <returns>The path to its manifest.</returns>
    public static string ManifestIn(string directory) => Path.Combine(directory, ManifestName);
}
