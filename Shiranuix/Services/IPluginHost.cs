using Shiranuix.Plugins;
namespace Shiranuix.Services;

/// <summary>
/// One plugin folder, read.
/// </summary>
/// <param name="Folder">The folder it is installed in.</param>
/// <param name="Id">What it calls itself, or the folder's name when it says nothing.</param>
/// <param name="Version">The version it says it is.</param>
/// <param name="Description">What it says it does.</param>
/// <param name="Problem">
/// Why this folder cannot be loaded, in a sentence a writer can act on. Empty
/// when it can.
/// </param>
/// <remarks>
/// A folder that could not be read is still a row: it is still on the disk,
/// taking up space, and a list of plugins that only showed the ones working
/// would leave a writer with no way of getting rid of the one that is not.
/// </remarks>
public sealed record PluginOnDisk(
    string Folder,
    string Id,
    string Version,
    string Description,
    string Problem);

/// <summary>
/// The plugins the editor has, and the one thing that can be done to a running
/// one.
/// </summary>
/// <remarks>
/// The seam the settings window talks to, and nothing else: what is
/// installed, what each folder says about itself, and taking a plugin out.
/// Naming it rather than reaching for the catalog is what keeps the folder
/// conventions - which folder, which file, what a manifest has to contain -
/// on the host's side of the line.
/// </remarks>
public interface IPluginHost {
    /// <summary>
    /// The plugins that are loaded right now.
    /// </summary>
    IReadOnlyList<PluginIdentity> Running { get; }

    /// <summary>
    /// Reads every plugin folder the host was pointed at, including the ones
    /// that would not load.
    /// </summary>
    /// <returns>One entry per folder, whether or not it is usable.</returns>
    IReadOnlyList<PluginOnDisk> OnDisk();

    /// <summary>
    /// Raised when the running set changes: a plugin finished starting, or was
    /// taken out of the editor.
    /// </summary>
    event EventHandler? PluginsChanged;

    /// <summary>
    /// Winds a loaded plugin down and takes it out of the editor that is
    /// running: its panels leave the column, its commands leave the palette
    /// and the keybinding table.
    /// </summary>
    /// <remarks>
    /// The assembly itself stays in memory until the editor closes - a
    /// plugin's world is not collectible - so this is about behaviour, not
    /// about memory. Nothing is deleted from disk: that is the settings
    /// window's decision, made with the writer's answer behind it.
    /// </remarks>
    /// <param name="pluginId">The plugin to take out.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>Whether a loaded plugin went by that id.</returns>
    Task<bool> UnloadAsync(string pluginId, CancellationToken cancellationToken);
}
