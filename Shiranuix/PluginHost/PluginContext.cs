using Shiranuix.Plugins;
using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Messaging;
using Shiranuix.Plugins.Services;

namespace Shiranuix.PluginHost;

/// <summary>
/// One plugin's view of the editor: who it is, where to speak, what other
/// plugins have offered, the channel to talk over, and what the editor is
/// willing to hand it.
/// </summary>
/// <remarks>
/// <para>
/// The capabilities are the editor's answer to
/// <see cref="IPluginContext.Capability{TCapability}"/>, and the answer is a
/// lookup rather than a construction: the editor decides once, at startup,
/// what it offers, and every plugin is handed the same set. A capability the
/// editor does not have is simply not in the table, which is how the null in
/// that contract comes to be honest.
/// </para>
/// <para>
/// The register and the channel are shared by every plugin on purpose. Both
/// are how a plugin reaches past the contract without the contract changing:
/// the register is what a plugin offers, the channel is what they announce
/// over, and neither of them needs the editor to have heard of the type first.
/// </para>
/// </remarks>
internal sealed class PluginContext : IPluginContext {
    private readonly IReadOnlyDictionary<Type, object> _capabilities;

    /// <summary>
    /// Creates the context for one plugin.
    /// </summary>
    /// <param name="self">Who the plugin is.</param>
    /// <param name="log">Where its lines go.</param>
    /// <param name="services">The register every plugin shares.</param>
    /// <param name="events">The channel every plugin announces over.</param>
    /// <param name="capabilities">What the editor is offering every plugin.</param>
    public PluginContext(
        PluginIdentity self,
        IPluginLog log,
        IPluginServices services,
        IPluginEvents events,
        IReadOnlyDictionary<Type, object> capabilities) {
        Self = self ?? throw new ArgumentNullException(nameof(self));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    /// <inheritdoc />
    public PluginIdentity Self { get; }

    /// <inheritdoc />
    public IPluginLog Log { get; }

    /// <inheritdoc />
    public IPluginServices Services { get; }

    /// <inheritdoc />
    public IPluginEvents Events { get; }

    /// <inheritdoc />
    public TCapability? Capability<TCapability>()
        where TCapability : class =>
        _capabilities.TryGetValue(typeof(TCapability), out var capability)
            ? (TCapability)capability
            : null;
}
