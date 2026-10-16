namespace Shiranuix.Plugins;

/// <summary>
/// The shape of the names a plugin is known by: its own id, and the id of each
/// command it contributes.
/// </summary>
/// <remarks>
/// Ids end up in files on disk — the plugin's own directory, its settings, the
/// keyboard bindings table — so they have to survive a round trip through a
/// file system that may not be one that cares about case. That is why the
/// rule is lower case all the way down: <c>word-count</c> is the same id on
/// every machine, and a plugin that reads its own settings on Windows finds
/// what it wrote.
/// </remarks>
public static class PluginId {
    /// <summary>The shortest id worth having.</summary>
    public const int MinLength = 2;

    /// <summary>The longest id this contract will store.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Whether a name is an id this contract accepts.
    /// </summary>
    /// <param name="id">The name to judge, or null.</param>
    /// <returns>
    /// True when it starts with a lower-case letter and carries only
    /// lower-case letters, digits, dots and dashes, within the length caps.
    /// </returns>
    public static bool IsValid(string? id) {
        if (id is null || id.Length is < MinLength or > MaxLength) {
            return false;
        }

        if (!IsLower(id[0]) && !char.IsDigit(id[0])) {
            return false;
        }

        foreach (var character in id) {
            var allowed = IsLower(character)
                || char.IsDigit(character)
                || character is '.' or '-';

            if (!allowed) {
                return false;
            }
        }

        return true;
    }

    private static bool IsLower(char character) =>
        character is >= 'a' and <= 'z';
}
