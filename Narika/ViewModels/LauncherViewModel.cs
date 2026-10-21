using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Narika.Services.Install;
using Narika.Services.Setup;
using Narika.Services.Theme;

namespace Narika.ViewModels;

/// <summary>
/// The launcher's one screen: what the repository's first application is,
/// and everything a person can do about it — which works the way a shop
/// window does: the picture and the words stand on show, and a press of
/// the card opens whatever stands behind them.
/// </summary>
/// <remarks>
/// The card is one picture and a few words, and pressing it is the whole
/// interface. What stands behind that press is the three doors an
/// installation has ever had: Download opens the wizard — the same door
/// for a machine with nothing and one putting a fresh copy over an old —
/// Repair walks the install service itself over the folder the receipt
/// names, and Uninstall takes everything back off the machine, asking
/// once before a door that final is opened. Repair and Uninstall are
/// offered even when there is nothing installed, standing closed rather
/// than absent, so the whole shape of what this window is for is one
/// press away at any time. While the wizard stands, every door here
/// waits for it to close.
/// </remarks>
public sealed partial class LauncherViewModel : ViewModelBase {
    private readonly IInstallState _state;
    private readonly ISetupWindowService _setup;
    private readonly IInstallService _install;
    private readonly IUninstallService _uninstall;
    private readonly InstallOptions _options;

    private CancellationTokenSource? _cancellation;
    private bool _uninstallConfirmed;

