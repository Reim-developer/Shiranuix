namespace Shiranuix.Models;

/// <summary>
/// The requested application colour theme.
/// </summary>
/// <remarks>
/// <see cref="System"/> follows the OS preference; the other two force a
/// fixed theme regardless of the OS. Kept separate from the concrete
/// palette so <see cref="System"/> can be resolved against the platform
/// without losing the user's original choice.
/// </remarks>
public enum ThemeMode
{
    /// <summary>Follow the operating system's light/dark preference.</summary>
    System,

    /// <summary>Always use the light palette.</summary>
    Light,

    /// <summary>Always use the dark palette.</summary>
    Dark
}
