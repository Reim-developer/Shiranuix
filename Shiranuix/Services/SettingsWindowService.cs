using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Views;

namespace Shiranuix.Services;

/// <summary>
/// Shows the settings window: modeless, so the theme it changes is the theme
/// being looked at while it is changed.
/// </summary>
/// <remarks>
/// Unlike the dialogs, which are gone the moment they are answered, settings
/// are revisited: the window is remembered while it is open — a second press
/// of the button brings that one forward rather than stacking another — and
/// is re-made after it closes, because a closed window cannot be shown again.
/// </remarks>
public sealed class SettingsWindowService : ISettingsWindowService {
    private readonly IServiceProvider _services;
    private Window? _window;

    /// <summary>
    /// Creates the service over the container, which builds each window.
    /// </summary>
    /// <param name="services">The container; a window is asked for only at
    /// the moment settings are actually opened.</param>
    public SettingsWindowService(IServiceProvider services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public void Show() {
        if (_window is { IsVisible: true }) {
            _window.Activate();
            return;
        }

        _window = _services.GetRequiredService<SettingsWindow>();
        _window.Show();
    }
}
