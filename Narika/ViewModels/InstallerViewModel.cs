using CommunityToolkit.Mvvm.Input;
using Narika.Services.Install;
using Narika.ViewModels.Pages;

namespace Narika.ViewModels;

/// <summary>
/// The whole wizard: the pages in the order they are walked, which page is
/// showing, and the three doors at the bottom of every installer — back,
/// forward, and out.
/// </summary>
/// <remarks>
/// One object owns the walk so the pages never have to know about each
/// other: a page is asked for, shown, and its arrival noticed here (which
/// is where the installation begins), and the buttons at the bottom are
/// enabled from what the current page says rather than from what the page
/// before it happened to leave behind. The install request is shared the
/// same way — the destination page writes the folder, the options page
/// writes the shortcuts, and the service is handed the one object all of
/// it collected by the time the walk reaches the page that runs.
///
/// The walk begins at the folder page rather than at an announcement:
/// the launcher opens this window at the first place a decision is asked
/// for, because a person who pressed the card had already decided.
/// </remarks>
public sealed partial class InstallerViewModel : ViewModelBase {
    /// <summary>
    /// The application this installer, today, installs. The catalog and
    /// the install service both answer to this name; when the launcher
    /// grows more apps it becomes one name among several rather than the
    /// only one.
    /// </summary>
    public const string AppId = "shiranuix";

    private readonly IReadOnlyList<ViewModelBase> _pages;
    private readonly IAppLauncher _launcher;
    private readonly IInstallState _state;
    private readonly RelayCommand _back;
    private readonly RelayCommand _next;

    private int _step;

