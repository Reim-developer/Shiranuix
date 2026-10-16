using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Shiranuix.Persistence;
using Shiranuix.Services;
using Shiranuix.Services.Dialogs;

namespace Shiranuix.ViewModels;

/// <summary>
/// One plugin folder, as the settings window shows it: what it says about
/// itself, whether it is running, and what can be done to it.
/// </summary>
public sealed partial class PluginRow : ObservableObject {
    private readonly PluginSettingsState _state;

    /// <summary>
    /// Builds a row over a folder that has been read.
    /// </summary>
    /// <param name="state">The state that acts on it.</param>
    /// <param name="onDisk">What the folder says about itself.</param>
    /// <param name="running">Whether it is running right now.</param>
    /// <param name="enabled">Whether it is turned on.</param>
    public PluginRow(
        PluginSettingsState state,
        PluginOnDisk onDisk,
        bool running,
        bool enabled) {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        OnDisk = onDisk ?? throw new ArgumentNullException(nameof(onDisk));
        IsRunning = running;
        _isEnabled = enabled;

        EnableCommand = new RelayCommand(() => _ = _state.EnabledChanged(this, !IsEnabled));
        RemoveCommand = new RelayCommand(() => _ = _state.RemoveAsync(this));
    }

    /// <summary>What the folder says about itself.</summary>
    public PluginOnDisk OnDisk { get; }

    /// <summary>The folder the plugin is installed in.</summary>
    public string Folder => OnDisk.Folder;

    /// <summary>What it calls itself.</summary>
    public string Id => OnDisk.Id;

    /// <summary>The version it says it is.</summary>
    public string Version => OnDisk.Version;

    /// <summary>What it says it does.</summary>
    public string Description => OnDisk.Description;

    /// <summary>Why it is not running, when it is not. Empty when it is.</summary>
    public string Problem => OnDisk.Problem;

    /// <summary>Whether anything is wrong with this folder.</summary>
    public bool HasProblem => !string.IsNullOrWhiteSpace(Problem);

    /// <summary>Whether it is running in this editor right now.</summary>
    public bool IsRunning { get; }

    /// <summary>What the switch says, in the editor's own words.</summary>
    public string SwitchLabel => IsEnabled ? "On" : "Off";

    /// <summary>Turns the plugin on or off.</summary>
    public IRelayCommand EnableCommand { get; }

    /// <summary>Deletes the plugin's folder, after asking.</summary>
    public IRelayCommand RemoveCommand { get; }

    /// <summary>
    /// Whether the plugin is turned on. Written to the store the moment it
    /// moves, and a plugin that was running is taken out of the running editor
    /// on the way - a writer who switches something off expects it off now.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>
    /// The switch moved: the store is written and a plugin that was running
    /// is taken out of the running editor on the way, because a writer who
    /// switches something off did not mean "on the next run".
    /// </summary>
    /// <param name="value">Whether the plugin is on.</param>
    partial void OnIsEnabledChanged(bool value) => _ = _state.EnabledChanged(this, value);

    /// <summary>
    /// What a writer needs to know about this folder, in one line.
    /// </summary>
    public string StateText => HasProblem
        ? Problem
        : IsRunning ? "Running." : IsEnabled ? "Not running." : "Turned off.";
}

/// <summary>
/// The plugins the writer has, and what can be done to them.
/// </summary>
/// <remarks>
/// <para>
/// The list is read from the folder rather than from the running editor, which
/// is the only way it can show a plugin that will not load: a folder with a
/// broken manifest, or an assembly for a contract this editor does not speak,
/// cannot be listed by the things that loaded successfully - nor removed by
/// them.
/// </para>
/// <para>
/// Two actions, and they are not the same action. Turning a plugin off leaves
/// its folder exactly where it is and is undone by turning it on again;
/// removing it deletes the folder, which is why it asks first. Both take
/// effect in the editor that is open - a plugin that was running comes out of
/// the column and out of the palette as it happens - because a writer who
/// switches something off did not mean "on the next run".
/// </para>
/// </remarks>
public sealed class PluginSettingsState : ObservableObject {
    private readonly IPluginHost _plugins;
    private readonly IDisabledPluginsStore _store;
    private readonly IDialogService _dialogs;
    private readonly ILogger _log;

    /// <summary>
    /// Builds the state over the plugins, the store of the turned-off ones and
    /// the dialogs that ask before anything is deleted.
    /// </summary>
    /// <param name="plugins">What is installed and what can be taken out.</param>
    /// <param name="store">Which plugins are turned off.</param>
    /// <param name="dialogs">Asked before a folder is deleted.</param>
    /// <param name="log">Where a folder that could not be deleted is written about.</param>
    public PluginSettingsState(
        IPluginHost plugins,
        IDisabledPluginsStore store,
        IDialogService dialogs,
        ILoggerFactory log) {
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _log = (log ?? throw new ArgumentNullException(nameof(log)))
            .CreateLogger<PluginSettingsState>();

        RefreshCommand = new RelayCommand(Refresh);

        // The running set moves on its own - plugins finish loading after this
        // window has opened - so the list is rebuilt when it does rather than
        // saying a plugin is not running for as long as it took to start.
        _plugins.PluginsChanged += (_, _) => Refresh();
    }

