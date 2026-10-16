using Avalonia.Controls;
using Avalonia.Input;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the command palette.
/// </summary>
/// <remarks>
/// The box itself is bound; the only thing that cannot be is the keyboard.
/// While the palette is up the arrows and Enter belong to it rather than to
/// the page underneath, and Escape has to close the box and hand the caret
/// back to the document — none of which a binding can say, because a binding
/// cannot tell which control the key press is being offered to.
///
/// The keys are handled, not swallowed blindly: each one that is answered is
/// marked handled, and anything else goes on to the page as usual, so a
/// writer who reaches for a chord while the box is open still gets it.
/// </remarks>
public partial class PaletteView : UserControl {
    /// <summary>
    /// Creates the palette.
    /// </summary>
    public PaletteView() {
        InitializeComponent();
    }

    /// <summary>
    /// A key was pressed in the box: the palette answers the ones that are
    /// about navigating rows, and leaves the rest alone.
    /// </summary>
    /// <param name="sender">The box the press was reported on.</param>
    /// <param name="e">The press.</param>
    private void OnQueryKeyDown(object? sender, KeyEventArgs e) {
        if (DataContext is not PaletteState palette) {
            return;
        }

        switch (e.Key) {
            case Key.Down:
                palette.Down();
                e.Handled = true;
                break;
            case Key.Up:
                palette.Up();
                e.Handled = true;
                break;
            case Key.Enter:
                palette.Run();
                e.Handled = true;
                break;
            case Key.Escape:
                palette.Close();
                e.Handled = true;
                break;
            default:
                // Not the palette's to answer: the box keeps typing it.
                break;
        }
    }
}
