using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Shiranuix.Models;
using Shiranuix.Utilities;
using Shiranuix.ViewModels;

namespace Shiranuix.Views;

/// <summary>
/// Code-behind for the reader screen.
/// </summary>
/// <remarks>
/// Handles the two pieces of genuinely view-level logic: sizing the page
/// image for the current fit mode (delegating the arithmetic to the
/// testable <see cref="ImageFitting"/>) and keeping the seek slider in sync
/// with the reading position. Navigation, page state and the fit/direction
/// choices themselves live in <see cref="ReaderViewModel"/>.
/// </remarks>
public partial class ReaderView : UserControl
{
    private ReaderViewModel? _viewModel;

    /// <summary>Guards against a slider ↔ view-model feedback loop.</summary>
    private bool _syncingSlider;

    /// <summary>
    /// Creates the control and wires the layout/slider event handlers.
    /// </summary>
    public ReaderView()
    {
        InitializeComponent();

        // Re-fit whenever the scroll viewport is resized.
        PageScroller.SizeChanged += (_, _) => UpdateImageLayout();

        // Push seek-bar drags into the view model.
        PageSlider.ValueChanged += OnSliderValueChanged;
    }

    /// <summary>
    /// Reacts to the view model being attached, subscribing to its changes
    /// and performing an initial sync.
    /// </summary>
    /// <param name="e">Event args.</param>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ReaderViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        SyncSliderFromViewModel();
        UpdateImageLayout();
    }

    /// <summary>
    /// Reacts to view-model changes that affect layout or the slider.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Property-changed args.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReaderViewModel.CurrentImage):
            case nameof(ReaderViewModel.FitMode):
                UpdateImageLayout();
                break;

            case nameof(ReaderViewModel.CurrentPageIndex):
            case nameof(ReaderViewModel.PageCount):
                SyncSliderFromViewModel();
                break;
        }
    }

    /// <summary>
    /// Sizes and aligns the page image to satisfy the current fit mode.
    /// </summary>
    private void UpdateImageLayout()
    {
        Bitmap? image = _viewModel?.CurrentImage;

        if (image is null || _viewModel is null)
        {
            // No page yet: let the Image auto-size to nothing.
            PageImage.Width = double.NaN;
            PageImage.Height = double.NaN;
            return;
        }

        var source = new Size(image.PixelSize.Width, image.PixelSize.Height);
        var viewport = PageScroller.Bounds.Size;
        Size box = ImageFitting.ComputeDisplaySize(source, viewport, _viewModel.FitMode);

        if (box.Width <= 0 || box.Height <= 0)
        {
            // Viewport not laid out yet; retry on the next size pass.
            PageImage.Width = double.NaN;
            PageImage.Height = double.NaN;
            return;
        }

        PageImage.Width = box.Width;
        PageImage.Height = box.Height;

        // Centre the image when it fits; otherwise anchor to the start so
        // scrolling reaches the top-left corner normally.
        bool fits = box.Width <= viewport.Width + 0.5
                    && box.Height <= viewport.Height + 0.5;
        PageScroller.HorizontalContentAlignment =
            fits ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        PageScroller.VerticalContentAlignment =
            fits ? VerticalAlignment.Center : VerticalAlignment.Top;
    }

    /// <summary>
    /// Copies the reading position and page count into the seek slider.
    /// </summary>
    private void SyncSliderFromViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        PageSlider.Maximum = Math.Max(1, _viewModel.PageCount);

        if (!_syncingSlider
            && (int)PageSlider.Value != _viewModel.CurrentPageIndex)
        {
            _syncingSlider = true;
            PageSlider.Value = _viewModel.CurrentPageIndex;
            _syncingSlider = false;
        }
    }

    /// <summary>
    /// Applies a seek-bar drag to the reading position.
    /// </summary>
    /// <param name="sender">The slider.</param>
    /// <param name="e">Event args (the new value is read from the slider).</param>
    private void OnSliderValueChanged(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _syncingSlider)
        {
            return;
        }

        // The slider's Value is already updated when ValueChanged fires.
        int index = (int)Math.Round(PageSlider.Value);
        if (index != _viewModel.CurrentPageIndex)
        {
            _syncingSlider = true;
            _viewModel.CurrentPageIndex = index;
            _syncingSlider = false;
        }
    }
}
