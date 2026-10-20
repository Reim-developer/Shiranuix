namespace Shiranuix.Models;

/// <summary>
/// The dash a pair of hyphens becomes as the writer types.
/// </summary>
/// <remarks>
/// In prose a pair of hyphens stands for the dash that breaks a sentence,
/// not a word, and the keyboard gives the two cheaply. Off keeps them as
/// they were typed.
/// </remarks>
public enum DashMode {
    /// <summary>The hyphens stay as they were typed.</summary>
    Off = 0,

    /// <summary>The pair becomes the short dash, the width of a letter n.</summary>
    En = 1,

    /// <summary>The pair becomes the long dash, the width of a letter m.</summary>
    Em = 2,
}
