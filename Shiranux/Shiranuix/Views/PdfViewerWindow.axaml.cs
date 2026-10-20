using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// The window a PDF is read beside the writing in.
/// </summary>
/// <remarks>
/// <para>
/// The window holds the paper itself: the reading stands on one page at a
/// time, the page shown whole — fitted to the stage, never cut — at the
/// glass's own scale, so the letters carry the pane's every pixel. The
/// reading steps on and back by the buttons, the wheel, and the keys, and
/// is looked at larger or smaller than the fit the same three ways plus the
/// steps under the page; a page stood larger than the glass is scrolled to
/// rather than cropped. The window's own closing button above the page
/// shuts the reading away.
/// </para>
/// <para>
/// Nothing here is bound to leave a test with a window: what the reader
/// knows is on the state, and this only draws what the state says.
/// </para>
/// </remarks>
public partial class PdfViewerWindow : Window {
    /// <summary>
    /// How long the window waits for a drag of its glass to end before the
    /// page falls into its new fit: a drag is a burst of sizes, and the
    /// paper is not re-fitted for every frame.
    /// </summary>
    private static readonly TimeSpan Resettle = TimeSpan.FromMilliseconds(250);

    private PdfViewerState? _reading;
    private int _drawing;
    private int _pixelStanding;
    private int _pageStanding = -1;
    private double _zoomStanding = 1.0;
    private bool _wired;
    private DispatcherTimer? _settle;

    /// <summary>
    /// Builds the window already reading a paper.
    /// </summary>
    /// <param name="state">The reading it shows from its first moment.</param>
    public PdfViewerWindow(PdfViewerState state) {
        ArgumentNullException.ThrowIfNull(state);
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
        Read(state);
    }

    /// <summary>
    /// The constructor the XAML previewer builds the window with.
    /// </summary>
    /// <remarks>
    /// Reads nothing worth reading — the running application builds the
    /// window through the constructor above.
    /// </remarks>
    public PdfViewerWindow() {
        InitializeComponent();
        WindowChrome.Watch(this, TitleBand);
    }

    /// <summary>
    /// Turns the window's reading to a paper, and shows the page it stands on.
    /// </summary>
    /// <param name="state">The reading to show.</param>
    /// <remarks>
    /// Called on a window that already stands, an open of another paper
    /// arrives here: the old paper is walked off, and the page the new
    /// reading stands on is drawn.
    /// </remarks>
    public void Read(PdfViewerState state) {
        ArgumentNullException.ThrowIfNull(state);

        _reading = state;
        DataContext = state;

        if (!_wired) {
            Stage.SizeChanged += OnStageSizeChanged;
            KeyDown += OnKey;
            PointerWheelChanged += OnWheel;
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;
            _wired = true;
        }

        // The page falls into its fit as soon as the stage knows its own
        // size; the first change of size answers the same. What the reading
        // stands on and how large it is looked at are said here too, so a
        // paper opened cold already carries its place and its size — and
        // knows which way its steps can still go — instead of staying mute
        // until the first hand touches a step.
        _ = ShowTheStanding();
        SayStanding();
        SayZoom();
    }

    /// <summary>
    /// Fits the page the reading stands on to the stage — whole, never cut —
    /// then grows or shrinks that fit to the size the reading is looked at,
    /// and draws it at the glass's own scale.
    /// </summary>
    private async Task ShowTheStanding() {
        if (_reading is not { } reading) {
            return;
        }

        var standing = reading.Standing;
        var shape = reading.PageShape(standing);
        var room = Stage.Bounds;
        if (room.Width is not > 0 || room.Height is not > 0) {
            return;
        }

        // The whole page on the stage: as wide as the room allows and as
        // tall as the room allows, whichever runs out first — the fit — and
        // from there the size the reading is actually being looked at.
        var fitted = Math.Min(room.Width, room.Height / shape);
        var width = fitted * reading.Zoom;
        var height = width * shape;
        var pixel = Math.Max(1, (int)(width * RenderScaling));

        // The sheet is never smaller than the room, so a page at or under
        // the fit stands in the middle of the window, and one looked at
        // larger grows past the glass with its edges still there to be
        // scrolled to rather than cropped away.
        Sheet.Width = Math.Max(width, room.Width);
        Sheet.Height = Math.Max(height, room.Height);

        Leaf.Width = width;
        Leaf.Height = height;

        if (standing == _pageStanding && pixel == _pixelStanding &&
            reading.Zoom == _zoomStanding && Leaf.Source is not null) {
            return;
        }

        var stamp = ++_drawing;

        try {
            var picture = await Task.Run(() => reading.Draw(standing, pixel));
            if (picture is null || stamp != _drawing) {
                return;
            }

            // The page's picture in bytes was made once in the reading;
            // here it is only decoded for the glass. Nothing native is
            // borrowed for the borrow-out, so a page cannot be handed
            // over dead no matter how the passes of a flip overlap.
            var bitmap = new Bitmap(new MemoryStream(picture));

            if (stamp != _drawing) {
                return;
            }

            Leaf.Source = bitmap;
            _pageStanding = standing;
            _pixelStanding = pixel;
            _zoomStanding = reading.Zoom;
        } catch (Exception) {
            // A page that will not draw leaves the paper standing: the
            // reading never throws the paper away for one bad page.
        }

        // The page after this one is the page the reader asks for next; it
        // is drawn while the eye is still on this one, so a flip is a turn
        // and not a wait. A page off the paper answers nothing and costs
        // nothing.
        _ = Task.Run(() => reading.Draw(standing + 1, pixel));
    }

