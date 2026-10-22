namespace Shiranuix.Graphs.Dialogs;

/// <summary>
/// The questions the relationship maps ask: a graph's name, a character
/// whole, a tie's two ends and the words it carries, and whether something
/// standing should really be struck out.
/// </summary>
/// <remarks>
/// One service rather than one per question, because the questions are one
/// conversation — the map's own — and every one of them is answered by a
/// dialog made new each ask, the way all the window dialogs are.
/// </remarks>
public interface IGraphDialogService {
    /// <summary>
    /// Asks what a new graph — or a standing one, being renamed — is called.
    /// </summary>
    /// <param name="initial">The name to open on, or nothing for an empty box.</param>
    /// <param name="error">A earlier refusal to show, or nothing.</param>
    /// <returns>The name typed, or null when the writer walked away.</returns>
    Task<string?> AskGraphName(string? initial, string? error);

    /// <summary>
    /// Asks about a character whole: what they are called, when they were
    /// born, what the writer says about them, and anything else worth
    /// keeping of them.
    /// </summary>
    /// <param name="initial">The account to open on, or nothing for an empty box.</param>
    /// <param name="error">An earlier refusal to show, or nothing.</param>
    /// <returns>What was typed, or null when the writer walked away.</returns>
    Task<WikiCharacterDraft?> AskCharacterInformation(WikiCharacterDraft? initial, string? error);

    /// <summary>Asks which tie to draw: its two ends and the words along it.</summary>
    /// <param name="characters">The characters to offer as either end.</param>
    /// <param name="fromId">The end to open on, or nothing.</param>
    /// <param name="toId">The other end to open on, or nothing.</param>
    /// <param name="label">The words to open on, or nothing.</param>
    /// <param name="error">An earlier refusal to show, or nothing.</param>
    /// <returns>What was picked, or null when the writer walked away.</returns>
    Task<RelationshipDraft?> AskRelationship(
        IReadOnlyList<GraphNodeInfo> characters,
        int? fromId,
        int? toId,
        string? label,
        string? error);

    /// <summary>
    /// Asks what a standing tie says now — its two ends read-only above the
    /// one box the words live in.
    /// </summary>
    /// <param name="current">The words to open on.</param>
    /// <param name="fromName">Whose arrow it is that points away from.</param>
    /// <param name="toName">Whose arrow it is that arrives at.</param>
    /// <returns>The words typed, or null when the writer walked away.</returns>
    Task<string?> AskRelationshipLabel(string current, string fromName, string toName);

    /// <summary>Asks whether something standing should really be struck out.</summary>
    /// <param name="message">What is about to go, in the writer's own terms.</param>
    /// <returns>True when the writer said yes.</returns>
    Task<bool> AskConfirm(string message);
}

/// <summary>What the character dialog answered with, as the writer typed it.</summary>
/// <param name="Name">The character's name.</param>
/// <param name="BirthDate">The day they were born, as the writer writes it.</param>
/// <param name="Description">What the writer says about them.</param>
/// <param name="Note">Whatever else is worth keeping of them.</param>
public sealed record WikiCharacterDraft(
    string Name,
    string BirthDate,
    string Description,
    string Note);

/// <summary>What the relationship dialog answered with: the two ends and the words.</summary>
/// <param name="FromId">The character the arrow points away from.</param>
/// <param name="ToId">The character the arrow arrives at.</param>
/// <param name="Label">What the tie says, possibly nothing.</param>
public sealed record RelationshipDraft(int FromId, int ToId, string Label);

/// <summary>One character as the relationship dialog offers them: an id and a name.</summary>
/// <param name="Id">The character's row.</param>
/// <param name="Name">What they are called, for the picker's list.</param>
public sealed record GraphNodeInfo(int Id, string Name);
