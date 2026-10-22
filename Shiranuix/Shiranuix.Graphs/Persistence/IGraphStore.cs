namespace Shiranuix.Graphs.Persistence;

/// <summary>
/// What the relationship maps remember: the graphs themselves, the
/// characters standing in them, and the ties drawn between those.
/// </summary>
/// <remarks>
/// <para>
/// The rows this speaks in are plain records rather than whatever the
/// application's database happens to hold: the map draws rows, and the
/// store behind it is free to keep them in SQLite, in memory, or in
/// somebody's notebook — the package asks for shapes, not for tables.
/// </para>
/// <para>
/// Every operation is expected to build its own store and let it go — a
/// store carries state and is not thread-safe, so nothing here holds one
/// between calls.
/// </para>
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
    /// <param name="graphId">The graph they stand in.</param>
    /// <param name="wikiCharacterId">The wiki entry they stand for.</param>
    /// <param name="x">Where they stand, across.</param>
    /// <param name="y">Where they stand, down.</param>
    /// <returns>The node's id.</returns>
    int AddCharacter(int graphId, int wikiCharacterId, double x, double y);

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
    /// Bends a tie's line — the writer's own hand on its curve — either
    /// side of the straight one, in the canvas's own units.
    /// </summary>
    void BendRelationship(int relationshipId, double bend);

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
/// One character as the canvas draws them: what the wiki says about them,
/// and the spot on the paper they were left standing at.
/// </summary>
/// <param name="Id">The node's row.</param>
/// <param name="WikiCharacterId">The wiki entry they stand for.</param>
/// <param name="Name">What they are called, as the wiki has it.</param>
/// <param name="BirthDate">The day they were born, as the writer writes it.</param>
/// <param name="Description">What the writer says about them.</param>
/// <param name="Note">Whatever else is worth keeping of them.</param>
/// <param name="X">Where the node stands, across.</param>
/// <param name="Y">Where the node stands, down.</param>
public sealed record GraphNodeRow(
    int Id,
    int WikiCharacterId,
    string Name,
    string BirthDate,
    string Description,
    string Note,
    double X,
    double Y);

/// <summary>
/// One tie as the canvas draws it: which way it points, under what word,
/// and how far its line is bent from the straight one.
/// </summary>
public sealed record GraphEdgeRow(
    int Id,
    int FromId,
    int ToId,
    string Label,
    double Bend);

/// <summary>
/// One graph whole: its characters and the ties between them, in the
/// shapes the canvas draws.
/// </summary>
public sealed record GraphSnapshot(
    IReadOnlyList<GraphNodeRow> Nodes,
    IReadOnlyList<GraphEdgeRow> Edges);
