using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Narika.Services.Install;

namespace Narika.ViewModels.Pages;

/// <summary>
/// The page that names the folder the files go into.
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
    /// Asks the machine for a folder and takes the answer.
    /// </summary>
    public IRelayCommand BrowseCommand { get; }

    /// <inheritdoc />
    partial void OnDestinationPathChanged(string value) {
        _options.Destination = value;
        OnPropertyChanged(nameof(HasDestination));
    }

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
