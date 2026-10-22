using Shiranuix.Graphs.Persistence;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// An in-memory wiki, so the cast of a story can be tested over memory
/// rather than the database — which the store's own tests already cover.
/// </summary>
/// <remarks>
/// The rules are the real store's rules, copied rather than relaxed: a
/// character taken out of the wiki takes every node standing for them with
/// them, and the wiki says when it has changed so that a map standing open
/// beside it draws the new name. A fake that forgave what the real one
/// refuses would test a store nobody ships.
/// </remarks>
public sealed class FakeWikiStore : IWikiStore {
    private readonly Dictionary<int, WikiCharacterRow> _characters = new();

    private int _next = 1;

    /// <summary>
    /// The graphs whose nodes stand for the wiki's characters — the fake
    /// store, which is where the nodes are kept.
    /// </summary>
    public FakeGraphStore? Graphs { get; set; }

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<WikiCharacterRow> ListCharacters() =>
        [.. _characters.Values.OrderBy(character => character.Name)];

    /// <inheritdoc />
    public int CreateCharacter(string name, string birthDate, string description, string note) {
        var id = _next++;
        _characters[id] = new WikiCharacterRow(id, name, birthDate, description, note);
        Changed?.Invoke();
        return id;
    }

    /// <inheritdoc />
    public void UpdateCharacter(
        int id,
        string name,
        string birthDate,
        string description,
        string note) {
        if (!_characters.ContainsKey(id)) {
            return;
        }

        _characters[id] = new WikiCharacterRow(id, name, birthDate, description, note);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public void DeleteCharacter(int id) {
        if (!_characters.Remove(id)) {
            return;
        }

        // Every node standing for the character goes with them, in every
        // map they stand in — the real store's rule, kept here too.
        Graphs?.RemoveStandingsOf(id);
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public int CountStandings(int id) => Graphs?.CountStandingsOf(id) ?? 0;

    /// <summary>
    /// The character the fake keeps under an id, for a test that wants to
    /// look at what the wiki holds.
    /// </summary>
    /// <param name="id">The character's row.</param>
    /// <returns>The character, or nothing.</returns>
    public WikiCharacterRow? Find(int id) =>
        _characters.TryGetValue(id, out var character) ? character : null;
}
