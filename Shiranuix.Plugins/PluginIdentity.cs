namespace Shiranuix.Plugins;

/// <summary>
/// Who the host thinks this plugin is: the name it is filed under, the version
/// it claims, and the folder its files live in.
/// </summary>
/// <remarks>
/// Handed to a plugin at load time so it can find things beside itself — a
/// template, a word list, a database of its own. The host promises the folder
/// stays put for as long as the plugin runs; the plugin is on its own for what
/// it writes there.
/// </remarks>
/// <param name="Id">The plugin's id, as its manifest declares it.</param>
/// <param name="Name">The name a person reads.</param>
/// <param name="Version">The version the manifest declares.</param>
/// <param name="Directory">The folder the plugin was loaded from.</param>
public sealed record PluginIdentity(
    string Id,
    string Name,
    Version Version,
    string Directory);
