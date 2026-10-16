using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Messaging;
using Shiranuix.Plugins.Services;

namespace Shiranuix.Plugins;

/// <summary>
/// What the host hands a plugin when it loads it.
/// </summary>
/// <remarks>
/// <para>
/// Five things and no more: who you are, where to say what happened, what other
/// plugins have offered, the channel to talk over, and a way to ask for what
/// the editor itself is willing to do. The editor is not on the last one by
/// name: it is a capability, asked for by the interface the plugin wants to
/// work against, which is what keeps the contract honest. A plugin that never
/// asks for <see cref="IEditorHost"/> cannot touch the document, and a host
/// that does not offer one can say so without breaking anyone.
/// </para>
/// <para>
/// The middle two are where a plugin goes past what this contract happens to
/// name. <see cref="Services"/> and <see cref="Events"/> are typed and open, so
/// two plugins can agree on a service or a piece of news the editor has never
/// heard of, and neither needs a door added here for it.
/// </para>
/// </remarks>
public interface IPluginContext {
    /// <summary>Who the plugin is, and the folder its own files live in.</summary>
    PluginIdentity Self { get; }

    /// <summary>The log every line of this plugin's is written to.</summary>
    IPluginLog Log { get; }

    /// <summary>
    /// What this plugin has offered, and what every other plugin in the editor
    /// has offered.
    /// </summary>
    IPluginServices Services { get; }

    /// <summary>
    /// The channel plugins and the editor announce news over.
    /// </summary>
    IPluginEvents Events { get; }

    /// <summary>
    /// Asks the host for one of the things it is willing to offer.
    /// </summary>
    /// <typeparam name="TCapability">
    /// The interface asked for: one the host documents as a capability, such
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
