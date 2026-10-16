using Shiranuix.Plugins;

namespace Shiranuix.PluginHost;

/// <summary>
/// One plugin's view of the editor: who it is, where to speak, and what the
/// editor is willing to hand it.
/// </summary>
/// <remarks>
/// The capabilities are the editor's answer to
/// <see cref="IPluginContext.Capability{TCapability}"/>, and the answer is a
/// lookup rather than a construction: the editor decides once, at startup,
/// what it offers, and every plugin is handed the same set. A capability the
/// editor does not have is simply not in the table, which is how the null in
/// that contract comes to be honest.
/// </remarks>
internal sealed class PluginContext : IPluginContext {
    private readonly IReadOnlyDictionary<Type, object> _capabilities;

    /// <summary>
    /// Creates the context for one plugin.
    /// </summary>
    /// <param name="self">Who the plugin is.</param>
    /// <param name="log">Where its lines go.</param>
    /// <param name="capabilities">What the editor is offering every plugin.</param>
    public PluginContext(
        PluginIdentity self,
        IPluginLog log,
        IReadOnlyDictionary<Type, object> capabilities) {
        Self = self ?? throw new ArgumentNullException(nameof(self));
        Log = log ?? throw new ArgumentNullException(nameof(log));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    /// <inheritdoc />
    public PluginIdentity Self { get; }

    /// <inheritdoc />
    public IPluginLog Log { get; }

    /// <inheritdoc />
    public TCapability? Capability<TCapability>()
        where TCapability : class =>
        _capabilities.TryGetValue(typeof(TCapability), out var capability)
            ? (TCapability)capability
            : null;
}
