namespace Shiranuix.Models;

/// <summary>
/// One character in the wiki: what they are called, when they were born,
/// what the writer says about them, and whatever else is worth keeping of
/// them.
/// </summary>
/// <remarks>
/// <para>
/// The wiki is the cast of every story at once: the same person may stand
/// in a map here and in a map there, and is one character either way — so
/// what is known about them is kept once, and the maps keep only where
/// they stand.
/// </para>
/// <para>
/// The fields are all text, because a character in a story is not a record
/// in a register: the day they were born may be a date, or a season, or
/// the night the storm came, and the writer is the one who says which.
/// An empty field is not a missing one — it is a field nobody has filled
/// in yet, and it reads as "None" rather than as a hole.
/// </para>
/// </remarks>
public sealed class WikiCharacter {
    /// <summary>The row's key.</summary>
    public int Id { get; set; }

    /// <summary>The character's name, drawn under their node and worn in their circle.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The day they were born, as the writer writes it: a date, a season,
    /// or whatever else the story gives them.
    /// </summary>
    public string BirthDate { get; set; } = string.Empty;

    /// <summary>What the writer says about them, in a few lines.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whatever else is worth keeping of them — a note, a secret, a thing
    /// that has not come up yet.
    /// </summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>
    /// Their face, as the picture itself: the bytes of an image the writer
    /// chose, kept here rather than pointed at, so that the character
    /// carries it wherever they are kept.
    /// </summary>
    /// <remarks>
    /// A picture kept in the database is a picture that cannot be lost by
    /// a file being moved, and a picture that travels with the character
    /// when the store is moved somewhere else — which is the whole point
    /// of a wiki: one character, kept once, however many stories they
    /// stand in.
    /// </remarks>
    public byte[]? Avatar { get; set; }

    /// <summary>When the wiki first heard of them.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When the wiki last heard anything new about them.</summary>
    public DateTime UpdatedUtc { get; set; }
}
