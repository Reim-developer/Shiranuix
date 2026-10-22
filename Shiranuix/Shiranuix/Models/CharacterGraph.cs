namespace Shiranuix.Models;

/// <summary>
/// One relationship map a writer keeps: the characters of a story and the
/// ties between them, under a name of their own.
/// </summary>
/// <remarks>
/// A writer writes more than one story, so the maps are many — each graph
/// stands alone, and deleting one takes its characters and every tie with
/// it rather than leaving rows nothing will ever draw.
/// </remarks>
public sealed class CharacterGraph {
    /// <summary>The row's key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// What the graph is called in the picker: the story it belongs to, as
    /// the writer named it. Names stand unique, so the picker never offers
    /// two graphs answering to the same word.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the graph was made.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>When anything in the graph last changed.</summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>The characters standing in this graph.</summary>
    public List<GraphCharacter> Characters { get; set; } = [];

    /// <summary>The relationships drawn between them.</summary>
    public List<GraphRelationship> Relationships { get; set; } = [];
}
