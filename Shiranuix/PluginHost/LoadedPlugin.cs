using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;

namespace Shiranuix.PluginHost;

/// <summary>
/// One plugin that loaded: what it is, what it was given, and what it offered
/// in return.
/// </summary>
/// <remarks>
/// The load context is held here on purpose. It is the thing that keeps the
/// plugin's assembly alive, and letting it fall out of scope would leave the
/// editor holding an interface into an assembly nothing else knows about.
/// </remarks>
internal sealed class LoadedPlugin {
    private readonly PluginLoadContext _world;

    /// <summary>
    /// Records a plugin that has been built and handed its context.
    /// </summary>
    /// <param name="world">The private world it was loaded into.</param>
    /// <param name="identity">Who it is.</param>
    /// <param name="plugin">The entry point the editor calls.</param>
    /// <param name="context">What it was given.</param>
    public LoadedPlugin(
        PluginLoadContext world,
        PluginIdentity identity,
        IPlugin plugin,
        IPluginContext context) {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Entry = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Who the plugin is, and the folder it was loaded from.</summary>
    public PluginIdentity Identity { get; }

    /// <summary>The entry point, as the plugin's own assembly built it.</summary>
    public IPlugin Entry { get; }

    /// <summary>The context it was given at startup.</summary>
    public IPluginContext Context { get; }

    /// <summary>
    /// The commands it offers, empty when it offers none or has not finished
    /// starting yet.
    /// </summary>
    public IReadOnlyList<IPluginCommand> Commands { get; private set; } = [];

    /// <summary>Whether the plugin has finished starting.</summary>
    public bool Started { get; private set; }

    /// <summary>
    /// Takes down what the plugin offered once it is up.
    /// </summary>
    /// <param name="commands">The commands it brought.</param>
    public void SetCommands(IReadOnlyList<IPluginCommand> commands) {
        ArgumentNullException.ThrowIfNull(commands);

        Commands = commands;
    }

    /// <summary>
    /// Lets the plugin get ready, and takes down whatever it offers afterwards.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <param name="timeout">
    /// How long to wait for a plugin that will not hurry. A plugin slower than
    /// this is left to finish on its own rather than holding the window.
    /// </param>
    /// <param name="onStarted">What to do with what the plugin offered, once it is up.</param>
    public async Task StartAsync(
        CancellationToken cancellationToken,
        TimeSpan timeout,
        Action<IReadOnlyList<IPluginCommand>> onStarted) {
        await Entry.InitializeAsync(Context, cancellationToken).WaitAsync(timeout, cancellationToken);

        Started = true;
        onStarted(Entry is IPluginCommandProvider provider
            ? provider.Commands
            : []);
    }

    /// <summary>
    /// Winds the plugin down on the way out.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor has waited long enough.</param>
    /// <param name="timeout">How long to wait before walking away.</param>
    public async Task StopAsync(CancellationToken cancellationToken, TimeSpan timeout) {
        if (!Started) {
            return;
        }

        await Entry.ShutdownAsync(cancellationToken).WaitAsync(timeout, cancellationToken);
        Started = false;
    }
}
