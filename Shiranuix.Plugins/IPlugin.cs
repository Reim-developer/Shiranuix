namespace Shiranuix.Plugins;

/// <summary>
/// The entry point every plugin implements: the host loads the assembly, finds
/// the type the manifest names, builds one of these and hands it its context.
/// </summary>
/// <remarks>
/// <para>
/// An interface with no base class and no attributes on purpose. A plugin is
/// an assembly the host has never seen, so the fewer things it has to match —
/// a base class, an attribute, a constructor shape — the fewer ways it can be
/// wrong before it runs.
/// </para>
/// <para>
/// Both halves get a cancellation token because both can be slow: reading a
/// large file beside the plugin, or a network call the plugin made itself.
/// The host keeps the plugin alive until <see cref="ShutdownAsync"/> returns,
/// and passes a token that has already been cancelled if the editor is
/// closing — so a plugin that checks it saves the writer a frozen window.
/// </para>
/// <para>
/// Commands are not here. A plugin that has commands implements
/// <see cref="IPluginCommandProvider"/> as well, and the host asks for it
/// after this has run; a plugin that has none implements only this.
/// </para>
/// </remarks>
public interface IPlugin {
    /// <summary>
    /// Gets the plugin ready: ask for capabilities, read the plugin's own
    /// settings, and do nothing slow enough to be worth showing a window for.
    /// </summary>
    /// <param name="context">The host's doors.</param>
    /// <param name="cancellationToken">
    /// Cancelled when the editor is closing.
    /// </param>
    /// <returns>A task that finishes when the plugin is ready.</returns>
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Winds the plugin down on the way out, giving back anything it holds.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancelled when the editor has waited as long as it means to wait.
    /// </param>
    /// <returns>A task that finishes when the plugin is done.</returns>
    Task ShutdownAsync(CancellationToken cancellationToken);
}
