namespace Shiranuix.ViewModels;

using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Models;
using Shiranuix.Services;

/// <summary>
/// State and behaviour for the reader screen.
/// </summary>
/// <remarks>
/// Owns the reading position and the three independent presentation axes
/// (<see cref="ReadingMode"/>, <see cref="ReadingDirection"/>,
/// <see cref="FitMode"/>). It depends on <see cref="IImageLoader"/> only
/// through its interface, so tests can substitute a fake loader and exercise
/// navigation without decoding real images.
/// </remarks>
/// <remarks>
/// No UI control types appear here, keeping the class headlessly testable.
/// The only Avalonia type is <see cref="Bitmap"/>, which is a data type (not a
/// control) and is never constructed by the tests.
/// </remarks>
public partial class ReaderViewModel : ViewModelBase
{
    private readonly IImageLoader _imageLoader;

    /// <summary>Cancels a superseded page load so a slow decode can't clobber a newer page.</summary>
    private CancellationTokenSource? _loadCts;

    /// <summary>The most recent load task, exposed so tests can await it deterministically.</summary>
    private Task _pendingLoad = Task.CompletedTask;

    /// <summary>Chapter currently being read, or <c>null</c> if none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageCount))]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    private Chapter? _chapter;

    /// <summary>All fit modes, for the toolbar's fit selector.</summary>
    public static IReadOnlyList<FitMode> AllFitModes { get; } =
        Enum.GetValues<FitMode>();

    /// <summary>All reading directions, for the toolbar's direction selector.</summary>
    public static IReadOnlyList<ReadingDirection> AllDirections { get; } =
        Enum.GetValues<ReadingDirection>();

    /// <summary>1-based index of the page currently displayed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageLabel))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    private int _currentPageIndex = 1;

    /// <summary>Decoded image for the current page, or <c>null</c>.</summary>
    [ObservableProperty]
    private Bitmap? _currentImage;

    /// <summary>The reading-mode axis (single page, webtoon, ...).</summary>
    [ObservableProperty]
    private ReadingMode _readingMode = ReadingMode.SinglePage;

    /// <summary>The reading-direction axis (LTR / RTL).</summary>
    [ObservableProperty]
    private ReadingDirection _readingDirection = ReadingDirection.RightToLeft;

    /// <summary>The fit-mode axis (fit screen, fit width, ...).</summary>
    [ObservableProperty]
    private FitMode _fitMode = FitMode.FitScreen;

    /// <summary>
    /// Raised when the user wants to leave the reader. The shell subscribes
    /// to navigate back to home.
    /// </summary>
    public event EventHandler? BackRequested;

    /// <summary>
    /// Creates a reader view model.
    /// </summary>
    /// <param name="imageLoader">Loads page images.</param>
    public ReaderViewModel(IImageLoader imageLoader)
    {
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
    }

    /// <summary>Total number of pages in the current chapter.</summary>
    public int PageCount => Chapter?.PageCount ?? 0;

    /// <summary>Whether there is a page after the current one.</summary>
    public bool CanGoNext => CurrentPageIndex < PageCount;

    /// <summary>Whether there is a page before the current one.</summary>
    public bool CanGoPrevious => CurrentPageIndex > 1;

    /// <summary>Human-readable position, e.g. <c>"3 / 42"</c>.</summary>
    public string PageLabel =>
        PageCount == 0 ? "– / –" : $"{CurrentPageIndex} / {PageCount}";

    /// <summary>
    /// The task for the in-flight (or most recent) page load, so callers and
    /// tests can await image availability.
    /// </summary>
    internal Task PendingLoad => _pendingLoad;

    /// <summary>
    /// Opens a chapter for reading, starting at page 1.
    /// </summary>
    /// <param name="chapter">The chapter to read.</param>
    public void Open(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        Chapter = chapter;
        CurrentPageIndex = 1;
    }

    /// <summary>Jumps to the next page, if any.</summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        // Guard independently of CanExecute: RelayCommand.Execute bypasses
        // CanExecute, so this keeps the position in range even when invoked
        // directly (e.g. from a test or a misbehaving bound control).
        if (!CanGoNext)
        {
            return;
        }

        CurrentPageIndex++;
    }

    /// <summary>Jumps to the previous page, if any.</summary>
    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        CurrentPageIndex--;
    }

    /// <summary>
    /// Jumps directly to a 1-based page index, clamped into range.
    /// </summary>
    /// <param name="index">Requested 1-based page index.</param>
    [RelayCommand]
    private void GoToPage(int index)
    {
        if (Chapter is null)
        {
            return;
        }

        CurrentPageIndex = Math.Clamp(index, 1, Chapter.PageCount);
    }

    /// <summary>Requests navigation back to the home screen.</summary>
    [RelayCommand]
    private void Back()
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Reacts to a position/chapter change by (re)loading the page image.
    /// </summary>
    /// <param name="value">The new page index.</param>
    partial void OnCurrentPageIndexChanged(int value)
    {
        TriggerLoad();
    }

    /// <summary>
    /// Reacts to a chapter change by (re)loading the first page image.
    /// </summary>
    /// <param name="value">The new chapter (may be null).</param>
    partial void OnChapterChanged(Chapter? value)
    {
        TriggerLoad();
    }

    /// <summary>
    /// Starts a load for the current page, cancelling any in-flight load.
    /// </summary>
    private void TriggerLoad()
    {
        _pendingLoad = LoadCurrentImageAsync();
    }

    /// <summary>
    /// Loads the image for the current page, ignoring a stale result if the
    /// user has since navigated away.
    /// </summary>
    /// <returns>A task that completes when the image has been loaded.</returns>
    private async Task LoadCurrentImageAsync()
    {
        // Cancel any previous load: only the latest navigation should win. We
        // intentionally don't dispose the prior CTS here — a superseded load may
        // still be unwinding on it, and it holds no timer to release.
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;

        MangaPage? page = Chapter?.FindPage(CurrentPageIndex);
        if (page is null)
        {
            CurrentImage = null;
            return;
        }

        try
        {
            // No ConfigureAwait(false): we want to resume on the captured
            // context so CurrentImage is set on the UI thread when running in
            // the app (in tests there is no context, which is also fine).
            Bitmap? image = await _imageLoader.LoadAsync(page.ImageFile, token);

            // Apply only if this load wasn't superseded while decoding.
            if (!token.IsCancellationRequested)
            {
                CurrentImage = image;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer load took over; nothing to do.
        }
    }
}
