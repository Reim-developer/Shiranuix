using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Narika;

namespace Narika.Services.Setup;

/// <summary>
/// The real setup windows: each <see cref="Show"/> asks the container for
/// another one, which is what keeps every walk starting from its first
/// page, and remembers it while it stands so the folder picker has a
/// window to belong to.
/// </summary>
/// <remarks>
/// The container arrives rather than the window: it builds this service
/// for the launcher, and builds the wizard window from the wizard's view
/// model, so asking for a window at construction time would send the
/// container round its own circle. Asking at <em>show</em> time — by which
/// point the wizard exists — breaks it, the same way the editor's file
/// pickers do.
/// </remarks>
public sealed class SetupWindowService : ISetupWindowService {
    private readonly IServiceProvider _services;

    /// <summary>
    /// Creates the service over the container the wizard is built from.
    /// </summary>
    /// <param name="services">The container.</param>
    public SetupWindowService(IServiceProvider services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public Window? CurrentWindow { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Closed;

    /// <inheritdoc />
    public void Show() {
        if (CurrentWindow is not null) {
            return;
        }

        var window = _services.GetRequiredService<SetupWindow>();

        CurrentWindow = window;
        IsOpen = true;

        window.Closed += (_, _) => {
            CurrentWindow = null;
            IsOpen = false;
            Closed?.Invoke(this, EventArgs.Empty);
        };

        window.Show();
    }
}
