namespace Shiranuix.CoreFormat;

/// <summary>
/// Which way a paragraph's lines are set: flush left, centred, or flush
/// right.
/// </summary>
/// <remarks>
/// Left is what a page of prose starts at, and the numbers these are stored
/// as are the numbers the file format writes: the file carries the choice
/// in one byte, and these names are what the app calls it.
/// </remarks>
public enum ParagraphAlignment : byte {
    /// <summary>Every line flush with the page's left edge.</summary>
    Left = 0,

    /// <summary>Every line sitting in the middle.</summary>
    Center = 1,

    /// <summary>Every line flush with the page's right edge.</summary>
    Right = 2
}
