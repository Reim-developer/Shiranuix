using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Routes the keys and the wheel the document answers itself over the
/// editor: the history's own three, the window's page keys, the whole-file
/// home and end, and the wheel that turns the window once the box has no
/// scrolling room left.
/// </summary>
/// <remarks>
/// The text box's undo is switched off in the markup, because its stacks
/// die the moment the whole text is replaced from outside — which is what
/// opening a file does and what moving the window over a large document
/// does. The keys are consumed even when there is nothing to take back:
/// the boxes must not fall through to a history that no longer exists, and
/// a key with no move behind it should simply do nothing.
///
/// The handlers run even when the text box got there first
/// (<c>handledEventsToo</c>) and read the scroll position as they find
/// it: the window's travel is asked for only when the box has no room
/// left to move in that direction, so the two never travel at once —
/// whichever of them the control lets through first.
///
/// The view model is read through the accessor on every event rather than
/// held from the start: it reaches this class as the window's data
/// context, and the routing has to answer to whatever is attached now.
/// </remarks>
public static class EditorKeys {
    /// <summary>How many lines one turn of the wheel slides the window.</summary>
    private const int WheelLines = 3;

    /// <summary>
    /// How close to a scroll's edge counts as being at it — the last half
    /// pixel is usually rounding rather than a position.
    /// </summary>
    private const double ScrollEdge = 0.5;

    /// <summary>
    /// Wires the editor's keys and wheel over to the document that answers
    /// them.
    /// </summary>
    /// <param name="editor">The editor whose keys and wheel are routed.</param>
    /// <param name="viewModel">Reads the view model the routes answer, or
    /// null while the window holds none.</param>
    public static void Watch(TextBox editor, Func<EditorViewModel?> viewModel) {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(viewModel);

        // The editor's own scroll viewer, found once it exists.
        ScrollViewer? scroller = null;

        editor.AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        editor.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnPointerWheel,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        // Handles one of the keys the document answers itself over the
        // editor: the history's three, the window's two page keys, and the
        // whole-file home and end. The whole-file jumps put the caret after
        // the window, so what lands on screen is where the jump puts it
        // rather than where the box's own key left it.
        void OnKeyDown(object? sender, KeyEventArgs e) {
            if (viewModel() is not { } vm) {
                return;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.Home or Key.End) {
                if (e.Key == Key.Home) {
                    vm.SlideToStart();
                    vm.CaretIndex = 0;
                } else {
                    vm.SlideToEnd();
                    vm.CaretIndex = vm.Text.Length;
                }

                e.Handled = true;
                return;
            }

            // The page keys turn whole windows — but only once the box has no
            // page of its own left to turn in that direction.
            switch (e.Key) {
                case Key.PageDown when AtScrollLimit(down: true):
                    vm.SlideBy(vm.PageLines);
                    e.Handled = true;
                    return;
                case Key.PageUp when AtScrollLimit(down: false):
                    vm.SlideBy(-vm.PageLines);
                    e.Handled = true;
                    return;
            }

            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                return;
            }

            switch (e.Key) {
                case Key.Z when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    vm.Undo();
                    e.Handled = true;
                    break;
                case Key.Z:
                case Key.Y:
                    vm.Redo();
                    e.Handled = true;
                    break;
            }
        }

        // Turns one wheel notch into a window slide — once the box has run
        // out of scrolling room in that direction, so its own scrolling
        // stays the thing that happens first. A notch is three lines —
        // small enough to read what passed, large enough to feel like
        // travel. The delta is read the way the box reads it: positive is
        // up the file. A turn too small to name rounds to nothing and is
        // left alone, which is the right answer for the last drift of a
        // trackpad's momentum.
        void OnPointerWheel(object? sender, PointerWheelEventArgs e) {
            if (viewModel() is not { } vm) {
                return;
            }

            var lines = (int)Math.Round(e.Delta.Y * WheelLines);

            if (lines == 0 || !AtScrollLimit(down: lines < 0)) {
                return;
            }

            vm.SlideBy(-lines);
            e.Handled = true;
        }

        // Whether the editor's own scroll has nothing left to give in a
        // direction — the edge past which the window's slide is the only
        // travel left. The scroll viewer lives in the text box's template,
        // so it cannot be named from the markup and is found through the
        // visuals once. A box with no room at all — fewer lines than the
        // viewport — counts as being at both edges, which is right: there
        // is nothing to scroll, so a turn is asking the window directly.
        // Before the template exists the same answer stands; the wheel
        // cannot turn itself.
        bool AtScrollLimit(bool down) {
            scroller ??= editor.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault();

            if (scroller is null) {
                return true;
            }

            var max = Math.Max(scroller.Extent.Height - scroller.Viewport.Height, 0);

            return down
                ? scroller.Offset.Y >= max - ScrollEdge
                : scroller.Offset.Y <= ScrollEdge;
        }
    }
}
