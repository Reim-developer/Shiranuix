using CommunityToolkit.Mvvm.ComponentModel;
using Narika.Services.Install;

namespace Narika.ViewModels.Pages;

/// <summary>
/// The page that asks which of the machine's surfaces may be marked: a
/// desktop icon and a start menu entry, both offered rather than assumed.
/// </summary>
public sealed partial class OptionsPageViewModel : ViewModelBase {
    private readonly InstallOptions _options;

    /// <summary>
    /// Creates the page over the options it writes into.
    /// </summary>
    /// <param name="options">The install request, shared with the wizard.</param>
    public OptionsPageViewModel(InstallOptions options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Whether an icon is left on the desktop.
    /// </summary>
    [ObservableProperty]
    private bool createDesktopShortcut = true;

    /// <summary>
    /// Whether an entry is left in the start menu.
    /// </summary>
    [ObservableProperty]
    private bool createStartMenuShortcut = true;

    /// <inheritdoc />
    partial void OnCreateDesktopShortcutChanged(bool value) =>
        _options.CreateDesktopShortcut = value;

    /// <inheritdoc />
    partial void OnCreateStartMenuShortcutChanged(bool value) =>
        _options.CreateStartMenuShortcut = value;
}
