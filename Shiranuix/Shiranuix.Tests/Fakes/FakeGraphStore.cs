using Shiranuix.Graphs.Persistence;
using Shiranuix.Models;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory graph store, so the window's state is tested over memory
/// rather than the database — which the store's own tests already cover.
/// </summary>
/// <remarks>
/// The rules are the real store's rules, copied rather than relaxed: names
/// stand unique, a tie may only join characters standing in its own graph,
/// and deleting a character takes the ties standing on them. A fake that
/// forgave what the real one refuses would test a store nobody ships.
/// </remarks>
public sealed class FakeGraphStore : IGraphStore {
    private readonly Dictionary<int, CharacterGraph> _graphs = new();
    private readonly List<GraphCharacter> _characters = new();
    private readonly List<GraphRelationship> _relationships = new();

    private int _nextGraph = 1;
    private int _nextCharacter = 1;
    private int _nextRelationship = 1;

    /// <summary>
    /// Creates the store over the wiki its characters stand for.
    /// </summary>
    /// <param name="wiki">
    /// The wiki the characters are kept in. A wiki of its own, unless one
    /// is handed in — a map and the wiki beside it must be looking at the
    /// same characters, so a test may hand in the one it already has.
    /// </param>
    public FakeGraphStore(FakeWikiStore? wiki = null) {
        Wiki = wiki ?? new FakeWikiStore();
        Wiki.Graphs = this;
    }

    /// <summary>The wiki the characters are kept in.</summary>
    public FakeWikiStore Wiki { get; }

    /// <inheritdoc />
    public IReadOnlyList<GraphSummary> ListGraphs() =>
        [.. _graphs.Values
            .OrderBy(graph => graph.Name)
            .Select(graph => new GraphSummary(
                graph.Id,
                graph.Name,
                _characters.Count(character => character.GraphId == graph.Id),
                _relationships.Count(relationship => relationship.GraphId == graph.Id)))];

    /// <inheritdoc />
    public int CreateGraph(string name) {
        if (_graphs.Values.Any(graph => graph.Name == name)) {
            throw new InvalidOperationException("A graph under that name already stands.");
        }

        var id = _nextGraph++;
        _graphs[id] = new CharacterGraph { Id = id, Name = name };
        return id;
    }

    /// <inheritdoc />
    public void RenameGraph(int graphId, string name) {
        if (_graphs.Values.Any(graph => graph.Name == name && graph.Id != graphId)) {
            throw new InvalidOperationException("A graph under that name already stands.");
        }

        if (_graphs.TryGetValue(graphId, out var graph)) {
            graph.Name = name;
        }
    }

    /// <inheritdoc />
    public void DeleteGraph(int graphId) {
        _graphs.Remove(graphId);
        _characters.RemoveAll(character => character.GraphId == graphId);
        _relationships.RemoveAll(relationship => relationship.GraphId == graphId);
    }

    /// <inheritdoc />
    public GraphSnapshot LoadGraph(int graphId) =>
        new([.. _characters
                .Where(character => character.GraphId == graphId)
                .OrderBy(character => character.Id)
                .Select(character => {
                    var known = Wiki.Find(character.WikiCharacterId);

                    return new GraphNodeRow(
                        character.Id,
                        character.WikiCharacterId,
                        known?.Name ?? string.Empty,
                        known?.BirthDate ?? string.Empty,
                        known?.Description ?? string.Empty,
                        known?.Note ?? string.Empty,
                        character.X,
                        character.Y);
                })],
            [.. _relationships
                .Where(relationship => relationship.GraphId == graphId)
                .OrderBy(relationship => relationship.Id)
                .Select(relationship => new GraphEdgeRow(
                    relationship.Id,
                    relationship.FromCharacterId,
                    relationship.ToCharacterId,
                    relationship.Label,
                    relationship.Bend))]);

    /// <inheritdoc />
    public int AddCharacter(int graphId, int wikiCharacterId, double x, double y) {
        if (!_graphs.ContainsKey(graphId)) {
            throw new InvalidOperationException("No graph stands under that id.");
        }

        // A node may not stand for a character nobody keeps: the wiki is
        // where the character is, and a node pointing at nothing would be
        // a node with no face.
        if (Wiki.Find(wikiCharacterId) is null) {
            throw new InvalidOperationException("No character in the wiki stands under that id.");
        }

        var id = _nextCharacter++;
        _characters.Add(new GraphCharacter {
            Id = id,
            GraphId = graphId,
            WikiCharacterId = wikiCharacterId,
            X = x,
            Y = y
        });
        return id;
    }

