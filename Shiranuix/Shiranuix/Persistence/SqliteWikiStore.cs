using Microsoft.EntityFrameworkCore;
using Shiranuix.Graphs.Persistence;
using Shiranuix.Models;

namespace Shiranuix.Persistence;

/// <summary>
/// The wiki, kept in the application's own SQLite file beside everything
/// else it remembers: one row per character, whatever graphs they stand in.
/// </summary>
/// <remarks>
/// The tables hold foreign keys, and SQLite does not stand over them on its
/// own — so the rules a key would keep are rules this store keeps itself:
/// a character taken out of the wiki takes every node standing for them
/// with them, so no node is ever left standing in a map for nobody.
/// </remarks>
public sealed class SqliteWikiStore : IWikiStore {
    private readonly IDbContextFactory<ShiranuixContext> _factory;

    /// <summary>
    /// Creates a store over the given context factory.
    /// </summary>
    /// <param name="factory">The factory contexts are built from.</param>
    public SqliteWikiStore(IDbContextFactory<ShiranuixContext> factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<WikiCharacterRow> ListCharacters() {
        using var context = _factory.CreateDbContext();

        // By name, the way the wiki's own list shows them: a character is
        // looked for by what they are called.
        var rows = context.WikiCharacters
            .AsNoTracking()
            .OrderBy(character => character.Name)
            .Select(character => new WikiCharacterRow(
                character.Id,
                character.Name,
                character.BirthDate,
                character.Description,
                character.Note))
            .ToList();

        return rows;
    }

    /// <inheritdoc />
    public int CreateCharacter(string name, string birthDate, string description, string note) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(birthDate);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(note);

        using var context = _factory.CreateDbContext();

        var now = DateTime.UtcNow;
        var character = new WikiCharacter {
            Name = name,
            BirthDate = birthDate,
            Description = description,
            Note = note,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        context.WikiCharacters.Add(character);
        context.SaveChanges();

        Changed?.Invoke();

        return character.Id;
    }

    /// <inheritdoc />
    public void UpdateCharacter(
        int id,
        string name,
        string birthDate,
        string description,
        string note) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(birthDate);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(note);

        using var context = _factory.CreateDbContext();

        var stored = context.WikiCharacters
            .AsTracking()
            .FirstOrDefault(character => character.Id == id);

        if (stored is null) {
            return;
        }

        stored.Name = name;
        stored.BirthDate = birthDate;
        stored.Description = description;
        stored.Note = note;
        stored.UpdatedUtc = DateTime.UtcNow;
        context.SaveChanges();

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void DeleteCharacter(int id) {
        using var context = _factory.CreateDbContext();

        var stored = context.WikiCharacters
            .AsTracking()
            .FirstOrDefault(character => character.Id == id);

        if (stored is null) {
            return;
        }

        // Every node standing for the character goes with them — in every
        // map they stand in — so that no map is left holding a node whose
        // face has been taken away. The database would do this of itself,
        // but the store reads what it deletes rather than trusting the
        // database's enforcement: SQLite does not stand over the keys
        // unless something asks it to.
        var nodes = context.GraphCharacters
            .AsTracking()
            .Where(character => character.WikiCharacterId == id)
            .ToList();

        context.GraphCharacters.RemoveRange(nodes);
        context.WikiCharacters.Remove(stored);
        context.SaveChanges();

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public int CountStandings(int id) {
        using var context = _factory.CreateDbContext();

        return context.GraphCharacters.Count(character => character.WikiCharacterId == id);
    }
}
