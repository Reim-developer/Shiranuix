namespace Shiranuix.Models;

/// <summary>
/// Which way the page's lines are set: flush left, centred, or flush right.
/// </summary>
/// <remarks>
/// One way for the whole page: a .txt file carries text and nothing else, so
/// the alignment is how the page is drawn rather than anything the file can
/// remember - the same footing as the typeface and the point size. Left is
/// what a page of prose starts at. Which way each line of the writing itself
/// stands is a different question, answered per line and carried in the app's
/// own file - this is the shape of the page those lines are drawn on.
/// </remarks>
public enum TextAlign {
    /// <summary>Every line flush with the page's left edge.</summary>
    Left,

    /// <summary>Every line sitting in the middle.</summary>
    Center,

    /// <summary>Every line flush with the page's right edge.</summary>
    Right
}
