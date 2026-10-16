namespace Shiranuix.Plugins;

/// <summary>
/// What the host hands a plugin when it loads it.
/// </summary>
/// <remarks>
/// Three things and no more: who you are, where to say what happened, and a
/// way to ask for what you can do. The editor itself is not on this contract
/// — it is a capability, asked for by the interface the plugin wants to work
/// against, which is what keeps the contract honest: a plugin that never asks
/// for <see cref="IEditorHost"/> cannot touch the document, and a host that
/// does not offer one can say so without breaking anyone.
/// </remarks>
public interface IPluginContext {
    /// <summary>Who the plugin is, and the folder its own files live in.</summary>
    PluginIdentity Self { get; }

    /// <summary>The log every line of this plugin's is written to.</summary>
    IPluginLog Log { get; }

    /// <summary>
    /// Asks the host for one of the things it is willing to offer.
    /// </summary>
    /// <typeparam name="TCapability">
    /// The interface asked for — one the host documents as a capability, such
    /// as <see cref="IEditorHost"/> or <see cref="IPluginDialog"/>.
    /// </typeparam>
    /// <returns>
    /// The host's implementation, or null when this host does not offer that
    /// capability. Ask in one place and answer null rather than throw: a
    /// plugin that runs in a host without a capability should say it cannot do
    /// the thing, not take the editor down asking.
    /// </returns>
    TCapability? Capability<TCapability>()
        where TCapability : class;
}