    /// <summary>
    /// Creates the launcher over everything its card and its panel reach
    /// for.
    /// </summary>
    /// <param name="state">Whether the application is installed.</param>
    /// <param name="releases">Where the version in the panel comes from.</param>
    /// <param name="setup">The wizard a press of Download opens.</param>
    /// <param name="install">The work Repair walks.</param>
    /// <param name="uninstall">The work Uninstall walks.</param>
    /// <param name="options">
    /// The install request Repair walks, over the folder the receipt names.
    /// </param>
    /// <param name="theme">The theme toggle in the header.</param>
    public LauncherViewModel(
        IInstallState state,
        IReleaseCatalog releases,
        ISetupWindowService setup,
        IInstallService install,
        IUninstallService uninstall,
        InstallOptions options,
        ThemeService theme) {
        ArgumentNullException.ThrowIfNull(releases);

        _state = state ?? throw new ArgumentNullException(nameof(state));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _install = install ?? throw new ArgumentNullException(nameof(install));
        _uninstall = uninstall ?? throw new ArgumentNullException(nameof(uninstall));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));

        DownloadCommand = new RelayCommand(Download, CanDownload);
        RepairCommand = new AsyncRelayCommand(RepairAsync, CanRepair);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, CanUninstall);
        CancelCommand = new RelayCommand(Cancel, () => IsWorking);
        OpenCommand = new RelayCommand(Open, CanOpen);

        // The two answers that move the buttons both arrive as events:
        // the wizard finishing its walk writes the installed state, and
        // its window closing re-enables whatever it had frozen — each one
        // re-asked of every command rather than remembered beside it. A
        // wizard that closed over an installation also opens the panel,
        // which is where the doors onto that installation stand.
        _state.Changed += (_, _) => RefreshCommands();
        _setup.Closed += (_, _) => {
            var installed = _state.IsInstalled;

            StatusMessage = installed
                ? "Installed — Shiranuix is ready to use."
                : "Setup closed — nothing was installed.";
            IsDetailsOpen = installed;
            _uninstallConfirmed = false;
            RefreshCommands();
        };

        _ = LoadVersionAsync(releases);
    }

    /// <summary>
    /// The theme toggle's own state and command, bound as a small object
    /// of its own rather than flattened into this one — it is a service
    /// the application also listens to, not a field of the launcher.
    /// </summary>
    public ThemeService Theme { get; }

    /// <summary>
    /// The one application this launcher, today, shows. The monorepo adds
    /// cards beside it; this is the first and only one for now.
    /// </summary>
    public string AppName => "Shiranuix";

    /// <summary>
    /// What the application is, in the one sentence its own README opens
    /// with — the launcher describes what it sells, not what it does.
    /// </summary>
    public string AppDescription =>
        "A simple text editor with its own document format.";

    /// <summary>
    /// The release number the panel says, filled in from the catalog once
    /// it answers; empty until then rather than a number anyone guessed.
    /// </summary>
    [ObservableProperty]
    private string version = string.Empty;

    /// <summary>
    /// Whether the application is on this machine — the one fact every
    /// button's permission is derived from.
    /// </summary>
    public bool IsInstalled => _state.IsInstalled;

    /// <summary>
    /// The badge inside the panel: the same fact, said out loud.
    /// </summary>
    public string StatusBadge => IsInstalled ? "Installed" : "Not installed";

    /// <summary>
    /// Whether the panel behind the card is showing — the place where what
    /// is on the machine, and the three doors onto it, stand.
    /// </summary>
    [ObservableProperty]
    private bool isDetailsOpen;

    /// <summary>
    /// Whether an install or removal walk is running — the one thing that
    /// holds every other button still while it does.
    /// </summary>
    [ObservableProperty]
    private bool isWorking;

    /// <summary>
    /// How far along the walk is, nought to a hundred, or a negative
    /// number while it runs and cannot honestly say.
    /// </summary>
    [ObservableProperty]
    private int percent = -1;

    /// <summary>
    /// What the walk is doing, in the service's own words.
    /// </summary>
    [ObservableProperty]
    private string workMessage = string.Empty;

    /// <summary>
    /// The status line inside the panel: what just happened, in the few
    /// words a person reads while their finger is already moving.
    /// </summary>
    [ObservableProperty]
    private string statusMessage = "Ready to install.";

    /// <summary>
    /// What a press of the whole card does: the panel, open or shut the
    /// way a card that is showing its own answers works. The whole
    /// interface is this one press and the three doors standing behind it.
    /// </summary>
    public IRelayCommand OpenCommand { get; }

    /// <summary>
    /// Opens the wizard: the door for a machine with nothing on it yet,
    /// and the same door walked again for one putting a fresh copy over
    /// an old one.
    /// </summary>
    public IRelayCommand DownloadCommand { get; }

    /// <summary>
    /// Walks the install again over the folder the receipt names, putting
    /// the files back rather than only fetching the bytes that would, and
    /// staying where it is when it is done.
    /// </summary>
    public IAsyncRelayCommand RepairCommand { get; }

    /// <summary>
    /// Takes the installation back off the machine — the marks, the files,
    /// the record — asking once before a door that final is opened.
    /// </summary>
    public IAsyncRelayCommand UninstallCommand { get; }

    /// <summary>
    /// Stops the walk that is running; offered only while one is.
    /// </summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// The bar's number: a negative percent means nothing yet, and a bar
    /// is drawn from zero rather than from below it.
    /// </summary>
    public int PercentDisplay => Math.Max(Percent, 0);

    /// <summary>
    /// Whether the bar sweeps rather than fills — before the first honest
    /// fraction arrives, and never once there is one.
    /// </summary>
    public bool IsIndeterminate => IsWorking && Percent < 0;

    private void Open() {
        IsDetailsOpen = !IsDetailsOpen;
    }

    private void Download() {
        _uninstallConfirmed = false;
        IsDetailsOpen = false;
        _setup.Show();
    }

    /// <summary>
    /// The whole repair: one walk through the install service, which
    /// fetches the bytes itself when no copy of this release is still
    /// lying about, and which reports every stretch of the way so the
    /// panel's bar never lies about where it is.
    /// </summary>
    private async Task RepairAsync() {
        if (IsWorking) {
            return;
        }

        _uninstallConfirmed = false;
        IsWorking = true;
        Percent = -1;
        WorkMessage = "Starting...";
        StatusMessage = "Repairing Shiranuix...";
        RefreshCommands();

        _cancellation = new CancellationTokenSource();

        try {
            var reports = new InlineProgress<InstallProgress>(note => {
                Percent = note.Percent;
                WorkMessage = note.Message;
            });

            // The walk goes over where the installation already lives —
            // the folder the receipt names — so a repair never quietly
            // moves the program somewhere else.
            if (_state.ExecutablePath is { } installed
                && Path.GetDirectoryName(installed) is { Length: > 0 } folder) {
                _options.Destination = folder;
            }

            var outcome = await _install.InstallAsync(_options, reports, _cancellation.Token);
            _state.MarkInstalled(outcome.ExecutablePath);
            StatusMessage = "Repair complete — Shiranuix is ready to use.";
        } catch (OperationCanceledException) {
            StatusMessage = "Repair cancelled.";
        } catch {
            StatusMessage = "The repair failed — please try again.";
        } finally {
            IsWorking = false;
            RefreshCommands();
        }
    }

    /// <summary>
    /// The whole removal, asked for twice before it runs: the first press
    /// is the one that says what will be taken, and only the second one
    /// takes it — a door this final deserves a hand that meant it. The
    /// walk itself takes the marks, the program's own folder, and the
    /// record, in that order, and the launcher stays exactly where it is
    /// with a line saying there is nothing left.
    /// </summary>
    private async Task UninstallAsync() {
        if (IsWorking || _state.ExecutablePath is not { } path) {
            return;
        }

        if (!_uninstallConfirmed) {
            _uninstallConfirmed = true;
            StatusMessage =
                "Uninstall takes Shiranuix off this machine — its files, its shortcuts, everything. Press Uninstall again to confirm.";
            return;
        }

        IsWorking = true;
        Percent = -1;
        WorkMessage = "Starting...";
        StatusMessage = "Uninstalling Shiranuix...";
        RefreshCommands();

        _cancellation = new CancellationTokenSource();

        try {
            var reports = new InlineProgress<InstallProgress>(note => {
                Percent = note.Percent;
                WorkMessage = note.Message;
            });

            await _uninstall.UninstallAsync(path, reports, _cancellation.Token);
            _state.Uninstall();
            StatusMessage = "Shiranuix was removed.";
        } catch (OperationCanceledException) {
            StatusMessage = "Uninstall cancelled.";
        } catch {
            StatusMessage =
                "The uninstall failed — some of it may still be on this machine.";
        } finally {
            _uninstallConfirmed = false;
            IsWorking = false;
            RefreshCommands();
        }
    }

    private void Cancel() {
        // The source is a per-run one, and asking twice costs nothing:
        // the second Cancel on an already-cancelled source is a no-op, and
        // a null one — no walk has begun — cancels nothing at all.
        _cancellation?.Cancel();
    }

    private bool CanOpen() => !IsWorking && !_setup.IsOpen;

    private bool CanDownload() => !IsWorking && !_setup.IsOpen;

    private bool CanRepair() => !IsWorking && IsInstalled && !_setup.IsOpen;

    private bool CanUninstall() => !IsWorking && IsInstalled && !_setup.IsOpen;

    /// <summary>
    /// Re-asks every command whether it is still offered — one place for
    /// the four ways the answers can move (busy, installed, the wizard
    /// standing open, a walk's own start and end).
    /// </summary>
    private void RefreshCommands() {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(StatusBadge));

        OpenCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Asks the catalog what version is current and says it in the panel.
    /// </summary>
    private async Task LoadVersionAsync(IReleaseCatalog releases) {
        try {
            var release = await releases.ResolveAsync(InstallerViewModel.AppId);
            Version = release.Version;
        } catch {
            // A panel that cannot ask the version still offers everything
            // else; the number simply stays off it.
        }
    }

    /// <inheritdoc />
    partial void OnIsWorkingChanged(bool value) {
        OnPropertyChanged(nameof(IsIndeterminate));

        // Cancel is the one door that opens and closes with the walk
        // itself: everything else waits for the state to change, and this
        // one changes the moment work does.
        CancelCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    partial void OnPercentChanged(int value) {
        OnPropertyChanged(nameof(PercentDisplay));
        OnPropertyChanged(nameof(IsIndeterminate));
    }
}
