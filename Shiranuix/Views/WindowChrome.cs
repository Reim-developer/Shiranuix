using Avalonia.Controls;
using Lucide.Avalonia;

namespace Shiranuix.Views;

/// <summary>
/// The windows' own chrome: what the bands carry and what the frames read.
/// </summary>
/// <remarks>
/// <para>
/// The drag itself and the double press belong to the band the markup marks
/// as the title bar with <c>WindowDecorationProperties.ElementRole</c>, which
/// the runtime answers natively — moving the window, filling it, and the
/// system's own way between the two. This class answers for everything the
/// ledge carries besides: the three glyphs named <c>CaptionMin</c>,
/// <c>CaptionMax</c> and <c>CaptionClose</c> rest, fill and leave the window,
/// the frame named <c>Frame</c> reads the window's own states as its
/// pseudo-classes — <c>:active</c> while the window holds the eye,
/// <c>:maximized</c> while it fills the room — and the fill glyph answers the
/// state the window is in. What a window does not declare, the chrome leaves
/// out; the fill button of a window that cannot be resized is shown no more.
/// </para>
/// </remarks>
public static class WindowChrome {

    /// <summary>
    /// Wires a window's chrome: its three caption buttons and the frame's
    /// reading of the window's own states.
    /// </summary>
    /// <param name="window">The window whose chrome this is.</param>
    public static void Watch(Window window) {
        if (window.FindControl<Button>("CaptionMin") is { } minimize) {
            minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
        }

        if (window.FindControl<Button>("CaptionClose") is { } close) {
            close.Click += (_, _) => window.Close();
        }

        window.PropertyChanged += (_, change) => {
            if (change.Property == WindowBase.IsActiveProperty ||
                change.Property == Window.WindowStateProperty) {
                Retell(window);
            }
        };
        Retell(window);
    }

    /// <summary>
    /// Retells the window's own states on what reads them: the frame's
    /// pseudo-classes answer where its styles look, and the fill button shows
    /// the glyph that answers the state the window is in.
    /// </summary>
    private static void Retell(Window window) {
        var filling = window.WindowState == WindowState.Maximized;

        if (window.FindControl<Border>("Frame") is { } frame) {
            frame.Classes.Set(":active", window.IsActive);
            frame.Classes.Set(":maximized", filling);
        }

        if (window.FindControl<Button>("CaptionMax") is { } maximize) {
            if (maximize.Content is LucideIcon glyph) {
                glyph.Kind = filling ? LucideIconKind.Copy : LucideIconKind.Square;
            }

            ToolTip.SetTip(maximize, filling ? "Restore" : "Maximize");
        }
    }
}
