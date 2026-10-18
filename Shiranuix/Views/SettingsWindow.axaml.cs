using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Shiranuix.ViewModels;
using Splat;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the settings window.
/// </summary>
/// <remarks>
/// The window arrives with the state whose settings it edits already in
/// hand, and almost everything inside is bound to it: picking a theme is
/// a TwoWay binding, and the application applies it the moment it changes.
/// Two rules are here because no binding carries them: Escape closes, and
/// a chord being pressed for eats every key that comes while it waits —
/// the chords it asks about are the same ones the main window answers,
/// and a capture that let them through would answer twice.
/// </remarks>
public partial class SettingsWindow : Window {
    /// <summary>The keyboard state the section below the rows talks to.</summary>
    private KeyBindingState? _keys;

    /// <summary>The row whose chord is being pressed for, if one is.</summary>
    private KeyBindingRow? _capturing;

    /// <summary>
    /// Creates the window over the state whose settings it edits.
    /// </summary>
    /// <param name="typography">The document's typography and theme.</param>
    /// <param name="keys">What the window's chords answer to.</param>
    public SettingsWindow(TypographyState typography, KeyBindingState keys) {
        InitializeComponent();
        DataContext = typography ?? throw new ArgumentNullException(nameof(typography));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));

        // The window itself is bound to the typography; the keyboard section
        // below keeps its own state, so it takes it in hand rather than
        // reading the window's.
        KeysList.DataContext = _keys;
        WireEscapeToClose();
        WireCapture();
        WindowChrome.Watch(this);
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

        _keys = AppLocator.Current.GetService<KeyBindingState>();
        if (_keys is not null) {
            KeysList.DataContext = _keys;
        }

        WireEscapeToClose();
        WireCapture();
        WindowChrome.Watch(this);
    }

    /// <summary>
    /// Opens a chip for its chord: the row waits for the next keys.
    /// </summary>
    private void OnBindChordClick(object? sender, RoutedEventArgs e) {
        if (_keys is null || sender is not Button { DataContext: KeyBindingRow row }) {
            return;
        }

        // A second chip clicked while another waits: one row at a time is
        // being pressed for, and the first one hangs no further.
        StopCapture();
        _capturing = row;
        row.IsCapturing = true;
    }

    /// <summary>Takes a row's chord away.</summary>
    private void OnClearChordClick(object? sender, RoutedEventArgs e) {
        if (sender is not Button { DataContext: KeyBindingRow row }) {
            return;
        }

        StopCapture();
        _keys?.Clear(row.CommandId);
    }

    /// <summary>Puts every command back on the chord it started with.</summary>
    private void OnResetBindingsClick(object? sender, RoutedEventArgs e) {
        StopCapture();
        _keys?.ResetToDefaults();
    }

    /// <summary>Leaves the row's wait, whichever way it ended.</summary>
    private void StopCapture() {
        if (_capturing is { } row) {
            row.IsCapturing = false;
            _capturing = null;
        }
    }

    private void WireEscapeToClose() =>
        KeyDown += (_, e) => {
            if (e.Key != Key.Escape) {
                return;
            }

            // While a chord is being pressed for, Escape gives the press
            // back rather than closing the window: a writer who opened a
            // capture by mistake asked to leave the row, not the settings.
            if (_capturing is not null) {
                StopCapture();
                return;
            }

            Close();
        };

    /// <summary>
    /// Answers above the rows while a chip waits, so what is typed at the
    /// chip is taken before the chords it is asking about can run onward.
    /// </summary>
    private void WireCapture() =>
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);

    private void OnKeyDown(object? sender, KeyEventArgs e) {
        if (_capturing is not { } row || _keys is null) {
            return;
        }

        // From here the capture eats everything, including the Escape the
        // window already answered: a capture that let a chord through would
        // answer the press twice — once here, once on the main window the
        // chord belongs to.
        e.Handled = true;

        // Delete or Backspace on their own leave the command unassigned; a
        // press with a modifier along it is a chord attempt like any other,
        // and the state calls it what it is.
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Delete or Key.Back) {
            StopCapture();
            _keys.Clear(row.CommandId);
            return;
        }

        if (_keys.Assign(row.CommandId, e.Key, e.KeyModifiers)) {
            StopCapture();
        }

        // Not a chord: the press is eaten anyway, and the capture keeps
        // waiting for one that is.
    }
}
