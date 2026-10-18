using Microsoft.Extensions.DependencyInjection;
using Shiranuix.ViewModels;
using Shiranuix.Views;

namespace Shiranuix.Services;

/// <summary>
/// Shows papers in the reading window, beside the writing.
/// </summary>
/// <remarks>
/// Modeless, the way the settings window is: the writer reads and writes at
/// the same time. The window is remembered while it stands — an open of
/// another paper turns the same window to it rather than stacking another —
/// and is remade after it closes, because a closed window cannot be shown
/// again. A paper no reader can carry is an outcome with a reason, the same
/// shape a document's failure travels in.
/// </remarks>
public sealed class PdfViewerWindowService : IPdfViewerService {
    private readonly IServiceProvider _services;
    private PdfViewerWindow? _window;

    /// <summary>
    /// Creates the service over the container, which builds each paper's
    /// reading when it is actually asked for.
    /// </summary>
    /// <param name="services">The container.</param>
    public PdfViewerWindowService(IServiceProvider services) =>
        _services = services ?? throw new ArgumentNullException(nameof(services));

    /// <inheritdoc />
    public DocumentOutcome Show(string path) {
        ArgumentNullException.ThrowIfNull(path);

        try {
            var paper = _services.GetRequiredService<Func<string, PdfViewerState>>()(path);

            // Reading continues in the same window: the one standing turns to
            // the new paper, and a closed window is remade rather than shown.
            if (_window is { IsVisible: true }) {
                _window.Read(paper);
                _window.Activate();
            } else {
                _window = new PdfViewerWindow(paper);
                _window.Show();
            }

            return DocumentOutcome.Done;
        } catch (Exception flaw) {
            return DocumentOutcome.Failed(flaw.Message);
        }
    }
}
