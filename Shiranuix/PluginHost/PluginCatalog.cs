using Microsoft.Extensions.Logging;
using Shiranuix.Persistence;
using Shiranuix.Plugins;
using Shiranuix.Plugins.Commands;
using Shiranuix.Plugins.Dialogs;
using Shiranuix.Plugins.Editor;
using Shiranuix.Plugins.Messaging;
using Shiranuix.Plugins.Services;
using Shiranuix.Plugins.UI;
using Shiranuix.Services;
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
internal sealed class PluginCatalog : IPluginHost {
    /// <summary>How long a plugin may take to start before it is written about.</summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger _log;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PluginServices _services;
    private readonly PluginEvents _events;
    private readonly PanelRegistry _panels;
    private readonly IReadOnlyDictionary<Type, object> _capabilities;
    private readonly List<LoadedPlugin> _loaded = [];

    /// <summary>Where the commands plugins offer are kept, for the palette.</summary>
    private readonly PluginCommandRegistry _registry;

    /// <summary>Which plugins the writer has turned off.</summary>
    private readonly IDisabledPluginsStore _disabled;

    /// <summary>
    /// Raised when what is running changes: a plugin finished starting, or was
    /// taken out. The settings window reads the folder, and a folder read once
    /// at the moment the window opened would say a plugin is not running for
    /// as long as the plugins were still loading.
    /// </summary>
    public event EventHandler? PluginsChanged;

