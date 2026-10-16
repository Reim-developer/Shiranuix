using Microsoft.Extensions.Logging;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Services;
using Shiranuix.Services.Dialogs;
using Shiranuix.ViewModels;

namespace Shiranuix.PluginHost;

/// <summary>
/// What is installed: every plugin that loaded, in the order their folders are
/// read, and a line in the log for every one that did not.
/// </summary>
/// <remarks>
/// <para>
/// One bad plugin is one bad plugin. A folder with no manifest, a manifest
/// with a typo in it, an assembly that will not load, an entry point that
/// throws in its constructor — each is a sentence in the log and the next
/// folder is read anyway. The editor does not refuse to start because something
/// it can do without is broken.
/// </para>
/// <para>
/// A plugin is loaded once, at startup, and kept until the editor closes.
/// </para>
/// </remarks>
internal sealed class PluginCatalog {
    /// <summary>How long a plugin may take to start before it is written about.</summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger _log;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PluginServices _services;
    private readonly PluginEvents _events;
    private readonly IReadOnlyDictionary<Type, object> _capabilities;
    private readonly List<LoadedPlugin> _loaded = [];

    /// <summary>
    /// Creates the catalog over the editor's own services.
    /// </summary>
    /// <param name="editor">The open document, offered to plugins as a capability.</param>
    /// <param name="dialogs">Asked where a plugin should save a new document.</param>
    /// <param name="loggerFactory">Where plugin lines and refusals go.</param>
    /// <param name="services">
    /// The register plugins offer services on, shared with this catalog so
    /// that the editor's doors are in it before any plugin runs.
    /// </param>
    /// <param name="events">The channel plugins announce news over.</param>
    public PluginCatalog(
        EditorViewModel editor,
        IFileDialogService dialogs,
        ILoggerFactory loggerFactory,
        PluginServices services,
        PluginEvents events) {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(events);

        _loggerFactory = loggerFactory;
        _services = services;
        _events = events;
        _log = loggerFactory.CreateLogger<PluginCatalog>();

        // The doors every plugin is offered, decided once and here rather than
        // per plugin: the editor, and nothing else. There is no dialog door
        // yet - the editor can ask a question but cannot yet take a line of text
        // back, and a capability that can only do half of what its name
        // promises is worse than none.
        _capabilities = new Dictionary<Type, object> {
            [typeof(IEditorHost)] = new EditorHostCapability(editor, dialogs)
        };

        // The same doors on the register, so that asking for one through the
        // open register and asking for it as a capability both find the same
        // instance.
        foreach (var capability in _capabilities) {
            services.Register(capability.Key, capability.Value);
        }
    }

    /// <summary>
    /// The plugins that loaded, in the order they were found.
    /// </summary>
    public IReadOnlyList<PluginIdentity> Installed =>
        _loaded.Select(plugin => plugin.Identity).ToArray();

    private IReadOnlyList<LoadedPlugin> Loaded => _loaded;

    /// <summary>
    /// Every command every loaded plugin offers, in the same order.
    /// </summary>
    public IReadOnlyList<IPluginCommand> Commands =>
        _loaded.SelectMany(plugin => plugin.Commands).ToArray();

    /// <summary>
    /// Reads a folder of plugins and starts what it finds.
    /// </summary>
    /// <param name="root">The folder to read.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>A task that finishes when every plugin has had its chance.</returns>
    public async Task LoadAsync(string root, CancellationToken cancellationToken) {
        foreach (var directory in PluginFolder.Candidates(root)) {
            cancellationToken.ThrowIfCancellationRequested();
            await TryLoadAsync(directory, cancellationToken);
        }

        _log.LogInformation(
            "{Count} plugin(s) loaded from {Root}, {Commands} command(s) offered.",
            _loaded.Count, root, Commands.Count);
    }

    /// <summary>
    /// Winds every loaded plugin down on the way out.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the editor has waited long enough.</param>
    /// <returns>A task that finishes when every plugin has been given its chance.</returns>
    public async Task ShutdownAsync(CancellationToken cancellationToken) {
        foreach (var plugin in _loaded) {
            try {
                await plugin.StopAsync(cancellationToken, StartTimeout);
            } catch (Exception error) {
                _log.LogError(error, "{Plugin} did not shut down cleanly.", plugin.Identity.Id);
            }
        }
    }

