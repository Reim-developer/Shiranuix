namespace Shiranuix.Models;

/// <summary>
/// Which way "next page" travels — the horizontal reading direction.
/// </summary>
/// <remarks>
/// Independent from <see cref="ReadingMode"/> so, for example, a webtoon can
/// still be LTR while single-page manga is RTL. Japanese manga reads
/// right-to-left, so that is the application default.
/// </remarks>
public enum ReadingDirection
{
    /// <summary>Western order: "next" advances to the right.</summary>
    LeftToRight,

    /// <summary>Manga order: "next" advances to the left.</summary>
    RightToLeft
}
