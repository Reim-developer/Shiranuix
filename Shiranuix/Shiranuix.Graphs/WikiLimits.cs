namespace Shiranuix.Graphs;

/// <summary>
/// How much the wiki keeps of a character: the writing a writer may give
/// them, and the length they are told of while they write it.
/// </summary>
/// <remarks>
/// A description is a character written out in full — the face, the
/// bearing, the history, the things nobody has said aloud — and a
/// character in a long novel may need a good deal of it. A hundred
/// thousand characters is a book the length of a chapter: more than any
/// character needs, and more than the wiki can be asked to hold.
/// </remarks>
public static class WikiLimits {
    /// <summary>
    /// How much a character's description may hold, in characters.
    /// </summary>
    public const int Description = 100_000;
}