    /// <summary>
    /// Loads one folder, and writes down why when it does not.
    /// </summary>
    /// <param name="directory">The plugin's folder.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>A task that finishes when the plugin is up, or has given up.</returns>
    private async Task TryLoadAsync(string directory, CancellationToken cancellationToken) {
        var read = PluginManifestReader.Read(directory);
        if (read.Manifest is not { } manifest) {
            foreach (var problem in read.Problems) {
                _log.LogWarning("Not loading '{Folder}': {Problem}", Path.GetFileName(directory), problem);
            }

            return;
        }

        var name = manifest.Id;
        if (!manifest.Api.Contains(PluginApi.Current)) {
            _log.LogWarning(
                "{Plugin} speaks contract {Wanted}; this editor speaks {Here}.",
                name, Wanted(manifest.Api), PluginApi.Current);
            return;
        }

        try {
            var plugin = Build(directory, manifest);
            _loaded.Add(plugin);
            await StartAsync(plugin, cancellationToken);
        } catch (Exception error) {
            _log.LogError(error, "{Plugin} did not load.", name);
        }
    }

    /// <summary>
    /// Builds one plugin: its assembly, its entry point, and its context.
    /// </summary>
    /// <param name="directory">The plugin's folder.</param>
    /// <param name="manifest">What it says about itself.</param>
    /// <returns>The plugin, ready to be started.</returns>
    /// <exception cref="InvalidOperationException">
    /// The assembly is not there, or the type the manifest names is not a
    /// plugin. Both are the plugin author's mistake, said in one sentence.
    /// </exception>
    private LoadedPlugin Build(string directory, PluginManifest manifest) {
        // The assembly is named after the plugin, the same way its folder is:
        // one rule to learn, one name to keep straight.
        var assemblyPath = Path.Combine(directory, $"{manifest.Id}.dll");
        if (!File.Exists(assemblyPath)) {
            throw new InvalidOperationException(
                $"{assemblyPath} is not there. A plugin's assembly is named after it.");
        }

        var world = new PluginLoadContext(directory);
        var assembly = world.LoadFromAssemblyPath(assemblyPath);

        var type = assembly.GetType(manifest.EntryPoint)
            ?? throw new InvalidOperationException(
                $"'{manifest.EntryPoint}' is not a type in {assembly.GetName().Name}.");

        if (!typeof(IPlugin).IsAssignableFrom(type)) {
            throw new InvalidOperationException(
                $"'{manifest.EntryPoint}' does not implement {nameof(IPlugin)}.");
        }

        var plugin = (IPlugin)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                $"'{manifest.EntryPoint}' could not be built; it needs a constructor with no arguments."));

        var identity = new PluginIdentity(
            manifest.Id, manifest.Name, manifest.Version, directory);

        var context = new PluginContext(
            identity,
            new PluginLog(_loggerFactory, manifest.Id),
            _services,
            _events,
            _capabilities);

        return new LoadedPlugin(world, identity, plugin, context);
    }

    /// <summary>
    /// Starts one plugin, and keeps it even when it takes its time.
    /// </summary>
    /// <param name="plugin">The plugin to start.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>A task that finishes when the plugin is up, or has given up.</returns>
    private async Task StartAsync(LoadedPlugin plugin, CancellationToken cancellationToken) {
        try {
            await plugin.StartAsync(cancellationToken, StartTimeout, commands => {
                plugin.SetCommands(commands);
                foreach (var command in commands) {
                    _log.LogInformation("{Plugin} offered the command {Command}.",
                        plugin.Identity.Id, command.Info.Id);
                }
            });

            _log.LogInformation("{Plugin} loaded.", plugin.Identity.Id);
        } catch (TimeoutException) {
            // Slow is not broken. The plugin is left running; it just does not
            // get its commands read until it is done.
            _log.LogWarning(
                "{Plugin} took longer than {Seconds}s to start.",
                plugin.Identity.Id, StartTimeout.TotalSeconds);
        }
    }

    /// <summary>
    /// Says a range the way a manifest writes it, which is how a reader will
    /// recognise it in the log.
    /// </summary>
    /// <param name="range">The range to say.</param>
    /// <returns>The range as text.</returns>
    private static string Wanted(PluginApiRange range) =>
        range.Maximum is null ? $"{range.Minimum}+" : $"{range.Minimum} - {range.Maximum}";
}
