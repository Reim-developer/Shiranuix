using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Narika.Services.Download;
using Narika.Services.Install;
using Narika.Services.Setup;
using Narika.Services.Theme;

namespace Narika.ViewModels;

/// <summary>
/// The launcher's one screen: what the repository's first application is,
/// whether it is on this machine yet, and the three things a person can
/// do about that — fetch it, start it, or put it right.
/// </summary>
/// <remarks>
/// The card is state-driven rather than button-driven. Nothing installed
/// means Download is the door and the other two have nowhere to go;
/// installed flips it — Launch takes over as the accent, Repair sits beside
/// it, and Download has nothing left to fetch. The same walk through the
/// download service stands under both of the first two: fetching because
/// there is nothing, fetching because what is there has broken, is one
/// errand with two reasons. Download then opens the wizard, while Repair
/// walks the install itself over the folder the receipt names — putting the
/// files back rather than only fetching the bytes that would — and stays
/// exactly where it is when it is done.
/// </remarks>
public sealed partial class LauncherViewModel : ViewModelBase {
    private readonly IInstallState _state;
    private readonly IDownloadService _download;
    private readonly ISetupWindowService _setup;
    private readonly IAppLauncher _launcher;
    private readonly IInstallService _install;
    private readonly InstallOptions _options;

    private CancellationTokenSource? _cancellation;

