using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Narika.Services.Theme;

/// <summary>
/// The launcher's face: which theme is showing, the words and glyph it is
/// shown under, and the button that walks to the next answer.
/// </summary>
/// <remarks>
/// The mode is the whole of the state, and it is remembered across runs in
/// a file beside the app's own settings — a choice a person makes once
/// should not be undone every time the launcher opens. Applying the mode to
/// the application is a separate, one-line concern the application wires up
/// itself, which is what keeps this class constructible in a test that has
/// no application behind it at all.
/// </remarks>
public sealed partial class ThemeService : ObservableObject {
    private readonly string _path;
    private readonly Action<ThemeMode> _apply;

    /// <summary>
    /// Creates the service over where the remembered mode lives and how it
    /// is applied.
    /// </summary>
    /// <param name="apply">Applies a mode to the running application.</param>
    /// <param name="path">
    /// The file the mode is remembered in; the application passes its own
    /// default, a test passes a temporary one.
    /// </param>
    public ThemeService(Action<ThemeMode> apply, string path) {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _path = path ?? throw new ArgumentNullException(nameof(path));

        // Through the property rather than the field beside it, so the
        // first paint and the first save take the same road every later
        // one does — and applied once more by hand for the run where the
        // remembered answer *is* the machine's own, which the property
        // would (rightly) call no change at all.
        Mode = Load();
        NextCommand = new RelayCommand(Next);

        _apply(Mode);
    }

    /// <summary>
    /// The mode being shown now.
    /// </summary>
    [ObservableProperty]
    private ThemeMode mode;

    /// <summary>
    /// Walks System -&gt; Light -&gt; Dark -&gt; System: the three answers,
    /// one button, and nowhere else to fall off to.
    /// </summary>
    public IRelayCommand NextCommand { get; }

    /// <summary>
    /// The word the toggle shows for the current mode.
    /// </summary>
    public string ModeLabel => Mode switch {
        ThemeMode.Light => "Light",
        ThemeMode.Dark => "Dark",
        _ => "System"
    };

    /// <summary>
    /// The glyph the toggle shows for the current mode: a monitor for the
    /// machine's answer, a sun and a moon for the two hand-picked ones.
    /// The Segoe Fluent Icons codepoints stand here as the glyphs they
    /// are, each with the name it carries said beside it.
    /// </summary>
    public string ModeIcon => Mode switch {
        ThemeMode.Light => "",   // WeatherSunny
        ThemeMode.Dark => "",   // WeatherMoon
        _ => ""             // TVMonitor
    };

    private void Next() =>
        Mode = Mode switch {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.System
        };

    /// <inheritdoc />
    partial void OnModeChanged(ThemeMode value) {
        _apply(value);
        Save(value);

        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(ModeIcon));
    }

    /// <summary>
    /// Reads the remembered mode, falling back to the machine's answer for
    /// a first run — and for a file that says anything that is not one of
    /// the three, since a settings file is written by hand as often as not.
    /// </summary>
    private ThemeMode Load() {
        try {
            return Enum.TryParse<ThemeMode>(File.ReadAllText(_path).Trim(), out var mode)
                ? mode
                : ThemeMode.System;
        } catch (IOException) {
            return ThemeMode.System;
        } catch (UnauthorizedAccessException) {
            return ThemeMode.System;
        }
    }

    private void Save(ThemeMode mode) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, mode.ToString());
        } catch (IOException) {
            // A launcher that opens is worth more than a preference that
            // is remembered: an unwritable disk leaves the choice to the
            // next run rather than taking this one down with it.
        } catch (UnauthorizedAccessException) {
            // The same, said for the other way a disk refuses.
        }
    }
}
