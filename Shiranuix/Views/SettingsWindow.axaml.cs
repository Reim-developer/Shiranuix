using Avalonia.Controls;
using Avalonia.Input;
using Shiranuix.ViewModels;
using Splat;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the settings window.
/// </summary>
/// <remarks>
/// The window arrives with the state whose settings it edits already in hand,
/// and everything inside is bound to it — picking a theme is a TwoWay
/// binding, and the application applies it the moment it changes. The only
/// rule here is the one no binding carries: Escape closes.
/// </remarks>
public partial class SettingsWindow : Window {
    /// <summary>
    /// Creates the window over the state whose settings it edits.
    /// </summary>
    /// <param name="typography">The document's typography and theme.</param>
    public SettingsWindow(TypographyState typography) {
        InitializeComponent();
        DataContext = typography ?? throw new ArgumentNullException(nameof(typography));

        WireEscapeToClose();
    }

    /// <summary>
    /// The constructor the XAML previewer and the runtime loader build the
    /// window with.
    /// </summary>
    /// <remarks>
    /// A view without a parameterless constructor is one the previewer refuses
    /// to build and the loader warns about, so this overload exists for them —
    /// and it does the only thing that can be done with no container to ask:
    /// take the state out of Splat, where the application put it the moment it
    /// created the container. The running application never takes this path:
    /// the container builds the window through the constructor above.
    /// </remarks>
    public SettingsWindow() {
        InitializeComponent();
        DataContext = AppLocator.Current.GetService<TypographyState>();

        WireEscapeToClose();
    }

    private void WireEscapeToClose() =>
        KeyDown += (_, e) => {
            if (e.Key == Key.Escape) {
                Close();
            }
        };
}