    /// <summary>
    /// Creates the launcher over everything its card and its buttons
    /// reach for.
    /// </summary>
    /// <param name="state">Whether the application is installed.</param>
    /// <param name="download">The fetch behind Download and Repair.</param>
    /// <param name="releases">Where the version on the card comes from.</param>
    /// <param name="launcher">What Launch starts the program with.</param>
    /// <param name="setup">The wizard Download opens when the fetch lands.</param>
    /// <param name="install">The work Repair runs over its own fetch.</param>
    /// <param name="options">
    /// The install request Repair walks, over the folder the receipt names.
    /// </param>
    /// <param name="theme">The theme toggle in the header.</param>
    public LauncherViewModel(
        IInstallState state,
        IDownloadService download,
        IReleaseCatalog releases,
        IAppLauncher launcher,
        ISetupWindowService setup,
        IInstallService install,
        InstallOptions options,
        ThemeService theme) {
        ArgumentNullException.ThrowIfNull(releases);

        _state = state ?? throw new ArgumentNullException(nameof(state));
        _download = download ?? throw new ArgumentNullException(nameof(download));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _install = install ?? throw new ArgumentNullException(nameof(install));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));

        DownloadCommand = new AsyncRelayCommand(
            () => FetchAsync(repair: false), CanFetch);
        RepairCommand = new AsyncRelayCommand(
            () => FetchAsync(repair: true), CanRepair);
        LaunchCommand = new AsyncRelayCommand(LaunchAsync, CanLaunch);
        CancelCommand = new RelayCommand(Cancel, () => IsWorking);

        // The two answers that move the buttons both arrive as events:
        // the wizard finishing its walk writes the installed state, and
        // its window closing re-enables whatever it had frozen — each one
        // re-asked of every command rather than remembered beside it.
        _state.Changed += (_, _) => RefreshCommands();
        _setup.Closed += (_, _) => {
            StatusMessage = _state.IsInstalled
                ? "Installed — Shiranuix is ready to use."
                : "Setup closed — nothing was installed.";
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
    /// What the application is, in the two sentences its own README opens
    /// with — the launcher describes what it sells, not what it does.
    /// </summary>
    public string AppDescription =>
        "A small plain-text editor for Windows, kept for one writer. " +
        "It opens ordinary .txt files offline and carries its own file " +
        "tree — no account, no cloud, no Ribbon.";

    /// <summary>
    /// The release number on the card, filled in from the catalog once it
    /// answers; empty until then rather than a number anyone guessed.
    /// </summary>
    [ObservableProperty]
    private string version = string.Empty;

    /// <summary>
    /// Whether the application is on this machine — the one fact every
    /// button's permission is derived from.
    /// </summary>
    public bool IsInstalled => _state.IsInstalled;

    /// <summary>
    /// The badge beside the name: the same fact, said out loud.
    /// </summary>
    public string StatusBadge => IsInstalled ? "Installed" : "Not installed";

    /// <summary>
    /// Whether a fetch is running — the one thing that holds every other
    /// button still while it does.
    /// </summary>
    [ObservableProperty]
    private bool isWorking;

    /// <summary>
    /// How far along the fetch is, nought to a hundred, or a negative
    /// number while it runs and cannot honestly say.
    /// </summary>
    [ObservableProperty]
    private int percent = -1;

    /// <summary>
    /// What the fetch is doing, in the service's own words.
    /// </summary>
    [ObservableProperty]
    private string workMessage = string.Empty;

    /// <summary>
    /// The status line under the buttons: what just happened, in the few
    /// words a person reads while their finger is already moving.
    /// </summary>
    [ObservableProperty]
    private string statusMessage = "Ready to install.";

    /// <summary>
    /// Fetches the release and opens the wizard over it — the door for a
    /// machine that has nothing yet.
    /// </summary>
    public IAsyncRelayCommand DownloadCommand { get; }

    /// <summary>
    /// Fetches the release again to put a broken installation right,
    /// staying where it is when it is done.
    /// </summary>
    public IAsyncRelayCommand RepairCommand { get; }

    /// <summary>
    /// Starts what is installed.
    /// </summary>
    public IAsyncRelayCommand LaunchCommand { get; }

    /// <summary>
    /// Stops the fetch that is running; offered only while one is.
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

    /// <summary>
    /// The whole fetch, run under either name: it reports as it goes,
    /// offers itself to be cancelled, and at the far end either opens the
    /// wizard — for the reason Download was pressed — or stays put and
    /// says the repair is over.
    /// </summary>
    private async Task FetchAsync(bool repair) {
        if (IsWorking) {
            return;
        }

        IsWorking = true;
        Percent = -1;
        WorkMessage = "Starting...";
        StatusMessage = repair
            ? "Repairing Shiranuix..."
            : "Downloading Shiranuix...";
        RefreshCommands();

        _cancellation = new CancellationTokenSource();

        try {
            var reports = new InlineProgress<InstallProgress>(note => {
                Percent = note.Percent;
                WorkMessage = note.Message;
            });

            await _download.DownloadAsync(reports, _cancellation.Token);

            if (repair) {
                StatusMessage = "Putting the files back where they were...";

                // The download's own bar hands straight over to the
                // install's: one person, one bar, the same errand in two
                // halves, and the install walks the copy this fetch just
                // put down rather than fetching anything again.
                await RepairAsync(reports, _cancellation.Token);

                StatusMessage = "Repair complete — Shiranuix is ready to use.";
            } else {
                StatusMessage = "Download complete — opening setup...";
                _setup.Show();
            }
        } catch (OperationCanceledException) {
            StatusMessage = repair ? "Repair cancelled." : "Download cancelled.";
        } catch {
            StatusMessage = repair
                ? "The repair failed — please try again."
                : "The download failed — please try again.";
        } finally {
            IsWorking = false;
            RefreshCommands();
        }
    }

    /// <summary>
    /// The second half of a repair: the files are put back over the ones
    /// that are there, into the folder the receipt names — which is where
    /// the installation already lives, so a repair never quietly moves the
    /// program — and the record is written again with whatever path came
    /// out the far end.
    /// </summary>
    private async Task RepairAsync(
        IProgress<InstallProgress> reports,
        CancellationToken cancellation) {
        if (_state.ExecutablePath is { } installed
            && Path.GetDirectoryName(installed) is { Length: > 0 } folder) {
            _options.Destination = folder;
        }

        var outcome = await _install.InstallAsync(_options, reports, cancellation);
        _state.MarkInstalled(outcome.ExecutablePath);
    }

    /// <summary>
    /// Starts the installed program; the status line says so either way,
    /// because a launch that fails silently is worse than one that says
    /// it did.
    /// </summary>
    private async Task LaunchAsync() {
        if (IsWorking || _state.ExecutablePath is not { } path) {
            return;
        }

        try {
            await _launcher.LaunchAsync(path);
            StatusMessage = "Shiranuix is running.";
        } catch {
            StatusMessage = "Couldn't start Shiranuix — try Repair.";
        }
    }

    private void Cancel() {
        // The source is a per-run one, and asking twice costs nothing:
        // the second Cancel on an already-cancelled source is a no-op, and
        // a null one — no fetch has begun — cancels nothing at all.
        _cancellation?.Cancel();
    }

    private bool CanFetch() => !IsWorking && !IsInstalled && !_setup.IsOpen;

    private bool CanRepair() => !IsWorking && IsInstalled && !_setup.IsOpen;

    private bool CanLaunch() => !IsWorking && IsInstalled && !_setup.IsOpen;

    /// <summary>
    /// Re-asks every command whether it is still offered — one place for
    /// the four ways the answers can move (busy, installed, the wizard
    /// standing open, a fetch's own start and end).
    /// </summary>
    private void RefreshCommands() {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(StatusBadge));

        DownloadCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
        LaunchCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Asks the catalog what version is current and says it on the card.
    /// </summary>
    private async Task LoadVersionAsync(IReleaseCatalog releases) {
        try {
            var release = await releases.ResolveAsync(InstallerViewModel.AppId);
            Version = release.Version;
        } catch {
            // A card that cannot ask the version still offers everything
            // else; the number simply stays off it.
        }
    }

    /// <inheritdoc />
    partial void OnIsWorkingChanged(bool value) {
        OnPropertyChanged(nameof(IsIndeterminate));

        // Cancel is the one door that opens and closes with the fetch
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
