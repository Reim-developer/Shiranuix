using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Graphs.Views;

namespace Shiranuix.Graphs;

/// <summary>
/// Shows the relationship map in its own window, beside the writing.
/// </summary>
/// <remarks>
/// The same walk as the settings and the reader: modeless, one window kept
/// while it stands, a fresh one after it closes. Each opening builds the
/// window's state anew and loads it from the store first, so what comes
/// back up is whatever the database holds by then — not what some closed
/// window once saw.
/// </remarks>
public sealed class GraphWindowService : IGraphWindowService {
    private readonly IServiceProvider _services;
    private GraphWindow? _window;

    /// <summary>
    /// Creates the service over the container, which builds each opening's
    /// state at the moment it is actually asked for.
    /// </summary>
    /// <param name="services">The container.</param>
    public GraphWindowService(IServiceProvider services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public void Show() {
        if (_window is { IsVisible: true }) {
            _window.Activate();
            return;
        }

        var state = _services.GetRequiredService<GraphState>();
        state.Load();

        _window = new GraphWindow(state);
        _window.Closed += (_, _) => {
            // The map lets go of the wiki when it goes: the wiki is a
            // standing thing, and a map that has been closed should not
            // stay in its memory.
            state.Dispose();
            _window = null;
        };

        _window.Show();
    }

    /// <inheritdoc />
    public Window? Current => _window is { IsVisible: true } ? _window : null;
}