    /// <summary>
    /// Creates the wizard over its pages and the two services its outer
    /// buttons reach for: the catalog that says what version is being set
    /// up, and the launcher the finish page may ask for at the very end.
    /// </summary>
    /// <param name="destination">The folder page, and the walk's first.</param>
    /// <param name="options">The shortcuts page.</param>
    /// <param name="progress">The installing page.</param>
    /// <param name="finish">The closing page.</param>
    /// <param name="releases">Where the version on the folder line comes from.</param>
    /// <param name="launcher">What the finish page may start.</param>
    /// <param name="state">
    /// What the launcher behind this window will read when it closes:
    /// finished setup records the installation here, which is how the
    /// card's press starts offering Launch rather than the wizard
    /// without ever having watched the wizard walk.
    /// </param>
    public InstallerViewModel(
        DestinationPageViewModel destination,
        OptionsPageViewModel options,
        ProgressPageViewModel progress,
        FinishPageViewModel finish,
        IReleaseCatalog releases,
        IAppLauncher launcher,
        IInstallState state) {
        ArgumentNullException.ThrowIfNull(releases);

        _pages = [destination, options, progress, finish];
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _state = state ?? throw new ArgumentNullException(nameof(state));

        Destination = destination;
        Progress = progress;
        Finish = finish;

        _back = new RelayCommand(GoBack, () => CanGoBack);
        _next = new RelayCommand(GoForward, () => CanGoNext);
        CancelCommand = new RelayCommand(Cancel);

        // The buttons at the bottom answer to the pages themselves: a
        // destination being typed changes what forward will take, and a
        // finished installation changes it again.
        destination.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(DestinationPageViewModel.HasDestination)) {
                _next.NotifyCanExecuteChanged();
            }
        };
        progress.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(ProgressPageViewModel.IsFinished)) {
                Refresh();
            }
        };

        // The version arrives whether or not anybody waits for it: the
        // folder page's own line is already standing and simply gains its
        // number when the catalog answers. A catalog that fails leaves the
        // line plain rather than taking the wizard down with it.
        _ = LoadVersionAsync(destination, releases);
    }

    /// <summary>
    /// The folder page, and the walk's first — kept in reach because
    /// forward over it is the one forward that is ever refused.
    /// </summary>
    public DestinationPageViewModel Destination { get; }

    /// <summary>
    /// The closing page, kept in reach for the last question it asks.
    /// </summary>
    public FinishPageViewModel Finish { get; }

    /// <summary>
    /// The installing page, kept in reach so Cancel can stop what it runs.
    /// </summary>
    public ProgressPageViewModel Progress { get; }

    /// <summary>
    /// The page being shown now.
    /// </summary>
    public object CurrentPage => _pages[_step];

    /// <summary>
    /// What the forward button says on this page: Next while there is
    /// somewhere to go, Install on the page that starts the work, and
    /// Finish when there is nothing left but to close.
    /// </summary>
    public string NextContent => _step switch {
        1 => "Install",
        2 => Progress.IsFinished ? "Next" : "Installing...",
        3 => "Finish",
        _ => "Next"
    };

    /// <summary>
    /// Raised when the wizard is done and the window should go — whether
    /// everything was installed or the person pressed Cancel.
    /// </summary>
    public event EventHandler? ShutdownRequested;

    /// <summary>
    /// One page back, offered only where a page back means anything.
    /// </summary>
    public IRelayCommand BackCommand => _back;

    /// <summary>
    /// One page forward, offered only when the page being left has an
    /// answer worth taking.
    /// </summary>
    public IRelayCommand NextCommand => _next;

    /// <summary>
    /// Out: stops any installation running and asks the window to close.
    /// </summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>
    /// Whether back is offered: somewhere to go back to, and not the
    /// installing page — a walk backwards out of a half-written
    /// installation is a question for Cancel, not for Back.
    /// </summary>
    private bool CanGoBack => _step > 0 && _step != 2;

    /// <summary>
    /// Whether forward is offered, which is the current page's to answer:
    /// every page but the folder page always has one, and the folder page
    /// has one once it names a folder — which, being the walk's first, is
    /// also where forward is refused at the very start.
    /// </summary>
    private bool CanGoNext => _step switch {
        0 => Destination.HasDestination,
        2 => Progress.IsFinished,
        _ => true
    };

    private void GoBack() {
        if (CanGoBack) {
            EnterStep(_step - 1);
        }
    }

    private void GoForward() {
        if (!CanGoNext) {
            return;
        }

        if (_step == _pages.Count - 1) {
            FinishUp();
        } else {
            EnterStep(_step + 1);
        }
    }

    /// <summary>
    /// Out: whatever the install is doing is asked to stop, and the
    /// window is asked to close — a cancelled installation is left as it
    /// was found, which is the one thing Cancel has ever promised.
    /// </summary>
    private void Cancel() {
        Progress.Cancel();
        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves to a page and notices everything that arrival changes: which
    /// page stands here, what the buttons below now say and whether they
    /// are offered at all — and, for the installing page, the arrival is
    /// what starts the installation.
    /// </summary>
    private void EnterStep(int step) {
        _step = step;
        Refresh();

        if (CurrentPage is ProgressPageViewModel progress) {
            progress.BeginInstall();
        }
    }

    /// <summary>
    /// Raises every name the bottom bar binds to, in one place rather than
    /// beside each of the four ways a step can change.
    /// </summary>
    private void Refresh() {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(NextContent));
        _back.NotifyCanExecuteChanged();
        _next.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// The wizard's last act: record what was installed — whether or not
    /// the finish page also asks for it to be started — then close the
    /// window either way. The record comes before the start because it is
    /// the thing the launcher reads after this window is gone; a program
    /// that declines to open does not un-install itself.
    /// </summary>
    private void FinishUp() {
        if (Progress.Outcome is { } outcome) {
            _state.MarkInstalled(outcome.ExecutablePath);

            if (Finish.LaunchWhenDone) {
                // Fire-and-forget with the failure swallowed on purpose:
                // the window is closing, and a program that refuses to
                // start must not leave a dialog behind saying the wizard
                // never ended.
                _ = LaunchQuietlyAsync(outcome.ExecutablePath);
            }
        }

        ShutdownRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Starts the installed program, swallowing any failure — see above.
    /// </summary>
    private async Task LaunchQuietlyAsync(string executablePath) {
        try {
            await _launcher.LaunchAsync(executablePath);
        } catch {
            // There is nobody left to tell: the wizard is on its way out.
        }
    }

    /// <summary>
    /// Asks the catalog what version is current and says it on the folder
    /// page's own line.
    /// </summary>
    private async Task LoadVersionAsync(DestinationPageViewModel destination, IReleaseCatalog releases) {
        try {
            var release = await releases.ResolveAsync(AppId);
            destination.Version = release.Version;
        } catch {
            // A wizard that cannot ask the version still installs; the
            // folder page's line simply goes on without its number.
        }
    }
}
