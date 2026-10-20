namespace Narika.Services.Theme;

/// <summary>
/// Which light/dark answer the launcher is showing: the machine's own, or
/// one of the two the person overrode it with.
/// </summary>
public enum ThemeMode {
    /// <summary>
    /// Whatever the machine currently says — the default, and the one a
    /// person who never touches the toggle is always on.
    /// </summary>
    System,

    /// <summary>
    /// The light face, chosen by hand.
    /// </summary>
    Light,

    /// <summary>
    /// The dark face, chosen by hand.
    /// </summary>
    Dark
}
