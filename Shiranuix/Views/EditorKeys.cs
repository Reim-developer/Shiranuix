using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Routes the keys and the wheel the document answers itself over the
/// editor: the window's two page keys, the whole-file home and end, and
/// the wheel that turns the window once the box has no scrolling room
/// left. It also stands the page back on whole lines once the scrolling
/// has settled, so nothing the page shows is ever half a row at the
/// sheet's own edge.
/// </summary>
/// <remarks>
/// The text box's undo is switched off in the markup, because its stacks
/// die the moment the whole text is replaced from outside — which is what
/// opening a file does and what moving the window over a large document
/// does. The history's own chords are not here either: they are routes
/// <see cref="CommandKeys"/> answers, the same ones the writer may have
/// moved — a redo left unbound simply answers to nothing.
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

        // The page's settle: a scroll that stops between lines stands the
        // page back on the nearest whole line a breath after the last
        // change, so what the sheet's lower edge shows is a row and never
        // half of one — the one thing a page must not do that a code pane
        // gets away with. The timer waits out every change, so a rolling
        // wheel or a held scrollbar is never interrupted; the page is
        // only settled once the moving has stopped. The edges are left
        // where they stand: a page standing on its own first or last row
        // already shows that row whole.
        var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        settle.Tick += (_, _) => {
            settle.Stop();

            var lineHeight = editor.LineHeight;
            if (scroller is null || double.IsNaN(lineHeight) || lineHeight <= 0) {
                return;
            }

            var offset = scroller.Offset.Y;
            var max = Math.Max(scroller.Extent.Height - scroller.Viewport.Height, 0);
            if (offset <= ScrollEdge || offset >= max - ScrollEdge) {
                return;
            }

            var snapped = Math.Clamp(Math.Round(offset / lineHeight) * lineHeight, 0, max);
            if (Math.Abs(snapped - offset) >= ScrollEdge) {
                scroller.Offset = new Avalonia.Vector(scroller.Offset.X, snapped);
            }
        };

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
        // editor: the window's two page keys, and the whole-file home and
        // end. The whole-file jumps put the caret after the window, so
        // what lands on screen is where the jump puts it rather than
        // where the box's own key left it.
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

            // The settle listens from the first scroll the viewer reports,
            // whatever moved it — the wheel, a scrollbar, the caret asking
            // for its own row. A gutter that is already listening for the
            // same event only makes the page's own margin; this makes its
            // standing.
            scroller.ScrollChanged -= OnScroll;
            scroller.ScrollChanged += OnScroll;

            var max = Math.Max(scroller.Extent.Height - scroller.Viewport.Height, 0);

            return down
                ? scroller.Offset.Y >= max - ScrollEdge
                : scroller.Offset.Y <= ScrollEdge;
        }

        void OnScroll(object? sender, ScrollChangedEventArgs e) {
            settle.Stop();
            settle.Start();
        }
    }
}
