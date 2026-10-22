namespace Shiranuix.Models;

/// <summary>
/// One tie drawn between two characters: which way it points and what it
/// says, drawn along the arrow.
/// </summary>
/// <remarks>
/// Directed, and many to a pair — "the teacher of" and "in love with" may
/// both stand between the same two characters, each with its own words and
/// its own line on the canvas.
/// </remarks>
public sealed class GraphRelationship {
    /// <summary>The row's key.</summary>
    public int Id { get; set; }

    /// <summary>The graph the tie is drawn in.</summary>
    public int GraphId { get; set; }

    /// <summary>The graph itself, when a query has joined it.</summary>
    public CharacterGraph? Graph { get; set; }

    /// <summary>The character the tie starts at — the arrow points away from them.</summary>
    public int FromCharacterId { get; set; }

    /// <summary>The character the tie starts at, when a query has joined it.</summary>
    public GraphCharacter? FromCharacter { get; set; }

    /// <summary>The character the tie ends at — the arrow arrives here.</summary>
    public int ToCharacterId { get; set; }

    /// <summary>The character the tie ends at, when a query has joined it.</summary>
    public GraphCharacter? ToCharacter { get; set; }

    /// <summary>
    /// What the tie says — "the teacher of", "in love with" — or nothing at
    /// all, for a tie whose direction alone is enough.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// How far the tie's line is bent away from the straight one, in the
    /// canvas's own units — zero for a line the writer has never touched,
    /// and either side for one their hand has curved.
    /// </summary>
    public double Bend { get; set; }
}