    /// <summary>Says where the reading stands, and what a step can still reach.</summary>
    private void SayStanding() {
        if (_reading is not { } reading) {
            return;
        }

        Place.Text = $"{reading.Standing + 1} / {reading.Count}";
        Previous.IsEnabled = reading.Standing > 0;
        Next.IsEnabled = reading.Standing < reading.Count - 1;
    }

    /// <summary>Says how large the reading is looked at, and which way it can still move.</summary>
    private void SayZoom() {
        if (_reading is not { } reading) {
            return;
        }

        ZoomText.Text = $"{reading.Zoom * 100:0}%";
        ZoomIn.IsEnabled = reading.CanZoomIn;
        ZoomOut.IsEnabled = reading.CanZoomOut;
    }

    /// <summary>Walks the reading a step and shows the page it lands on.</summary>
    private void Walk(int by) {
        if (_reading is not { } reading) {
            return;
        }

        var stood = reading.Standing;
        reading.Step(by);

        if (reading.Standing != stood) {
            _ = ShowTheStanding();
        }

        SayStanding();
    }

    /// <summary>
    /// Looks at the reading a rung larger or smaller — a rung off either end
    /// stands at that end, which changes nothing and draws nothing new.
    /// </summary>
    /// <param name="by">How many rungs to move; larger when positive.</param>
    private void ZoomBy(int by) {
        if (_reading is not { } reading) {
            return;
        }

        reading.ZoomBy(by);
        _ = ShowTheStanding();
        SayZoom();
    }

    /// <summary>Puts the reading back on the fit, the size its glass offers.</summary>
    private void ZoomFit() {
        if (_reading is not { } reading) {
            return;
        }

        reading.ZoomFit();
        _ = ShowTheStanding();
        SayZoom();
    }

    private void OnPrevious(object? sender, RoutedEventArgs e) => Walk(-1);

    private void OnNext(object? sender, RoutedEventArgs e) => Walk(1);

    private void OnZoomOut(object? sender, RoutedEventArgs e) => ZoomBy(-1);

    private void OnZoomIn(object? sender, RoutedEventArgs e) => ZoomBy(1);

    private void OnZoomFit(object? sender, RoutedEventArgs e) => ZoomFit();

    private void OnKey(object? sender, KeyEventArgs e) {
        if (_reading is null) {
            return;
        }

        // The size only moves when the hand asking for it is holding the
        // control, so the bare keys keep walking the paper as they always
        // have; a hand on the control with no size under it walks nothing.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            switch (e.Key) {
                case Key.OemPlus or Key.Add:
                    ZoomBy(1);
                    break;
                case Key.OemMinus or Key.Subtract:
                    ZoomBy(-1);
                    break;
                case Key.D0 or Key.NumPad0:
                    ZoomFit();
                    break;
                default:
                    return;
            }

            e.Handled = true;
            return;
        }

        switch (e.Key) {
            case Key.Home:
                Walk(-_reading.Count);
                break;
            case Key.End:
                Walk(_reading.Count);
                break;
            case Key.Left or Key.PageUp or Key.Back:
                Walk(-1);
                break;
            case Key.Right or Key.PageDown or Key.Space or Key.Enter:
                Walk(1);
                break;
        }
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e) {
        e.Handled = true;

        // A hand on the control turns the wheel into a size rather than a
        // page: the paper grows or shrinks a rung instead of stepping on.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            ZoomBy(e.Delta.Y < 0 ? -1 : 1);
            return;
        }

        Walk(e.Delta.Y < 0 ? 1 : -1);
    }

    /// <summary>
    /// Brings the window down to the room it opened in, then shows the page.
    /// </summary>
    /// <remarks>
    /// The window comes up at a standing height that a room with scaled
    /// displays cannot always hold, and one taller than the room's work
    /// area puts the page's steps off the glass - so the height is asked
    /// of the room first, and the fit of the page follows the height it
    /// ends up standing at.
    /// </remarks>
    private void OnWindowOpened(object? sender, EventArgs e) {
        var room = Screens.ScreenFromWindow(this)?.WorkingArea
            ?? Screens.Primary?.WorkingArea;
        if (room is { } area) {
            var roomHeight = area.Height / RenderScaling;
            if (Bounds.Height > roomHeight) {
                Height = Math.Max(MinHeight, roomHeight);
            }
        }

        _ = ShowTheStanding();
    }

    private void OnStageSizeChanged(object? sender, SizeChangedEventArgs e) {
        if (_settle is null) {
            _settle = new DispatcherTimer { Interval = Resettle };
            _settle.Tick += (_, _) => {
                _settle?.Stop();
                _ = ShowTheStanding();
            };
        }

        _settle.Stop();
        _settle.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e) {
        _settle?.Stop();
        _settle = null;
    }
}