    /// <summary>
    /// Puts a character in the wiki and stands them in a graph — the two
    /// steps a character takes, said together, the way the state says them.
    /// </summary>
    /// <param name="graphId">The graph they stand in.</param>
    /// <param name="name">What they are called.</param>
    /// <param name="note">Whatever else is worth keeping of them.</param>
    /// <param name="x">Where they stand, across.</param>
    /// <param name="y">Where they stand, down.</param>
    /// <returns>The node's id.</returns>
    public int AddWikiCharacter(int graphId, string name, string note, double x, double y) {
        var wikiId = Wiki.CreateCharacter(name, string.Empty, string.Empty, note);
        return AddCharacter(graphId, wikiId, x, y);
    }

    /// <inheritdoc />
    public void MoveCharacter(int characterId, double x, double y) {
        if (Find(characterId) is { } character) {
            character.X = x;
            character.Y = y;
        }
    }

    /// <inheritdoc />
    public void DeleteCharacter(int characterId) {
        _characters.RemoveAll(character => character.Id == characterId);
        _relationships.RemoveAll(relationship =>
            relationship.FromCharacterId == characterId ||
            relationship.ToCharacterId == characterId);
    }

    /// <inheritdoc />
    public int AddRelationship(int graphId, int fromCharacterId, int toCharacterId, string label) {
        var standing = _characters
            .Where(character => character.GraphId == graphId)
            .Select(character => character.Id)
            .ToHashSet();

        if (!standing.Contains(fromCharacterId) || !standing.Contains(toCharacterId)) {
            throw new InvalidOperationException(
                "Both ends of a tie must be characters standing in that graph.");
        }

        var id = _nextRelationship++;
        _relationships.Add(new GraphRelationship {
            Id = id,
            GraphId = graphId,
            FromCharacterId = fromCharacterId,
            ToCharacterId = toCharacterId,
            Label = label
        });
        return id;
    }

    /// <inheritdoc />
    public void UpdateRelationship(int relationshipId, string label) {
        var stored = _relationships
            .FirstOrDefault(relationship => relationship.Id == relationshipId);

        if (stored is not null) {
            stored.Label = label;
        }
    }

    /// <inheritdoc />
    public void DeleteRelationship(int relationshipId) =>
        _relationships.RemoveAll(relationship => relationship.Id == relationshipId);

    /// <inheritdoc />
    public void BendRelationship(int relationshipId, double bend) {
        var stored = _relationships
            .FirstOrDefault(relationship => relationship.Id == relationshipId);

        if (stored is not null) {
            stored.Bend = bend;
        }
    }

    /// <inheritdoc />
    public void PlaceAll(int graphId, IReadOnlyList<(int CharacterId, double X, double Y)> placements) {
        foreach (var (characterId, x, y) in placements) {
            var character = _characters.FirstOrDefault(candidate =>
                candidate.Id == characterId && candidate.GraphId == graphId);

            if (character is not null) {
                character.X = x;
                character.Y = y;
            }
        }
    }

    /// <summary>
    /// How many nodes stand for a wiki character, across every graph.
    /// </summary>
    /// <param name="wikiCharacterId">The wiki character being counted.</param>
    /// <returns>The number of nodes standing for them.</returns>
    internal int CountStandingsOf(int wikiCharacterId) =>
        _characters.Count(character => character.WikiCharacterId == wikiCharacterId);

    /// <summary>
    /// Takes every node standing for a wiki character out of every graph,
    /// and the ties that stood on them with them.
    /// </summary>
    /// <param name="wikiCharacterId">The wiki character whose nodes go.</param>
    internal void RemoveStandingsOf(int wikiCharacterId) {
        var standing = _characters
            .Where(character => character.WikiCharacterId == wikiCharacterId)
            .Select(character => character.Id)
            .ToHashSet();

        _characters.RemoveAll(character => standing.Contains(character.Id));
        _relationships.RemoveAll(relationship =>
            standing.Contains(relationship.FromCharacterId) ||
            standing.Contains(relationship.ToCharacterId));
    }

    private GraphCharacter? Find(int characterId) =>
        _characters.FirstOrDefault(character => character.Id == characterId);
}
