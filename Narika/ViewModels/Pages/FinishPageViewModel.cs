using CommunityToolkit.Mvvm.ComponentModel;
using Narika.Services.Install;

namespace Narika.ViewModels.Pages;

/// <summary>
/// The last page: the work is over, and the only question left is whether
/// to open what was just installed.
/// </summary>
public sealed partial class FinishPageViewModel : ViewModelBase {
    private readonly InstallOptions _options;

    /// <summary>
    /// Creates the page over the options the wizard installed with, so it
    /// can say where things ended up.
    /// </summary>
    /// <param name="options">The install request the wizard ran with.</param>
    public FinishPageViewModel(InstallOptions options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Whether the wizard is asked to start the program as its last act.
    /// Offered, never assumed — the person who just installed a thing may
    /// have other things to do first.
    /// </summary>
    [ObservableProperty]
    private bool launchWhenDone = true;

    /// <summary>
    /// The folder everything was installed into, which the page names so
    /// the person knows where their next question should be pointed.
    /// </summary>
    public string InstalledTo => _options.Destination;
}
