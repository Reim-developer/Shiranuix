using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Lucide.Avalonia;

namespace Shiranuix.Views;

/// <summary>
/// The windows' own chrome: the drag, the double press and the three glyphs
/// the ledge carries.
/// </summary>
/// <remarks>
/// <para>
/// The band a window hands to <see cref="Watch"/> moves it: a press from any
/// place nothing interactive sits under the pointer — a button, a picker, a
/// box somebody is writing in never counts — begins the system's own move of
/// the window, and a double press fills it, or stands a filled window back
/// ordinary again by the same answer.
/// </para>
/// <para>
/// The three glyphs named <c>CaptionMin</c>, <c>CaptionMax</c> and
/// <c>CaptionClose</c> rest, fill and leave the window, and the fill glyph
/// answers the state the window is in. The edge's light is read by the frame
/// itself — the <see cref="ChromeFrame"/> the markup draws carries the
/// window's states as its own pseudo-classes. What a window does not declare,
/// the chrome leaves out.
/// </para>
/// </remarks>
public static class WindowChrome {

    /// <summary>
    /// Wires a window's chrome: the band it moves by, its three caption
    /// buttons, and the fill glyph's answer to the window's own state.
    /// </summary>
    /// <param name="window">The window whose chrome this is.</param>
    /// <param name="dragArea">The band the window moves by.</param>
    public static void Watch(Window window, Control? dragArea) {
        if (window.FindControl<Button>("CaptionMin") is { } minimize) {
            minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
        }

        if (window.FindControl<Button>("CaptionMax") is { } maximize) {
            maximize.Click += (_, _) => window.WindowState =
                window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        if (window.FindControl<Button>("CaptionClose") is { } close) {
            close.Click += (_, _) => window.Close();
        }

        window.PropertyChanged += (_, change) => {
            if (change.Property == Window.WindowStateProperty) {
                Retell(window);
            }
        };
        Retell(window);

        if (dragArea is not null) {
            dragArea.PointerPressed += (_, pressed) => Move(window, pressed);
        }
    }

    /// <summary>
    /// Retells the window's own state on the fill button: the glyph shows
    /// what answers the state, and the tip says its name.
    /// </summary>
    private static void Retell(Window window) {
        var filling = window.WindowState == WindowState.Maximized;

        if (window.FindControl<Button>("CaptionMax") is { } maximize) {
            if (maximize.Content is LucideIcon glyph) {
                glyph.Kind = filling ? LucideIconKind.Copy : LucideIconKind.Square;
            }

            ToolTip.SetTip(maximize, filling ? "Restore" : "Maximize");
        }
    }

    private static void Move(Window window, PointerPressedEventArgs pressed) {
        // A press whose pointer lands on a command stays with the command:
        // the band moves the window only from its own quiet places.
        if (Answered(pressed.Source as Control)) {
            return;
        }

        if (pressed.ClickCount == 2) {
            if (window.CanResize) {
                Fill(window);
            }

            return;
        }

        if (window.WindowState == WindowState.Normal &&
            pressed.GetCurrentPoint(window).Properties.IsLeftButtonPressed) {
            window.BeginMoveDrag(pressed);
        }
    }

    /// <summary>
    /// Says whether the press landed inside anything that answers on its own —
    /// a button, a picker, a box somebody is writing in. The answer walks the
    /// ancestors as well, for a glyph inside a button is still a button's
    /// press.
    /// </summary>
    private static bool Answered(Control? source) =>
        source is not null &&
        source.GetSelfAndVisualAncestors()
              .OfType<Control>()
              .Any(ancestor => ancestor is Button or ComboBox or TextBox or SelectableTextBlock);

    private static void Fill(Window window) {
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