    /// <summary>The folder plugins were looked for in, as last told.</summary>
    private string _root = AppFolder.Plugins;

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
        IDialogService questions,
        ILoggerFactory loggerFactory,
        PluginServices services,
        PluginEvents events,
        PanelRegistry panels,
        PluginCommandRegistry registry,
        IDisabledPluginsStore disabled) {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(panels);
        ArgumentNullException.ThrowIfNull(registry);

        _loggerFactory = loggerFactory;
        _services = services;
        _events = events;
        _panels = panels;
        _disabled = disabled ?? throw new ArgumentNullException(nameof(disabled));
        _registry = registry;
        _log = loggerFactory.CreateLogger<PluginCatalog>();

        // The doors every plugin is offered, decided once and here rather than
        // per plugin: the document, the editor's questions, the column a panel
        // can be drawn in, and the channel plugins talk over.
        _capabilities = new Dictionary<Type, object> {
            [typeof(IEditorHost)] = new EditorHostCapability(editor, dialogs),
            [typeof(IPluginDialog)] = new DialogCapability(questions, dialogs),
            [typeof(IPluginPanelHost)] = panels,

            // The channel is a door like any other: a plugin publishes news other
            // plugins hear, and the only way it can be one of them is for the editor
            // to hand it over. It was not, so a folder a tree stood on went out onto
            // a channel nobody was listening to.
            [typeof(IPluginEvents)] = events
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

    /// <inheritdoc />
    public IReadOnlyList<PluginIdentity> Running => Installed;

    /// <inheritdoc />
    public IReadOnlyList<PluginOnDisk> OnDisk() {
        var running = Installed.Select(identity => identity.Id).ToHashSet(StringComparer.Ordinal);
        var rows = new List<PluginOnDisk>();

        foreach (var directory in PluginFolder.Candidates(_root)) {
            var read = PluginManifestReader.Read(directory);
            var folder = Path.GetFileName(directory);

            // A folder with nothing readable in it is still a row: it is still
            // on the disk, and a list of plugins showing only the working ones
            // would leave a writer no way to get rid of the one that is not.
            if (read.Manifest is not { } manifest) {
                rows.Add(new PluginOnDisk(
                    directory, folder, string.Empty, string.Empty,
                    read.Problems.Count > 0 ? read.Problems[0] : "There is no manifest here."));

                continue;
            }

            var problem = string.Empty;
            if (!manifest.Api.Contains(PluginApi.Current)) {
                problem =
                    $"It speaks contract {Wanted(manifest.Api)}; this editor speaks {PluginApi.Current}.";
            } else if (!running.Contains(manifest.Id)
                && _disabled.Load().Contains(manifest.Id, StringComparer.Ordinal)) {
                problem = "Turned off.";
            }

            rows.Add(new PluginOnDisk(
                directory, manifest.Id, manifest.Version.ToString(),
                manifest.Description ?? string.Empty, problem));
        }

        return rows;
    }

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
        _root = root;
        EmptyBin();

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
    /// Winds one plugin down and takes it out of the editor that is running:
    /// its panels come out of the column, its commands out of the palette and
    /// the keybinding table, and it is forgotten.
    /// </summary>
    /// <remarks>
    /// Done while the editor is up because a writer who turns something off
    /// expects it off now, not on the next run. What cannot be done is
    /// forgetting the assembly: the plugin's world is not collectible, since
    /// unloading one another thread may still be inside is a promise the
    /// runtime cannot keep — so its code stays in memory until the editor
    /// closes, and its folder stays where it is until it is deleted.
    /// </remarks>
    /// <param name="pluginId">The plugin to take out.</param>
    /// <param name="cancellationToken">Cancelled when the editor is closing.</param>
    /// <returns>Whether a loaded plugin went by that id.</returns>
    public async Task<bool> UnloadAsync(string pluginId, CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var plugin = _loaded.FirstOrDefault(candidate =>
            string.Equals(candidate.Identity.Id, pluginId, StringComparison.Ordinal));

        if (plugin is null) {
            return false;
        }

        var panels = plugin.Entry is IPluginPanelProvider provider
            ? provider.Panels.ToArray()
            : [];

        try {
            await plugin.StopAsync(cancellationToken, StartTimeout);
        } catch (Exception error) {
            _log.LogError(error, "{Plugin} did not shut down cleanly.", pluginId);
        }

        // Read out of the plugin before it is stopped: a plugin that tidies up
        // after itself lets go of its panels as it winds down, so asking
        // afterwards finds an empty list and the column keeps a view whose
        // plugin has already gone.
        foreach (var panel in panels) {
            _panels.Hide(panel.Id);
        }

        _registry.Retire(pluginId);
        _loaded.Remove(plugin);
        plugin.Dispose();

        _log.LogInformation("{Plugin} was taken out of the running editor.", pluginId);

        PluginsChanged?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Empties the bin a removed plugin's files were left in.
    /// </summary>
    /// <remarks>
    /// Windows keeps a loaded assembly's file mapped, so a plugin taken out
    /// while the editor runs can be moved out of the plugins folder but not
    /// deleted. Its files go on the next run, when nothing of it is loaded and
    /// therefore nothing of it is holding a file open. A folder that still
    /// refuses to go is left where it is and written about: it costs disk and
    /// it is not a reason to stop the editor from starting.
    /// </remarks>
    private void EmptyBin() {
        var bin = Path.Combine(AppFolder.Root, "bin");
        if (!Directory.Exists(bin)) {
            return;
        }

        foreach (var folder in Directory.GetDirectories(bin)) {
            try {
                Directory.Delete(folder, recursive: true);
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                _log.LogWarning(
                    error,
                    "A removed plugin's files are still in use and stay for now: {Folder}",
                    folder);
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

        // A plugin the writer has turned off is not a problem and not a
        // reason to be quiet: it is said so here, once, where the rest of the
        // loading is written about.
        if (_disabled.Load().Contains(name, StringComparer.Ordinal)) {
            _log.LogInformation("{Plugin} is turned off, so it was not loaded.", name);
            return;
        }
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

                // Told as it happens, rather than asked for later: the palette
                // is a door for commands nobody could have known about when
                // the window opened.
                _registry.Offer(commands);
                foreach (var command in commands) {
                    _log.LogInformation("{Plugin} offered the command {Command}.",
                        plugin.Identity.Id, command.Info.Id);
                }
            });

            ShowPanels(plugin);
            _log.LogInformation("{Plugin} loaded.", plugin.Identity.Id);

            // Said after, not before: a window listening to this is reading the
            // running set, and reading it half way through a load would find the
            // plugin missing and say so.
            PluginsChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Puts a started plugin's panels in the window's column, one at a time,
    /// writing down each refusal so a plugin whose panel could not be shown is
    /// told so rather than left wondering why it is not there.
    /// </summary>
    /// <param name="plugin">The plugin that has just started.</param>
    private void ShowPanels(LoadedPlugin plugin) {
        if (plugin.Entry is not IPluginPanelProvider provider) {
            return;
        }

        foreach (var panel in provider.Panels) {
            var outcome = _panels.Show(panel);
            if (outcome.IsSuccess) {
                _log.LogInformation(
                    "{Plugin} offered the panel {Panel}.", plugin.Identity.Id, panel.Id);
            } else {
                _log.LogWarning(
                    "{Plugin}'s panel {Panel} was not shown: {Reason}",
                    plugin.Identity.Id, panel.Id, outcome.Error);
            }
        }
    }
}
