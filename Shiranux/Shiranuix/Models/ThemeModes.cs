namespace Shiranuix.Models;

/// <summary>
/// The theme choices, in the order the settings window offers them.
/// </summary>
/// <remarks>
/// A picker binds a list, and an enum cannot carry a list of itself — so the
/// list lives here, following the system first, because that is the choice a
/// fresh install arrives on.
/// </remarks>
public static class ThemeModes {
    /// <summary>Every mode, the system preference first.</summary>
    public static readonly IReadOnlyList<ThemeMode> All =
        [ThemeMode.System, ThemeMode.Light, ThemeMode.Dark];
}
