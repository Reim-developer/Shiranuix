using CommunityToolkit.Mvvm.ComponentModel;
using Narika.Services.Install;

namespace Narika.ViewModels.Pages;

/// <summary>
/// The wizard's opening page: what is being set up, and the way forward.
/// </summary>
public sealed partial class WelcomePageViewModel : ViewModelBase {
    /// <summary>
    /// The release number, filled in from the catalog once the wizard has
    /// one to say. Empty until then, so the page reads as a plain setup
    /// line rather than inventing a number.
    /// </summary>
    [ObservableProperty]
    private string version = string.Empty;

    /// <summary>
    /// The line the page prints under the heading: the version when the
    /// catalog has answered, nothing grander when it has not.
    /// </summary>
    public string VersionLine =>
        string.IsNullOrEmpty(Version)
            ? "The setup will install Shiranuix on this computer."
            : $"Version {Version} — the setup will install Shiranuix on this computer.";

    /// <inheritdoc />
    partial void OnVersionChanged(string value) =>
        OnPropertyChanged(nameof(VersionLine));
}
