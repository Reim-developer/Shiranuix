using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// The window a PDF is read beside the writing in.
/// </summary>
/// <remarks>
/// <para>
/// The window holds the paper itself: its pages stand one after another in
/// the scroll, each drawn at the pane's own width when it comes under the
/// eye — at the glass's own scale, so the letters carry the pane's every
/// pixel — and let go when it goes past. The button above shuts the reading
/// away.
/// </para>
/// <para>
/// Nothing here is bound to leave a test with a window: what the reader
/// knows is on the state, and this only draws what the state says.
/// </para>
/// </remarks>
public partial class PdfViewerWindow : Window {
    /// <summary>
    /// How long the window waits for a drag of its glass to end before the
    /// pages fall into their new places: a drag is a burst of widths, and
    /// the paper is not re-laid for every frame.
    /// </summary>
    private static readonly TimeSpan Resettle = TimeSpan.FromMilliseconds(250);

    private PdfViewerState? _reading;
    private readonly List<Image> _leaves = new();
    private readonly List<int> _drawnAt = new();
    private double[] _places = [];
    private int _drawing;
    private int _pixelDrawn;
    private bool _wired;
    private DispatcherTimer? _settle;

    /// <summary>
    /// Builds the window already reading a paper.
    /// </summary>
    /// <param name="state">The reading it shows from its first moment.</param>
    public PdfViewerWindow(PdfViewerState state) {
        ArgumentNullException.ThrowIfNull(state);
        InitializeComponent();
        Read(state);
    }

    /// <summary>
    /// The constructor the XAML previewer builds the window with.
    /// </summary>
    /// <remarks>
    /// Reads nothing worth reading — the running application builds the
    /// window through the constructor above.
    /// </remarks>
    public PdfViewerWindow() => InitializeComponent();

    /// <summary>
    /// Turns the window's reading to a paper, and lays its pages out.
    /// </summary>
    /// <param name="state">The reading to show.</param>
    /// <remarks>
    /// Called on a window that already stands, an open of another paper
    /// arrives here: the old leaves are walked off, the new paper's pages
    /// take their places, and the pages under the eye are drawn.
    /// </remarks>
    public void Read(PdfViewerState state) {
        ArgumentNullException.ThrowIfNull(state);

        _reading = state;
        DataContext = state;
        BuildLeaves(state);

        if (!_wired) {
            Pane.SizeChanged += OnPaneSizeChanged;
            Pane.ScrollChanged += OnPaneScrolled;
            Opened += OnWindowOpened;
            Closed += OnWindowClosed;
            _wired = true;
        }

        // The pages fall into their places as soon as the pane knows its own
        // width; the first change of size answers the same.
        LayLeaves();
    }

    /// <summary>
    /// Lays one leaf for every page the paper carries.
    /// </summary>
    /// <param name="reading">The reading whose pages are laid.</param>
    private void BuildLeaves(PdfViewerState reading) {
        Pages.Children.Clear();
        _leaves.Clear();
        _drawnAt.Clear();

        for (var index = 0; index < reading.Count; index++) {
            var leaf = new Image {
                HorizontalAlignment = HorizontalAlignment.Center,
                Stretch = Stretch.Fill,
            };
            _leaves.Add(leaf);
            _drawnAt.Add(0);
            Pages.Children.Add(leaf);
        }
    }

    /// <summary>
    /// Lays every leaf to the pane's width and the shape its page stands in,
    /// keeping the reading's place in the paper when the glass changed.
    /// </summary>
    private void LayLeaves() {
        if (_reading is not { } reading) {
            return;
        }

        var width = Pane.Bounds.Width;
        if (width is not > 0 || double.IsInfinity(width)) {
            return;
        }

        var laid = _places.Length > 0 ? _places[^1] : 0;
        var stood = Pane.Offset.Y;
        var pixel = Math.Max(1, (int)(width * RenderScaling));

        _places = new double[reading.Count + 1];
        for (var index = 0; index < _leaves.Count; index++) {
            var leaf = _leaves[index];
            leaf.Width = width;
            leaf.Height = width * reading.PageShape(index);
            _places[index + 1] = _places[index] + leaf.Height;
        }

        // The paper did not move because its glass did: the place the reader
        // stood at keeps its share of the paper when the width changed.
        if (laid > 0 && stood > 0 && pixel != _pixelDrawn) {
            Pane.Offset = new Vector(Pane.Offset.X, stood / laid * _places[^1]);
        }
        _pixelDrawn = pixel;

        _ = ShowStanding();
    }

    /// <summary>
    /// Draws the pages the eye stands on, at the width the pane now is, and
    /// lets the pages it left go.
    /// </summary>
    private async Task ShowStanding() {
        if (_reading is not { } reading || _places.Length <= 1) {
            return;
        }

        var stamp = ++_drawing;
        var (first, last) = RangeUnderTheEye(reading.Count);

        for (var index = 0; index < _leaves.Count; index++) {
            if (index < first || index > last) {
                _leaves[index].Source = null;
                _drawnAt[index] = 0;
            }
        }

        for (var index = first; index <= last; index++) {
            if (stamp != _drawing) {
                return;
            }

            var leaf = _leaves[index];
            if (leaf.Source is not null && _drawnAt[index] == _pixelDrawn) {
                continue;
            }

            try {
                var picture = await Task.Run(() => reading.Draw(index, _pixelDrawn));
                if (picture is null || stamp != _drawing) {
                    continue;
                }

                // The page's picture in bytes was made once in the reading;
                // here it is only decoded for the glass. Nothing native is
                // borrowed for the borrow-out, so a page cannot be handed
                // over dead no matter how the passes of the scroll overlap.
                var bitmap = new Bitmap(new MemoryStream(picture));

                if (stamp != _drawing) {
                    continue;
                }

                leaf.Source = bitmap;
                _drawnAt[index] = _pixelDrawn;
            } catch (Exception) {
                // A leaf that will not draw leaves the paper standing: the
                // reading never throws the paper away for one bad leaf.
            }
        }
    }

    /// <summary>
    /// The pages the eye stands between: where the scroll begins and ends,
    /// told in page places against the leaves laid out.
    /// </summary>
    /// <returns>The first page under the eye, and the last, counting from zero.</returns>
    private (int First, int Last) RangeUnderTheEye(int count) {
        var first = PlaceOf(Pane.Offset.Y);
        var last = Math.Min(count - 1, PlaceOf(Pane.Offset.Y + Pane.Bounds.Height));

        return (first, Math.Max(first, last));
    }

    /// <summary>The page a line of the paper lands in, told from the places laid out.</summary>
    /// <param name="line">How far down the paper the line is, in the pane's own units.</param>
    private int PlaceOf(double line) {
        var first = 0;
        var past = _places.Length - 1;

        while (first + 1 < past) {
            var middle = first + (past - first) / 2;
            if (_places[middle] <= line) {
                first = middle;
            } else {
                past = middle;
            }
        }

        return Math.Max(0, Math.Min(first, _places.Length - 2));
    }

    private void OnWindowOpened(object? sender, EventArgs e) => LayLeaves();

    private void OnPaneScrolled(object? sender, ScrollChangedEventArgs e) {
        _ = ShowStanding();
    }

    private void OnPaneSizeChanged(object? sender, SizeChangedEventArgs e) {
        if (_settle is null) {
            _settle = new DispatcherTimer { Interval = Resettle };
            _settle.Tick += (_, _) => {
                _settle?.Stop();
                LayLeaves();
            };
        }

        _settle.Stop();
        _settle.Start();
    }

    private void OnWindowClosed(object? sender, EventArgs e) {
        _settle?.Stop();
        _settle = null;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
