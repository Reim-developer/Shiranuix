namespace Shiranuix.Models;

/// <summary>
/// How pages are laid out while reading.
/// </summary>
/// <remarks>
/// This is one of three independent axes (mode, direction, fit) kept separate
/// so new modes can be added without touching the others — mirroring how
/// Mihon models reading options. Only <see cref="SinglePage"/> is implemented
/// today; <see cref="Webtoon"/> and <see cref="DoublePage"/> exist so the
/// vertical-scroll and two-up layouts can land without a redesign.
/// </remarks>
public enum ReadingMode
{
    /// <summary>One page at a time (the classic manga reader).</summary>
    SinglePage,

    /// <summary>Continuous vertical scroll (manhwa / webtoon style).</summary>
    Webtoon,

    /// <summary>Two pages side by side, for wide/landscape reading.</summary>
    DoublePage
}
