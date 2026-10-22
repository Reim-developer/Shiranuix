namespace Shiranuix.Graphs.Persistence;

/// <summary>
/// The wiki every story's cast is kept in: the characters themselves, and
/// how many nodes stand for them in the maps.
/// </summary>
/// <remarks>
/// <para>
/// The wiki is a thing of its own, not a part of any one graph: the same
/// character may stand in a map here and in a map there, and is one
/// character in all of them. So the package asks for the characters as
/// shapes — a name, a day of birth, a description, a note — and leaves
/// the table they are kept in to whoever holds the store.
/// </para>
/// <para>
/// The store also says when the wiki has changed, so that a map standing
/// open beside it may draw the new name rather than the old one.
/// </para>
/// </remarks>
public interface IWikiStore {
    /// <summary>
    /// The change the wiki hears of: a character made, changed or gone.
    /// </summary>
    event Action? Changed;

    /// <summary>Every character in the wiki, by name.</summary>
    IReadOnlyList<WikiCharacterRow> ListCharacters();

    /// <summary>
    /// Puts a new character in the wiki, answering with their id.
    /// </summary>
    /// <param name="name">What they are called.</param>
    /// <param name="birthDate">The day they were born, as the writer writes it.</param>
    /// <param name="description">What the writer says about them.</param>
    /// <param name="note">Whatever else is worth keeping of them.</param>
    /// <returns>The character's id.</returns>
    int CreateCharacter(string name, string birthDate, string description, string note);

    /// <summary>Changes what the wiki knows about a character.</summary>
    void UpdateCharacter(
        int id,
        string name,
        string birthDate,
        string description,
        string note);

    /// <summary>
    /// Takes a character out of the wiki — and every node standing for them
    /// goes with them, in every map.
    /// </summary>
    void DeleteCharacter(int id);

    /// <summary>
    /// How many nodes stand for a character, across every map: the number
    /// a writer is told before the character is taken away.
    /// </summary>
    /// <param name="id">The character being counted.</param>
    /// <returns>The number of nodes standing for them.</returns>
    int CountStandings(int id);
}

/// <summary>
/// One character as the wiki keeps them: what they are called, when they
/// were born, what the writer says about them, and whatever else.
/// </summary>
/// <param name="Id">The character's row.</param>
/// <param name="Name">What they are called.</param>
/// <param name="BirthDate">The day they were born, as the writer writes it.</param>
/// <param name="Description">What the writer says about them.</param>
/// <param name="Note">Whatever else is worth keeping of them.</param>
public sealed record WikiCharacterRow(
    int Id,
    string Name,
    string BirthDate,
    string Description,
    string Note);
