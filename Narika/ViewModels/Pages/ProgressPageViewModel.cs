using CommunityToolkit.Mvvm.ComponentModel;
using Narika.Services.Install;

namespace Narika.ViewModels.Pages;

/// <summary>
/// The page the installation runs on: the words of the service as they
/// arrive, the bar under them, and the way forward once it is over.
/// </summary>
public sealed partial class ProgressPageViewModel : ViewModelBase {
    private readonly IInstallService _install;
    private readonly InstallOptions _options;
    private readonly CancellationTokenSource _cancellation = new();

    private bool _started;
    private Task _installing = Task.CompletedTask;

    /// <summary>
    /// Creates the page over the service that will do the work and the
    /// options the work is done with.
    /// </summary>
    /// <param name="install">The install service.</param>
    /// <param name="options">The install request, shared with the wizard.</param>
    public ProgressPageViewModel(IInstallService install, InstallOptions options) {
        _install = install ?? throw new ArgumentNullException(nameof(install));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// How far along the installation is, nought to a hundred, or a
    /// negative number while it is running and cannot say.
    /// </summary>
    [ObservableProperty]
    private int percent = -1;

    /// <summary>
    /// What the service is doing, in its own words.
    /// </summary>
    [ObservableProperty]
    private string message = "Waiting to start...";

    /// <summary>
    /// Whether the installation has finished and the wizard may move on.
    /// </summary>
    [ObservableProperty]
    private bool isFinished;

    /// <summary>
    /// What the installation left behind, once there is anything: null
    /// until the service answers, which is what keeps the finish page from
    /// offering to start something that was never made.
    /// </summary>
    public InstallOutcome? Outcome { get; private set; }

    /// <summary>
    /// The running installation, so a test — or anything else that must
    /// wait rather than guess — has the task to wait on.
    /// </summary>
    public Task Installing => _installing;

    /// <summary>
    /// The bar's number: a negative percent means nothing yet, and a bar
    /// is drawn from zero rather than from below it.
    /// </summary>
    public int PercentDisplay => Math.Max(Percent, 0);

    /// <summary>
    /// Whether the bar should sweep instead of fill — before the first
    /// honest fraction arrives, and never after the work is done.
    /// </summary>
    public bool IsIndeterminate => !IsFinished && Percent < 0;

    /// <summary>
    /// Starts the installation, once: the wizard calls this on arriving at
    /// the page, and a second arrival — there has not been one yet — would
    /// find the same run still going rather than beginning another.
    /// </summary>
    public void BeginInstall() {
        if (_started) {
            return;
        }

        _started = true;
        _installing = InstallAsync();
    }

    /// <summary>
    /// Asks the installation to stop. Safe to ask whether or not it is
    /// running: a cancellation nobody waits on costs nothing.
    /// </summary>
    public void Cancel() {
        // The source is disposed with the view model; asking a disposed
        // one to cancel is a question about a page that is already gone.
        if (!_cancellation.IsCancellationRequested) {
            _cancellation.Cancel();
        }
    }

    /// <summary>
    /// The run itself: reports come back on the thread that reports them,
    /// the service decides when it is finished, and a cancellation is not
    /// a failure — it is the person having changed their mind.
    /// </summary>
    private async Task InstallAsync() {
        try {
            var reports = new InlineProgress<InstallProgress>(note => {
                Percent = note.Percent;
                Message = note.Message;
            });

            Outcome = await _install.InstallAsync(_options, reports, _cancellation.Token);
            Percent = 100;
            Message = "Installation complete.";
            IsFinished = true;
        } catch (OperationCanceledException) {
            Message = "Installation cancelled.";
        }
    }

    /// <inheritdoc />
    partial void OnPercentChanged(int value) {
        OnPropertyChanged(nameof(PercentDisplay));
        OnPropertyChanged(nameof(IsIndeterminate));
    }

    /// <inheritdoc />
    partial void OnIsFinishedChanged(bool value) =>
        OnPropertyChanged(nameof(IsIndeterminate));
}
