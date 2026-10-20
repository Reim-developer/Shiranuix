using System.Linq;
using Avalonia.Controls;
using Shiranuix.Persistence;
using Shiranuix.Services.Fonts;
using Shiranuix.ViewModels.Dialogs;

namespace Shiranuix.Services.Dialogs;

/// <summary>
/// Shows the missing typeface dialog as a window over the application's main
/// window.
/// </summary>
/// <remarks>
/// The same walk as the other window dialogs: the window arrives as a
/// <see cref="Func{Window}"/> rather than captured, because the container is
/// still building the window when it builds this. What is offered is put in
/// the order the toolbar's own font list is put in — the typefaces picked
/// lately first, then everything else this machine holds — with one
/// difference: only faces that are actually here are offered, since a dialog
/// asking which face to open in must not offer one that cannot be opened in.
/// </remarks>
public sealed class WindowMissingFontDialogService : IMissingFontDialogService {
    private readonly Func<Window> _window;
    private readonly IFontCatalog _fonts;
    private readonly IFontHistoryStore _history;

    /// <summary>
    /// Creates the service over a way of asking for the window and over the
    /// typefaces this machine carries.
    /// </summary>
    /// <param name="window">Resolves the window that owns the dialog.</param>
    /// <param name="fonts">The typefaces installed here.</param>
    /// <param name="history">The typefaces picked lately.</param>
    public WindowMissingFontDialogService(
        Func<Window> window,
        IFontCatalog fonts,
        IFontHistoryStore history) {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    /// <inheritdoc />
    public async Task<string?> AskAsync(
        string missing,
        double size,
        string sample,
        string initial) {
        ArgumentNullException.ThrowIfNull(missing);
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(initial);

        var offered = Installed();

        // A machine holding no typeface at all cannot be asked which one to
        // open in, so the file opens on its own answer — the same answer it
        // would get if the dialog had never been worth showing.
        if (offered.Count == 0) {
            return initial;
        }

        var dialog = new Views.MissingFontDialog(
            new MissingFontDialogViewModel(missing, size, sample, initial, offered));

        return await dialog.ShowDialog<string?>(_window());
    }

    /// <summary>
    /// The typefaces that are actually installed: the ones picked lately,
    /// then the rest, each appearing once.
    /// </summary>
    private IReadOnlyList<string> Installed() {
        var held = _fonts.GetFamilies();
        var present = new HashSet<string>(held, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>(_history.RecentFamilies().Where(present.Contains));
        ordered.AddRange(held.Where(family => !ordered.Contains(family, StringComparer.OrdinalIgnoreCase)));
        return ordered;
    }
}
