namespace Shiranuix.Models;

/// <summary>
/// Which way the page's lines are set: flush left, centred, flush right, or
/// spread out to both rims.
/// </summary>
/// <remarks>
/// One way for the whole page: a .txt file carries text and nothing else, so
/// the alignment is how the page is drawn rather than anything the file can
/// remember - the same footing as the typeface and the point size. Left is
/// what a page of prose starts at, and the spread needs a wrap to break the
/// lines it fills; a page drawn line by line has nothing between the rims
/// for the spread to reach.
/// </remarks>
public enum TextAlign {
    /// <summary>Every line flush with the page's left edge.</summary>
    Left,

    /// <summary>Every line sitting in the middle.</summary>
    Center,

    /// <summary>Every line flush with the page's right edge.</summary>
    Right,

    /// <summary>Every wrapped line reaching both rims, a paragraph's own last line staying home.</summary>
    Justify
}
