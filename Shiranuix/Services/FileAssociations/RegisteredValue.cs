namespace Shiranuix.Services.FileAssociations;

/// <summary>
/// One thing the machine's registry is asked to keep: the key it lives under,
/// the name it goes by there, and what it says.
/// </summary>
/// <remarks>
/// Plain data on purpose. What a machine ends up holding is decided by
/// <see cref="FileAssociationPlan"/> and nothing else, so it can be read,
/// argued with and tested without a registry anywhere near — the writing is
/// the one part that has to touch a real machine, and it is the one part
/// that is never guessed at.
/// </remarks>
/// <param name="Key">
/// The key to create or open, under the current user. Never a machine-wide
/// one: this is a single person's choice about their own files, and it
/// costs no administrator to make.
/// </param>
/// <param name="Name">What it is called inside that key.</param>
/// <param name="Value">
/// What it says, or null for a value with a name and nothing in it — which
/// is how Windows is told that a format can be opened with a program.
/// </param>
public sealed record RegisteredValue(string Key, string Name, string? Value) {
    /// <summary>
    /// Whether this is a name with no contents, rather than something to say.
    /// </summary>
    public bool IsFlag => Value is null;
}
