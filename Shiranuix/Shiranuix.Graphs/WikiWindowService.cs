using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Graphs.Views;

namespace Shiranuix.Graphs;

/// <summary>
/// Shows the wiki in its own window, beside the writing.
/// </summary>
/// <remarks>
/// The same walk as the settings, the reader and the map: modeless, one
/// window kept while it stands, a fresh one after it closes. Each opening
/// builds the window's state anew and loads it from the store first, so
/// what comes back up is whatever the wiki holds by then — not what some
/// closed window once saw.
/// </remarks>
public sealed class WikiWindowService : IWikiWindowService {
    private readonly IServiceProvider _services;
    private WikiWindow? _window;

    /// <summary>
    /// Creates the service over the container, which builds each opening's
    /// state at the moment it is actually asked for.
    /// </summary>
    /// <param name="services">The container.</param>
    public WikiWindowService(IServiceProvider services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public void Show() {
        if (_window is { IsVisible: true }) {
            _window.Activate();
            return;
        }

        var state = _services.GetRequiredService<WikiState>();
        state.Load();

        _window = new WikiWindow(state);

        // The wiki's own question stands over the wiki's own window: the
        // prompt is the application's, but where it stands is the point.
        state.Owner = _window;

        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    /// <inheritdoc />
    public Window? Current => _window is { IsVisible: true } ? _window : null;
}
