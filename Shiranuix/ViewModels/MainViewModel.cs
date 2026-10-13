namespace Shiranuix.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shiranuix.Models;
using Shiranuix.Services;

/// <summary>
/// Root view model: owns navigation between the home and reader screens and
/// the requested theme.
/// </summary>
/// <remarks>
/// Navigation uses a single <see cref="Current"/> child that the window shows
/// through the <c>ViewLocator</c>, which maps a view model to its view by name.
/// The shell listens to child events rather than reaching into their internals,
/// keeping each screen independent.
/// </remarks>
public partial class MainViewModel : ViewModelBase {
    private readonly HomeViewModel _home;
    private readonly ReaderViewModel _reader;

    /// <summary>The screen currently displayed in the window.</summary>
    [ObservableProperty]
    private ViewModelBase _current;

    /// <summary>The requested colour theme.</summary>
    [ObservableProperty]
    private ThemeMode _themeMode = ThemeMode.System;

    /// <summary>
    /// Creates a fully wired shell using the default services.
    /// </summary>
    /// <remarks>Also used by the XAML previewer and quick tests.</remarks>
    public MainViewModel()
        : this(new DirectoryFolderScanner(), new InMemoryLibraryRepository(), new AsyncImageLoader()) {
    }

    /// <summary>
    /// Creates a shell with the given services (dependency injection).
    /// </summary>
    /// <param name="scanner">Turns folders into chapters.</param>
    /// <param name="repository">Backs the recent list.</param>
    /// <param name="imageLoader">Loads page images.</param>
    public MainViewModel(IFolderScanner scanner, ILibraryRepository repository, IImageLoader imageLoader) {
        _home = new HomeViewModel(scanner, repository);
        _reader = new ReaderViewModel(imageLoader);
        _current = _home;

        // Home asks to open a chapter -> show the reader at page 1.
        _home.ChapterRequested += (_, chapter) => {
            _reader.Open(chapter);
            Current = _reader;
        };

        // Reader asks to go back -> show home again.
        _reader.BackRequested += (_, _) => Current = _home;
    }

    /// <summary>The home screen view model.</summary>
    public HomeViewModel Home => _home;

    /// <summary>The reader screen view model.</summary>
    public ReaderViewModel Reader => _reader;

    /// <summary>
    /// Cycles the theme: System → Light → Dark → System.
    /// </summary>
    [RelayCommand]
    private void CycleTheme() {
        ThemeMode = ThemeMode switch {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.System
        };
    }
}
