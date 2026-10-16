namespace Shiranuix.Plugins;

/// <summary>
/// What a plugin says about itself in <c>manifest.json</c>, beside its assembly.
/// </summary>
/// <remarks>
/// <para>
/// The manifest rather than attributes on the class, because the host has to
/// read something before it loads anything: which assembly to load, which type
/// in it is the entry point, and whether that type was built for the contract
/// this host speaks. All three are answerable from a file, and none of them are
/// answerable from an assembly that has not been loaded yet — which matters
/// when loading is exactly what went wrong.
/// </para>
/// <para>
/// <c>Validate</c> collects every problem rather than stopping at the first,
/// so a plugin author fixing a manifest is not walked through it one error per
/// run. The host refuses to load a plugin whose manifest has any.
/// </para>
/// <example>
/// <code>
/// {
///   "id": "word-count",
///   "name": "Word count",
///   "version": "1.0.0",
///   "api": "0.1",
///   "entryPoint": "Shiranuix.Plugin.WordCount.WordCountPlugin"
/// }
/// </code>
/// </example>
/// </remarks>
/// <param name="Id">The plugin's id, and the folder name it is filed under.</param>
/// <param name="Name">The name a person reads.</param>
/// <param name="Version">The version the plugin declares.</param>
/// <param name="Api">
/// The contract versions it works with — <c>0.1</c>, <c>0.1 - 0.3</c>, or
/// <c>*</c>.
/// </param>
/// <param name="EntryPoint">
/// The full name of the type implementing <see cref="IPlugin"/>, in the
/// assembly beside this manifest.
/// </param>
/// <param name="Description">What the plugin does, in a line, or null.</param>
/// <param name="Author">Who wrote it, or null.</param>
public sealed record PluginManifest(
    string Id,
    string Name,
    Version Version,
    PluginApiRange Api,
    string EntryPoint,
    string? Description = null,
    string? Author = null) {
    /// <summary>
    /// Every way this manifest is wrong, phrased for the person who has to fix
    /// it.
    /// </summary>
    /// <returns>
    /// The problems, in the order they were found; empty when the manifest is
    /// sound.
    /// </returns>
    public IReadOnlyList<string> Validate() {
        var problems = new List<string>();

        if (!PluginId.IsValid(Id)) {
            problems.Add(
                $"The id '{Id}' is not usable: lower-case letters, digits, dots and dashes, "
                + $"between {PluginId.MinLength} and {PluginId.MaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(Name)) {
            problems.Add("The plugin needs a name somebody can read.");
        }

        if (Version is null) {
            problems.Add("The plugin needs a version.");
        }

        if (Api is null) {
            problems.Add("The plugin needs to say which contract versions it works with.");
        }

        if (string.IsNullOrWhiteSpace(EntryPoint)) {
            problems.Add("The manifest needs the full name of the type implementing IPlugin.");
        }

        return problems;
    }
}
