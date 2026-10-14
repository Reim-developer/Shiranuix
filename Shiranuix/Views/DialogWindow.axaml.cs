using Avalonia.Controls;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// The window every dialog is drawn in.
/// </summary>
/// <remarks>
/// Holds the dialogs' chrome and nothing about any one dialog: the title,
/// message and buttons come from the view model, and the view model answers
/// through a delegate the service hands it. It is generic only in what it
/// shows — there is one window, and no markup for a second kind of prompt.
/// </remarks>
public partial class DialogWindow : Window {
    /// <summary>
    /// Creates an empty window; the content is set by the service showing it.
    /// </summary>
    public DialogWindow() => InitializeComponent();
}
