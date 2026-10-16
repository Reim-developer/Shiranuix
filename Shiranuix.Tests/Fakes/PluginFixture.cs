namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A plugin folder built for one test, and taken away with it.
/// </summary>
/// <remarks>
/// The loader is the one part of the editor that reads somebody else's files,
/// so the only honest way to test it is with real ones: a real folder, a real
/// manifest and the sample plugin's real assembly, copied out of the build
/// output into somewhere temporary. Everything here is written with LF, and the
/// folder is deleted on the way out whatever the test did to it.
/// </remarks>
internal sealed class PluginFixture : IDisposable {
    /// <summary>
    /// Creates an empty plugins folder.
    /// </summary>
    public PluginFixture() {
        Root = Path.Combine(
            Path.GetTempPath(), "shiranuix-plugin-tests", Guid.NewGuid().ToString("n"));

        Directory.CreateDirectory(Root);
    }

    /// <summary>The folder plugins are looked for in.</summary>
    public string Root { get; }

    /// <summary>
    /// Files the sample plugin as it ships: its assembly beside its manifest,
    /// under its own name.
    /// </summary>
    /// <returns>The plugin's folder.</returns>
    public string InstallSample() {
        var folder = Folder("reading-time");

        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "reading-time.dll"),
            Path.Combine(folder, "reading-time.dll"),
            overwrite: true);

        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "manifest.json"),
            Path.Combine(folder, "manifest.json"),
            overwrite: true);

        return folder;
    }

    /// <summary>
    /// Files a plugin folder holding a manifest the test wrote itself.
    /// </summary>
    /// <param name="folderName">What the folder is called.</param>
    /// <param name="id">What the manifest calls the plugin.</param>
    /// <param name="api">
    /// The contract versions it claims, or null for the one this editor speaks.
    /// </param>
    /// <param name="entryPoint">
    /// The type it names, or null for the sample's own.
    /// </param>
    /// <param name="version">The version it declares.</param>
    /// <param name="withSampleAssembly">
    /// Whether to give it the sample's assembly under this plugin's own name,
    /// which is what a plugin folder that got everything right looks like.
    /// </param>
    /// <returns>The plugin's folder.</returns>
    public string WriteManifest(
        string folderName,
        string id,
        string? api = null,
        string? entryPoint = null,
        string version = "1.0.0",
        bool withSampleAssembly = false) {
        var folder = Folder(folderName);

        File.WriteAllText(Path.Combine(folder, "manifest.json"), Manifest(
            id, api ?? "0.1", entryPoint ?? "ReadingTime.ReadingTimePlugin", version));

        if (withSampleAssembly) {
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "reading-time.dll"),
                Path.Combine(folder, $"{id}.dll"),
                overwrite: true);
        }

        return folder;
    }

    /// <summary>
    /// Files a folder with nothing in it but an assembly, which is a folder
    /// that forgot its manifest.
    /// </summary>
    /// <param name="folderName">What the folder is called.</param>
    /// <returns>The plugin's folder.</returns>
    public string FolderWithoutManifest(string folderName) {
        var folder = Folder(folderName);

        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "reading-time.dll"),
            Path.Combine(folder, "reading-time.dll"),
            overwrite: true);

        return folder;
    }

    /// <summary>
    /// Writes a manifest as the editor would find it, in its own line endings.
    /// </summary>
    /// <param name="id">The plugin's id.</param>
    /// <param name="api">The contract versions it works with.</param>
    /// <param name="entryPoint">The type implementing IPlugin.</param>
    /// <param name="version">The version it declares.</param>
    /// <returns>The manifest, as text.</returns>
    private static string Manifest(string id, string api, string entryPoint, string version) =>
        string.Join("\n", [
            "{",
            $"  \"id\": \"{id}\",",
            "  \"name\": \"A test plugin\",",
            $"  \"version\": \"{version}\",",
            $"  \"api\": \"{api}\",",
            $"  \"entryPoint\": \"{entryPoint}\"",
            "}"
        ]);

    /// <summary>
    /// Creates an empty folder inside the plugins folder.
    /// </summary>
    /// <param name="name">What the folder is called.</param>
    /// <returns>The folder's path.</returns>
    public string Folder(string name) {
        var folder = Path.Combine(Root, name);
        Directory.CreateDirectory(folder);

        return folder;
    }

    /// <summary>
    /// Takes the folder away when it can, and leaves it when it cannot.
    /// </summary>
    /// <remarks>
    /// A plugin that was loaded holds its assembly open, and Windows will not
    /// delete a file somebody has open. The folder is under the temporary
    /// directory and goes with it eventually; failing a test over the tidying
    /// up would say nothing at all about the loader.
    /// </remarks>
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
