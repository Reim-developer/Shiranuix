namespace Shiranuix.Models;

/// <summary>
/// How a page image is scaled to the viewport.
/// </summary>
/// <remarks>
/// The third independent axis (alongside <see cref="ReadingMode"/> and
/// <see cref="ReadingDirection"/>). Kept separate because a user may combine
/// any fit with any mode/direction; modelling them as one setting would
/// explode into special cases.
/// </remarks>
public enum FitMode
{
    /// <summary>Uniform scale so the whole page fits inside the viewport.</summary>
    FitScreen,

    /// <summary>Scale to match the viewport width; vertical overflow is scrollable.</summary>
    FitWidth,

    /// <summary>Scale to match the viewport height; horizontal overflow is scrollable.</summary>
    FitHeight,

    /// <summary>No scaling: one source pixel maps to one screen pixel.</summary>
    OriginalSize
}
