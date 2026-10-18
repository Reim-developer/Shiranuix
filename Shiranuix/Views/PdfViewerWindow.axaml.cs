using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Shiranuix.ViewModels;
using SkiaSharp;

namespace Shiranuix.Views;

/// <summary>
/// The window a PDF is read beside the writing in.
/// </summary>
/// <remarks>
/// <para>
/// The window holds the paper itself: the reading's pointer lives in its
/// state, the page under the pointer is drawn off the interface thread, and
/// a keyboard's page turning answers the same as the buttons.
/// </para>
/// <para>
/// Nothing here is bound to leave a test with a window: what the reader
/// knows is on the state, and this only draws what the state says.
/// </para>
/// </remarks>
public partial class PdfViewerWindow : Window {
    /// <summary>The width a page is drawn at, in pixels.</summary>
    /// <remarks>
    /// A fixed width rather than the window's own: a page drawn to the pane's
    /// live width is re-drawn on every drag of the edge, and the paper does
    /// not change because its glass did.
    /// </remarks>
    private const int RenderWidth = 1000;

    private PdfViewerState? _reading;
    private int _drawing;

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
    /// Turns the window's reading to a paper, and draws its page.
    /// </summary>
    /// <param name="state">The reading to show.</param>
    /// <remarks>
    /// Called on a window that already stands, an open of another paper
    /// arrives here: the old reading is left without its listener, the new
    /// one takes its place, and the page under the pointer is drawn.
    /// </remarks>
    public void Read(PdfViewerState state) {
        ArgumentNullException.ThrowIfNull(state);

        if (_reading is { } standing) {
            standing.PropertyChanged -= OnReadingChanged;
        }

        _reading = state;
        _reading.PropertyChanged += OnReadingChanged;
        DataContext = _reading;
        _ = DrawThePage();
    }

    private void OnReadingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(PdfViewerState.Index)) {
            _ = DrawThePage();
        }
    }

    /// <summary>
    /// Draws the page the reading stands on, and leaves behind the one that
    /// has gone stale while the drawing ran.
    /// </summary>
    private async Task DrawThePage() {
        if (_reading is not { } reading) {
            return;
        }

        var stamp = ++_drawing;
        var index = reading.Index;

        try {
            var page = await Task.Run(() => reading.Draw(index, RenderWidth));
            if (page is null) {
                return;
            }

            // A page drawn while another was asked, or a reading that has
            // since been turned or retired, is not what the glass shows.
            if (stamp != _drawing || reading != _reading || index != _reading.Index) {
                return;
            }

            // The paper is its own bytes, and the drawn page is handed over
            // as a picture: encoded once, decoded once, and both readings
            // released when it is done.
            using var drawn = page;
            using var encoded = drawn.Encode(SKEncodedImageFormat.Png, 90);
            var bytes = encoded.ToArray();

            var bitmap = new Bitmap(new MemoryStream(bytes));
            Page.Source = bitmap;
        } catch (Exception) {
            // A page that will not draw leaves the last one standing: the
            // reading never throws the paper away for one bad leaf.
        }
    }

    private void OnPrevious(object? sender, RoutedEventArgs e) => _reading?.Previous();

    private void OnNext(object? sender, RoutedEventArgs e) => _reading?.Next();

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e) {
        switch (e.Key) {
            case Key.Left:
                _reading?.Previous();
                e.Handled = true;
                break;
            case Key.Right:
                _reading?.Next();
                e.Handled = true;
                break;
        }

        base.OnKeyDown(e);
    }
}
