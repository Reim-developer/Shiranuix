using System.Text.Json;
using System.Text.Json.Serialization;
using Shiranuix.Plugins;

namespace Shiranuix.PluginHost;

/// <summary>
/// Reads a plugin's <c>manifest.json</c> and says what is wrong with it.
/// </summary>
/// <remarks>
/// Read before anything is loaded, on purpose: the manifest is how the editor
/// knows which assembly to open, which type inside it is the entry point, and
/// whether that type was built for the contract this editor speaks. Every
/// problem is collected and reported together, so a plugin author fixing a
/// manifest is not walked through it one error per run.
/// <para>
/// The JSON shape is the one <see cref="PluginManifest"/> describes, and the
/// record below is its shadow: fields arrive as they were written, all of
/// them optional at this point, because "you left it out" is a sentence this
/// class knows how to say rather than an exception from the deserializer.
/// </para>
/// </remarks>
public static class PluginManifestReader {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Reads the manifest in a plugin folder.
    /// </summary>
    /// <param name="directory">The plugin's folder.</param>
    /// <returns>
    /// The manifest, or the reasons there is none. Never throws: a manifest
    /// that cannot be read is a plugin that does not load, which is a line in
    /// the log and not a broken editor.
    /// </returns>
    public static PluginManifestRead Read(string directory) {
        if (directory is null) {
            throw new ArgumentNullException(nameof(directory));
        }

        var path = PluginFolder.ManifestIn(directory);

        if (!File.Exists(path)) {
            return PluginManifestRead.Failed(
                $"{PluginFolder.ManifestName} is missing from '{Path.GetFileName(directory)}'.");
        }

        ManifestFile? file;
        try {
            file = JsonSerializer.Deserialize<ManifestFile>(File.ReadAllText(path), Options);
        } catch (JsonException error) {
            return PluginManifestRead.Failed(
                $"{PluginFolder.ManifestName} could not be read: {error.Message}");
        }

        if (file is null) {
            return PluginManifestRead.Failed($"{PluginFolder.ManifestName} is empty.");
        }

        return Read(directory, file);
    }

    /// <summary>
    /// Turns what the file holds into a manifest, and says what is missing or
    /// wrong with it.
    /// </summary>
    /// <param name="directory">The plugin's folder.</param>
    /// <param name="file">What the manifest file held.</param>
    /// <returns>The manifest, or the reasons there is none.</returns>
    internal static PluginManifestRead Read(string directory, ManifestFile file) {
        var problems = new List<string>();

        if (!Version.TryParse(file.Version, out var version)) {
            problems.Add($"'{file.Version ?? "(nothing)"}' is not a version.");
            version = new Version(0, 0, 0);
        }

        PluginApiRange? api = null;
        if (string.IsNullOrWhiteSpace(file.Api)) {
            problems.Add(
                "It says nothing about which contract versions it works with "
                + "— 'api' should be something like \"0.1\".");
        } else {
            try {
                api = PluginApiRange.Parse(file.Api);
            } catch (ArgumentException) {
                problems.Add($"'{file.Api}' is not a range of contract versions.");
            }
        }

        var manifest = new PluginManifest(
            Id: file.Id ?? string.Empty,
            Name: file.Name ?? string.Empty,
            Version: version,
            Api: api ?? PluginApiRange.Any,
            EntryPoint: file.EntryPoint ?? string.Empty,
            Description: file.Description,
            Author: file.Author);

        problems.AddRange(manifest.Validate());

        var folder = Path.GetFileName(directory);
        if (PluginId.IsValid(manifest.Id) &&
            !string.Equals(folder, manifest.Id, StringComparison.Ordinal)) {
            problems.Add(
                $"It is filed under '{folder}' but calls itself '{manifest.Id}'; "
                + "a plugin's folder is named after its id.");
        }

        return problems.Count == 0
            ? new PluginManifestRead(manifest, [])
            : PluginManifestRead.Failed([.. problems]);
    }

    /// <summary>
    /// One manifest as the file holds it: every field may be missing, because
    /// every field can be missing.
    /// </summary>
    /// <param name="Id">The plugin's id.</param>
    /// <param name="Name">The name a person reads.</param>
    /// <param name="Version">The version the plugin declares.</param>
    /// <param name="Api">The contract versions it works with.</param>
    /// <param name="EntryPoint">The type implementing IPlugin.</param>
    /// <param name="Description">What the plugin does, in a line.</param>
    /// <param name="Author">Who wrote it.</param>
    internal sealed record ManifestFile(
        string? Id,
        string? Name,
        string? Version,
        string? Api,
        string? EntryPoint,
        string? Description,
        string? Author);
}

/// <summary>
/// What reading a manifest came to: the manifest, or every reason there is
/// not one.
/// </summary>
/// <param name="Manifest">The manifest, or null when there is none.</param>
/// <param name="Problems">Why there is none, in sentences worth reading.</param>
public sealed record PluginManifestRead(PluginManifest? Manifest, IReadOnlyList<string> Problems) {
    /// <summary>Whether a manifest came out of it.</summary>
    public bool Succeeded => Manifest is not null;

    /// <summary>
    /// A manifest that is not there, and why.
    /// </summary>
    /// <param name="problems">Why, in sentences worth reading.</param>
    /// <returns>The reading.</returns>
    public static PluginManifestRead Failed(IReadOnlyList<string> problems) =>
        new(null, problems);

    /// <summary>
    /// A manifest that is not there, for one reason.
    /// </summary>
    /// <param name="problem">Why, in a sentence worth reading.</param>
    /// <returns>The reading.</returns>
    public static PluginManifestRead Failed(string problem) => new(null, [problem]);
}