    /// <summary>The plugin folders there are, one row each.</summary>
    public ObservableCollection<PluginRow> Rows { get; } = [];

    /// <summary>Reads the folder again.</summary>
    public IRelayCommand RefreshCommand { get; }

    /// <summary>Whether the window should say there are no plugins.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>
    /// Reads every plugin folder there is and builds the rows.
    /// </summary>
    public void Refresh() {
        var disabled = _store.Load();
        var running = _plugins.Running.Select(id => id.Id).ToHashSet(StringComparer.Ordinal);

        Rows.Clear();
        foreach (var onDisk in _plugins.OnDisk()) {
            Rows.Add(new PluginRow(
                this, onDisk,
                running.Contains(onDisk.Id),
                !disabled.Contains(onDisk.Id, StringComparer.Ordinal)));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Takes a row's switch at its word: writes the store, and if the plugin
    /// was running and is now off, takes it out of the editor that is open.
    /// </summary>
    /// <remarks>
    /// On does not load it again there and then: loading a plugin is the host's
    /// job, and the host is half way through its own startup. Turning a plugin
    /// back on therefore takes effect on the next run, which the row says by
    /// reading "Not running." rather than pretending otherwise.
    /// </remarks>
    /// <param name="row">The plugin whose switch moved.</param>
    /// <param name="wanted">Whether the plugin is now on.</param>
    /// <returns>A task carrying the outcome.</returns>
    public async Task EnabledChanged(PluginRow row, bool wanted) {
        ArgumentNullException.ThrowIfNull(row);

        var disabled = _store.Load().ToList();

        if (wanted) {
            disabled.RemoveAll(id => string.Equals(id, row.Id, StringComparison.Ordinal));
        } else if (!disabled.Contains(row.Id, StringComparer.Ordinal)) {
            disabled.Add(row.Id);
        }

        _store.Save(disabled);

        // Off means out of the running editor now. On means it will be there
        // next time: loading a plugin is the host's job, and the host is half
        // way through its own startup.
        if (!wanted) {
            await _plugins.UnloadAsync(row.Id, CancellationToken.None);
        }

        _log.LogInformation("{Plugin} was turned {State}.", row.Id, wanted ? "on" : "off");
        Refresh();
    }

    /// <summary>
    /// Deletes one plugin's folder, after asking whether to.
    /// </summary>
    /// <remarks>
    /// The folder does not always go the first time, and the reason is worth
    /// saying out loud: Windows keeps a loaded assembly's file mapped, so the
    /// editor that just unloaded a plugin cannot always delete its files. When
    /// that happens the folder is moved out of <c>plugins/</c> into a bin
    /// beside the editor's other data and emptied on the next run. Either way
    /// the plugin is gone from the editor now - which is the part a writer can
    /// see - rather than left behind with an error nobody could act on.
    /// </remarks>
    /// <param name="row">The plugin to remove.</param>
    /// <returns>A task carrying the outcome.</returns>
    public async Task RemoveAsync(PluginRow row) {
        ArgumentNullException.ThrowIfNull(row);

        var answer = await _dialogs.ShowAsync(new DialogRequest<bool>(
            $"Remove {row.Id}?",
            $"Its folder and everything in it are deleted.\nIt cannot be brought back from here.",
            [
                new DialogButton<bool>("Keep it", false, IsDefault: true, IsCancel: true),
                new DialogButton<bool>("Remove", true)
            ]));

        if (answer != true) {
            return;
        }

        // Out of the editor first: a panel still holding a view whose files
        // are being deleted is worse than a panel that is gone.
        await _plugins.UnloadAsync(row.Id, CancellationToken.None);

        if (Delete(row.Folder)) {
            _log.LogInformation("{Plugin}'s folder was deleted.", row.Id);
        } else if (Bin(row.Folder, row.Id)) {
            _log.LogInformation(
                "{Plugin}'s files were still in use, so they are emptied next time.",
                row.Id);
        } else {
            _log.LogWarning(
                "{Plugin}'s folder could not be deleted; it is still at {Folder}.",
                row.Id, row.Folder);
        }

        Refresh();
    }

    /// <summary>
    /// Deletes a folder outright, and says whether it did.
    /// </summary>
    /// <param name="folder">The folder to delete.</param>
    /// <returns>Whether the folder is gone.</returns>
    private static bool Delete(string folder) {
        try {
            Directory.Delete(folder, recursive: true);

            return true;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return false;
        }
    }

    /// <summary>
    /// Moves a folder that could not be deleted out of the plugins folder and
    /// into a bin, where the next run empties it.
    /// </summary>
    /// <param name="folder">The folder to move.</param>
    /// <param name="pluginId">The plugin it belongs to, for the new name.</param>
    /// <returns>Whether the folder moved.</returns>
    private static bool Bin(string folder, string pluginId) {
        try {
            var bin = Path.Combine(AppFolder.Root, "bin");
            Directory.CreateDirectory(bin);

            var name = $"{pluginId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            Directory.Move(folder, Path.Combine(bin, name));

            return true;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return false;
        }
    }
}
