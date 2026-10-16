using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using Lucide.Avalonia;

namespace Shiranuix.Plugins.UI;

/// <summary>
/// Finds the drawing behind the name a panel gives for its icon.
/// </summary>
/// <remarks>
/// <para>
/// Every icon in the editor comes from one set, Lucide's, and the editor is
/// what owns the drawings. A panel does not hand over path data and hope it
/// looks like the rest of the window; it says a name, and this finds it.
/// </para>
/// <para>
/// Spelling is not a mistake. "FolderTree", "folder-tree" and "folder tree"
/// are one icon, because the name in a plugin should be read by a person and
/// not by a machine: everything that is not a letter or a digit is dropped and
/// the rest is compared without regard to case. A name that is in no set at
/// all draws the question mark, so a typo is a question mark on the tab rather
/// than an empty one - something to notice, not something to guess at.
/// </para>
/// </remarks>
public static class PanelIcons {
    private static readonly FrozenDictionary<string, LucideIconKind> Kinds =
        Enum.GetValues<LucideIconKind>().ToFrozenDictionary(
            kind => Bare(kind.ToString()),
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the icon a name asks for.
    /// </summary>
    /// <param name="name">The icon's name, in any spelling.</param>
    /// <returns>
    /// The icon, or null when there is no name at all - which is a panel that
    /// has chosen to let its glyph speak. A name that names nothing in the set
    /// comes back as the question mark rather than as nothing.
    /// </returns>
    public static LucideIconKind? Resolve(string? name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        return Kinds.TryGetValue(Bare(name), out var kind)
            ? kind
            : LucideIconKind.CircleQuestionMark;
    }

    /// <summary>
    /// The name with everything that is not a letter or a digit taken out and
    /// the rest folded to lower case, so that the ways a person writes one name
    /// all land on the same key.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <returns>The bare name.</returns>
    private static string Bare(string name) {
        var bare = new StringBuilder(name.Length);

        foreach (var letter in name) {
            if (char.IsLetterOrDigit(letter)) {
                bare.Append(char.ToLower(letter, CultureInfo.InvariantCulture));
            }
        }

        return bare.ToString();
    }
}

/// <summary>
/// Turns the icon name on a panel into the icon the tab draws, for markup that
/// binds <see cref="IPluginPanel.Icon"/> straight to a drawing.
/// </summary>
public sealed class PanelIconConverter : IValueConverter {
    /// <summary>The one instance markup uses.</summary>
    public static PanelIconConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) => PanelIcons.Resolve(value as string);

    /// <inheritdoc />
    /// <remarks>
    /// One way: a tab that is already drawn is not the editor's business to
    /// read back out of a binding.
    /// </remarks>
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException("A panel's icon is the editor's to draw.");
}
