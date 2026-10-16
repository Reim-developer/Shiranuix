namespace Shiranuix.Persistence;

/// <summary>
/// The plugins the writer has turned off.
/// </summary>
/// <remarks>
/// Kept behind an interface for the same reason as
/// <see cref="IKeyBindingsStore"/>: the state that reads this runs headlessly
/// in tests, over an in-memory list that never touches a disk.
///
/// A plugin's own state lives beside its assembly, but this does not: it is
/// the editor's opinion about a folder, not the folder's opinion about itself,
/// and it has to be readable while the plugin is not running at all.
/// </remarks>
public interface IDisabledPluginsStore {
    /// <summary>
    /// The ids of the plugins that should not be loaded.
    /// </summary>
    /// <returns>The disabled ids, empty when none are.</returns>
    IReadOnlyList<string> Load();

    /// <summary>
    /// Writes the disabled set, replacing whatever was there.
    /// </summary>
    /// <param name="pluginIds">The ids to keep disabled.</param>
    void Save(IReadOnlyList<string> pluginIds);
}
