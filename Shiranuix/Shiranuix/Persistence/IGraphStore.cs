using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// What the relationship maps remember: the graphs themselves, the
/// characters standing in them, and the ties drawn between those.
/// </summary>
/// <remarks>
/// Every operation builds its own context and disposes it — the same rule
/// the other stores keep, and the same reason: a context carries change
/// tracking and is not thread-safe, so nothing here holds one between
/// calls.
/// </remarks>
public interface IGraphStore {
    /// <summary>Every graph, by name, with how much stands in each.</summary>
    IReadOnlyList<GraphSummary> ListGraphs();

    /// <summary>
    /// Creates an empty graph under the given name, answering with its id.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A graph under that name already stands — names are the picker's
    /// vocabulary, and they are unique.
    /// </exception>
    int CreateGraph(string name);

    /// <summary>Gives a graph another name, which must be nobody else's.</summary>
    /// <exception cref="InvalidOperationException">Another graph answers to that name.</exception>
    void RenameGraph(int graphId, string name);

    /// <summary>Removes a graph whole — its characters and every tie go with it.</summary>
    void DeleteGraph(int graphId);

    /// <summary>Loads everything standing in one graph.</summary>
    GraphSnapshot LoadGraph(int graphId);

    /// <summary>Adds a character at a place on the canvas, answering with their id.</summary>
    int AddCharacter(int graphId, string name, string note, double x, double y);

    /// <summary>Changes what a character is called and what the writer remembers.</summary>
    void UpdateCharacter(int characterId, string name, string note);

    /// <summary>Moves a character's node — the writer's own hand on the canvas.</summary>
    void MoveCharacter(int characterId, double x, double y);

    /// <summary>Removes a character, and every tie that stood on them.</summary>
    void DeleteCharacter(int characterId);

    /// <summary>Draws a tie from one character to another, answering with its id.</summary>
    /// <exception cref="InvalidOperationException">
    /// Either end names no character in that graph — a tie cannot point at
    /// nobody.
    /// </exception>
    int AddRelationship(int graphId, int fromCharacterId, int toCharacterId, string label);

    /// <summary>Changes what a tie says.</summary>
    void UpdateRelationship(int relationshipId, string label);

    /// <summary>Erases one tie.</summary>
    void DeleteRelationship(int relationshipId);

    /// <summary>
    /// Moves every node an arrangement just placed — one write for the
    /// whole arrangement rather than a write per node.
    /// </summary>
    void PlaceAll(int graphId, IReadOnlyList<(int CharacterId, double X, double Y)> placements);
}

/// <summary>
/// One graph as the picker shows it: its name and how much stands in it.
/// </summary>
public sealed record GraphSummary(int Id, string Name, int CharacterCount, int RelationshipCount);

/// <summary>
/// One graph whole: its characters and the ties between them, in the
/// shapes the canvas draws.
/// </summary>
public sealed record GraphSnapshot(
    IReadOnlyList<GraphCharacter> Characters,
    IReadOnlyList<GraphRelationship> Relationships);
