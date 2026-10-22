using Microsoft.EntityFrameworkCore;
using Shiranuix.Graphs.Persistence;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The relationship maps, kept in the application's own SQLite file beside
/// everything else it remembers.
/// </summary>
/// <remarks>
/// The tables hold foreign keys, and SQLite does not stand over them on its
/// own — so the rules a key would keep are rules this store keeps itself:
/// a tie is refused unless both its ends really stand in the graph, and a
/// character's ties are taken with them rather than left pointing at a row
/// that is gone.
/// </remarks>
public sealed class SqliteGraphStore : IGraphStore {
    private readonly IDbContextFactory<ShiranuixContext> _factory;

    /// <summary>
    /// Creates a store over the given context factory.
    /// </summary>
    /// <param name="factory">The factory contexts are built from.</param>
    public SqliteGraphStore(IDbContextFactory<ShiranuixContext> factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <inheritdoc />
    public IReadOnlyList<GraphSummary> ListGraphs() {
        using var context = _factory.CreateDbContext();

        // The counts ride the same read as the names, so the picker says
        // what each graph holds without a second question.
        var rows = context.Graphs
            .AsNoTracking()
            .OrderBy(graph => graph.Name)
            .Select(graph => new {
                graph.Id,
                graph.Name,
                Characters = graph.Characters.Count,
                Relationships = graph.Relationships.Count
            })
            .ToList();

        return [.. rows.Select(row =>
            new GraphSummary(row.Id, row.Name, row.Characters, row.Relationships))];
    }

    /// <inheritdoc />
    public int CreateGraph(string name) {
        ArgumentNullException.ThrowIfNull(name);

        using var context = _factory.CreateDbContext();

        if (context.Graphs.Any(graph => graph.Name == name)) {
            throw new InvalidOperationException("A graph under that name already stands.");
        }

        var now = DateTime.UtcNow;
        var graph = new CharacterGraph {
            Name = name,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        context.Graphs.Add(graph);
        context.SaveChanges();

        return graph.Id;
    }

    /// <inheritdoc />
    public void RenameGraph(int graphId, string name) {
        ArgumentNullException.ThrowIfNull(name);

        using var context = _factory.CreateDbContext();

        if (context.Graphs.Any(other => other.Name == name && other.Id != graphId)) {
            throw new InvalidOperationException("A graph under that name already stands.");
        }

        var stored = context.Graphs.AsTracking().FirstOrDefault(graph => graph.Id == graphId);
        if (stored is not null) {
            stored.Name = name;
            stored.UpdatedUtc = DateTime.UtcNow;
            context.SaveChanges();
        }
    }

    /// <inheritdoc />
    public void DeleteGraph(int graphId) {
        using var context = _factory.CreateDbContext();

        // The characters go by the model's own cascade; the ties are taken
        // here first, because the store reads what it deletes rather than
        // trusting the database's enforcement — SQLite does not stand over
        // the keys unless something asks it to.
        var graph = context.Graphs
            .AsTracking()
            .Include(other => other.Characters)
            .Include(other => other.Relationships)
            .FirstOrDefault(other => other.Id == graphId);

        if (graph is null) {
            return;
        }

        context.GraphRelationships.RemoveRange(graph.Relationships);
        context.GraphCharacters.RemoveRange(graph.Characters);
        context.Graphs.Remove(graph);
        context.SaveChanges();
    }

    /// <inheritdoc />
    public GraphSnapshot LoadGraph(int graphId) {
        using var context = _factory.CreateDbContext();

        // The rows leave as the plain shapes the map draws — an adapter's
        // whole job at the boundary: what SQLite holds of a character is
        // SQLite's business, and what the canvas holds of one is a place
        // and the wiki's own account of them. The name and the rest come
        // with the join, because the wiki is where they are kept.
        var characters = context.GraphCharacters
            .AsNoTracking()
            .Where(character => character.GraphId == graphId)
            .OrderBy(character => character.Id)
            .Select(character => new GraphNodeRow(
                character.Id,
                character.WikiCharacterId,
                character.WikiCharacter!.Name,
                character.WikiCharacter.BirthDate,
                character.WikiCharacter.Description,
                character.WikiCharacter.Note,
                character.X,
                character.Y))
            .ToList();

        var relationships = context.GraphRelationships
            .AsNoTracking()
            .Where(relationship => relationship.GraphId == graphId)
            .OrderBy(relationship => relationship.Id)
            .Select(relationship => new GraphEdgeRow(
                relationship.Id,
                relationship.FromCharacterId,
                relationship.ToCharacterId,
                relationship.Label,
                relationship.Bend))
            .ToList();

        return new GraphSnapshot(characters, relationships);
    }

    /// <inheritdoc />
    public int AddCharacter(int graphId, int wikiCharacterId, double x, double y) {
        using var context = _factory.CreateDbContext();

        if (!context.Graphs.Any(graph => graph.Id == graphId)) {
            throw new InvalidOperationException("No graph stands under that id.");
        }

        // A node may not stand for a character nobody keeps: the wiki is
        // where the character is, and a node pointing at nothing would be
        // a node with no face.
        if (!context.WikiCharacters.Any(character => character.Id == wikiCharacterId)) {
            throw new InvalidOperationException("No character in the wiki stands under that id.");
        }

        var character = new GraphCharacter {
            GraphId = graphId,
            WikiCharacterId = wikiCharacterId,
            X = x,
            Y = y
        };

        context.GraphCharacters.Add(character);
        context.SaveChanges();

        return character.Id;
    }

    /// <inheritdoc />
    public void MoveCharacter(int characterId, double x, double y) {
        using var context = _factory.CreateDbContext();

        var stored = context.GraphCharacters
            .AsTracking()
            .FirstOrDefault(character => character.Id == characterId);

        if (stored is not null) {
            stored.X = x;
            stored.Y = y;
            context.SaveChanges();
        }
    }

    /// <inheritdoc />
    public void DeleteCharacter(int characterId) {
        using var context = _factory.CreateDbContext();

        var stored = context.GraphCharacters
            .AsTracking()
            .FirstOrDefault(character => character.Id == characterId);

        if (stored is null) {
            return;
        }

        // Every tie standing on the character goes with them — those
        // leaving from them and those arriving at them alike, so no arrow
        // is ever left pointing at a row that is gone.
        var ties = context.GraphRelationships
            .AsTracking()
            .Where(relationship =>
                relationship.FromCharacterId == characterId ||
                relationship.ToCharacterId == characterId)
            .ToList();

        context.GraphRelationships.RemoveRange(ties);
        context.GraphCharacters.Remove(stored);
        context.SaveChanges();
    }

    /// <inheritdoc />
    public int AddRelationship(int graphId, int fromCharacterId, int toCharacterId, string label) {
        ArgumentNullException.ThrowIfNull(label);

        using var context = _factory.CreateDbContext();

        // Both ends are asked for by id inside the graph itself: a tie may
        // not borrow a character from another story's cast, and it may not
        // point at a character who was deleted a moment ago.
        var characters = context.GraphCharacters
            .AsNoTracking()
            .Where(character => character.GraphId == graphId)
            .Select(character => character.Id)
            .ToHashSet();

        if (!characters.Contains(fromCharacterId) || !characters.Contains(toCharacterId)) {
            throw new InvalidOperationException(
                "Both ends of a tie must be characters standing in that graph.");
        }

        var relationship = new GraphRelationship {
            GraphId = graphId,
            FromCharacterId = fromCharacterId,
            ToCharacterId = toCharacterId,
            Label = label
        };

        context.GraphRelationships.Add(relationship);
        context.SaveChanges();

        return relationship.Id;
    }

    /// <inheritdoc />
    public void UpdateRelationship(int relationshipId, string label) {
        ArgumentNullException.ThrowIfNull(label);

        using var context = _factory.CreateDbContext();

        var stored = context.GraphRelationships
            .AsTracking()
            .FirstOrDefault(relationship => relationship.Id == relationshipId);

        if (stored is not null) {
            stored.Label = label;
            context.SaveChanges();
        }
    }

    /// <inheritdoc />
    public void DeleteRelationship(int relationshipId) {
        using var context = _factory.CreateDbContext();

        var stored = context.GraphRelationships
            .AsTracking()
            .FirstOrDefault(relationship => relationship.Id == relationshipId);

        if (stored is not null) {
            context.GraphRelationships.Remove(stored);
            context.SaveChanges();
        }
    }

    /// <inheritdoc />
    public void BendRelationship(int relationshipId, double bend) {
        using var context = _factory.CreateDbContext();

        var stored = context.GraphRelationships
            .AsTracking()
            .FirstOrDefault(relationship => relationship.Id == relationshipId);

        if (stored is not null) {
            stored.Bend = bend;
            context.SaveChanges();
        }
    }

    /// <inheritdoc />
    public void PlaceAll(int graphId, IReadOnlyList<(int CharacterId, double X, double Y)> placements) {
        ArgumentNullException.ThrowIfNull(placements);

        using var context = _factory.CreateDbContext();

        // One context, one save: an arrangement of forty nodes is one write
        // rather than forty, and the whole graph lands where the layout put
        // it or nowhere at all.
        var characters = context.GraphCharacters
            .AsTracking()
            .Where(character => character.GraphId == graphId)
            .ToList();

        var byId = characters.ToDictionary(character => character.Id);
        foreach (var (characterId, x, y) in placements) {
            if (byId.TryGetValue(characterId, out var character)) {
                character.X = x;
                character.Y = y;
            }
        }

        context.SaveChanges();
    }
}
