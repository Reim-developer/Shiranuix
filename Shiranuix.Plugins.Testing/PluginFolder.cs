namespace Shiranuix.Plugins.Testing;

/// <summary>
/// A plugin folder a test can put an assembly into and load, the way the
/// editor finds one on a writer's machine.
/// </summary>
/// <remarks>
/// The editor reads a manifest, then an assembly named after the id in it, and
/// that arrangement is worth testing rather than assuming: a plugin whose
/// manifest and folder disagree is refused before any of its code runs, and
/// this is how a plugin author finds that out without launching the editor.
/// </remarks>
public sealed class PluginFolder : IDisposable {
    /// <summary>The manifest every plugin folder carries.</summary>
    public const string ManifestName = "manifest.json";

    /// <summary>
    /// Creates an empty plugins folder.
    /// </summary>
    public PluginFolder() {
        Root = Path.Combine(Path.GetTempPath(), "shiranuix-plugins", Guid.NewGuid().ToString("n"));

        Directory.CreateDirectory(Root);
    }

    /// <summary>The folder plugins are looked for in.</summary>
    public string Root { get; }

    /// <summary>
    /// Writes a manifest, and the assembly beside it.
    /// </summary>
    /// <param name="id">The plugin's id, which names the folder and the assembly.</param>
    /// <param name="entryPoint">The type implementing IPlugin.</param>
    /// <param name="api">The contract versions it works with.</param>
    /// <param name="assembly">
    /// The assembly to copy in; the plugin's own output by default.
    /// </param>
    /// <param name="folderName">What the folder is called, when it is not the id.</param>
    /// <returns>The plugin's folder.</returns>
    public string Install(
        string id,
        string entryPoint,
        string api = "0.1",
        string? assembly = null,
        string? folderName = null) {
        var folder = Directory.CreateDirectory(Path.Combine(
            Root, folderName ?? id)).FullName;

        WriteManifest(folder, id, entryPoint, api);

        var source = assembly ?? Path.Combine(AppContext.BaseDirectory, $"{id}.dll");
        File.Copy(source, Path.Combine(folder, $"{id}.dll"), overwrite: true);

        return folder;
    }

    /// <summary>
    /// Writes a manifest on its own, for a folder that is wrong in one way.
    /// </summary>
    /// <param name="folderName">What the folder is called.</param>
    /// <param name="id">What the manifest calls the plugin.</param>
    /// <param name="entryPoint">The type it names.</param>
    /// <param name="api">The contract versions it works with.</param>
    /// <returns>The plugin's folder.</returns>
    public string WriteManifest(string folderName, string id, string entryPoint, string api) {
        var folder = Directory.CreateDirectory(Path.Combine(Root, folderName)).FullName;

        File.WriteAllText(
            Path.Combine(folder, ManifestName),
            string.Join("\n", [
                "{",
                $"  \"id\": \"{id}\",",
                $"  \"name\": \"{id}\",",
                "  \"version\": \"1.0.0\",",
                $"  \"api\": \"{api}\",",
                $"  \"entryPoint\": \"{entryPoint}\"",
                "}"
            ]));

        return folder;
    }

    /// <summary>
    /// Takes the folder away, when nothing is holding a file in it open.
    /// </summary>
    public void Dispose() {
        try {
            if (Directory.Exists(Root)) {
                Directory.Delete(Root, recursive: true);
            }
        } catch (IOException) {
        } catch (UnauthorizedAccessException) {
        }
    }
}
