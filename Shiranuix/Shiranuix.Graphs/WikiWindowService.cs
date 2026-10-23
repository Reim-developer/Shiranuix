using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Shiranuix.Graphs.Dialogs;
using Shiranuix.Graphs.Views;
using Shiranuix.WindowBorderless;

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
    private readonly IImagePicker? _picker;
    private WikiWindow? _window;

    /// <summary>
    /// Creates the service over the container, which builds each opening's
    /// state at the moment it is actually asked for.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="picker">Asks the writer for a picture, or nothing when there is no picker to ask.</param>
    public WikiWindowService(IServiceProvider services, IImagePicker? picker = null) {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _picker = picker;
    }

    /// <inheritdoc />
    public void Show() {
        if (_window is { IsVisible: true }) {
            _window.Activate();
            return;
        }

        var state = _services.GetRequiredService<WikiState>();
        state.Load();

        // The same freeze guard the editor keeps: a Vietnamese IME that can
        // hold no more of a composition commits it instead of freezing.
        _window = new WikiWindow(state, _picker, _services.GetService<IImeCompositionGuard>());

        // The wiki's own question stands over the wiki's own window: the
        // prompt is the application's, but where it stands is the point.
        state.Owner = _window;

        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    /// <inheritdoc />
    public Window? Current => _window is { IsVisible: true } ? _window : null;
}
