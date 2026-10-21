using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Narika.Services.Install;

namespace Narika.ViewModels.Pages;

/// <summary>
/// The page that names the folder the files go into, and — since the
/// launcher opens the wizard here — the walk's first: it carries the
/// version line the wizard used to announce on a page of its own, because
/// a person who pressed the card did not come for an announcement.
/// </summary>
public sealed partial class DestinationPageViewModel : ViewModelBase {
    private readonly InstallOptions _options;
    private readonly IFolderPickerService _picker;

    /// <summary>
    /// Creates the page over the options it writes into and a way of
    /// asking the machine for a folder.
    /// </summary>
    /// <param name="options">The install request, shared with the wizard.</param>
    /// <param name="picker">The folder picker.</param>
    public DestinationPageViewModel(InstallOptions options, IFolderPickerService picker) {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));

        DestinationPath = options.Destination;
        BrowseCommand = new RelayCommand(Browse);
    }

    /// <summary>
    /// The folder, as it is being typed or was picked.
    /// </summary>
    [ObservableProperty]
    private string destinationPath = string.Empty;

    /// <summary>
    /// Whether the page has an answer the wizard may proceed on: a folder
    /// with anything in it, which is all a destination has to be.
    /// </summary>
    public bool HasDestination => !string.IsNullOrWhiteSpace(DestinationPath);

    /// <summary>
    /// The release number, filled in from the catalog once the wizard has
    /// one to say. Empty until then, so the page reads as a plain setup
    /// line rather than inventing a number.
    /// </summary>
    [ObservableProperty]
    private string version = string.Empty;

    /// <summary>
    /// The line under the heading: what this setup will install, and
    /// which release when the catalog has already named one.
    /// </summary>
    public string IntroLine => string.IsNullOrEmpty(Version)
        ? "Setup will install Shiranuix into the folder below."
        : $"Setup will install Shiranuix {Version} into the folder below.";

    /// <summary>
    /// Asks the machine for a folder and takes the answer.
    /// </summary>
    public IRelayCommand BrowseCommand { get; }

    /// <inheritdoc />
    partial void OnDestinationPathChanged(string value) {
        _options.Destination = value;
        OnPropertyChanged(nameof(HasDestination));
    }

    /// <inheritdoc />
    partial void OnVersionChanged(string value) =>
        OnPropertyChanged(nameof(IntroLine));

    /// <summary>
    /// Takes the picked folder and names the editor's own one inside it,
    /// which is where the files go: a person picks where programs go and
    /// expects every program to keep to its own door inside that.
    /// </summary>
    private async void Browse() {
        var picked = await _picker.PickFolderAsync();
        if (picked is not null) {
            DestinationPath = Path.Combine(picked, "Shiranuix");
        }
    }
}
