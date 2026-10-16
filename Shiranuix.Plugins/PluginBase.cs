using Shiranuix.Plugins.Diagnostics;
using Shiranuix.Plugins.Messaging;
using Shiranuix.Plugins.Services;

namespace Shiranuix.Plugins;

/// <summary>
/// A starting point for a plugin, saving the three lines every one of them
/// would otherwise open with.
/// </summary>
/// <typeparam name="TPlugin">
/// The plugin itself, so the base class can hand each instance its own log
/// and its own identity rather than something shared.
/// </typeparam>
/// <remarks>
/// <para>
/// Optional, and the contract does not need it: a plugin that prefers to
/// implement <see cref="IPlugin"/> on its own class may, and nothing in the
/// editor will know the difference. What this gives is the ordinary case —
/// <c>Log</c>, <c>Services</c> and <c>Events</c> are already to hand, typed,
/// with no field and no constructor of its own.
/// </para>
/// <para>
/// The two halves are virtual with nothing to do, so a plugin that has no
/// setup to speak of and one that has a great deal are written the same way.
/// </para>
/// </remarks>
public abstract class PluginBase<TPlugin> : IPlugin
    where TPlugin : PluginBase<TPlugin> {
    private IPluginContext? _context;

    /// <summary>
    /// This plugin's own log, every line in it carrying the plugin's id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Asked for before <see cref="InitializeAsync"/> has handed over the
    /// context, which means a plugin reaching for its log in its constructor.
    /// There is nowhere to write it yet at that point.
    /// </exception>
    protected IPluginLog Log => Context.Log;

    /// <summary>
    /// What this plugin has offered, and what other plugins have offered.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Asked for before the plugin has been given its context.
    /// </exception>
    protected IPluginServices Services => Context.Services;

    /// <summary>
    /// The channel this plugin announces over and listens to.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Asked for before the plugin has been given its context.
    /// </exception>
    protected IPluginEvents Events => Context.Events;

    /// <summary>
    /// Who the editor thinks this plugin is.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Asked for before the plugin has been given its context.
    /// </exception>
    protected PluginIdentity Self => Context.Self;

    /// <inheritdoc />
    public virtual Task InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken) {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task ShutdownAsync(CancellationToken cancellationToken) {
        // The context goes before the plugin can ask for anything else: a
        // plugin winding down has no business reaching the editor afterwards.
        _context = null;

        return Task.CompletedTask;
    }

    private IPluginContext Context => _context ?? throw new InvalidOperationException(
        "A plugin can reach its context only between InitializeAsync and ShutdownAsync.");
}
