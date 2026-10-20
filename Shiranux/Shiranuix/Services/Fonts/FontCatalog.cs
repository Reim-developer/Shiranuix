using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace Shiranuix.Services.Fonts;

/// <summary>
/// Reads the installed typefaces from Avalonia's font manager, the first time
/// somebody asks.
/// </summary>
/// <remarks>
/// <para>
/// Asking Windows what typefaces are installed is not a cheap question: it
/// opens the font collection, reads every family's metadata and builds the
/// list - hundreds of families on a machine with Office and a design tool on
/// it. A writer who never opens the font picker should never pay for it, and
/// this is a singleton built while the window is being built, so an eager list
/// would be paid for at startup by everyone.
/// </para>
/// <para>
/// So the list is built on first use and kept: the picker asks once per
/// opening, and the second answer is the same list the first one built.
/// Reading it again would also re-read the disk, which a font installed while
/// the editor was open should not be visible from anyway - the picker was
/// built against what was there when it opened.
/// </para>
/// </remarks>
public sealed class FontCatalog : IFontCatalog {
    private readonly Lock _gate = new();
    private IReadOnlyList<string>? _families;

    /// <inheritdoc />
    public IReadOnlyList<string> GetFamilies() {
        // Two windows can open the picker at once, and two threads must not
        // both go and read every font on the machine.
        lock (_gate) {
            return _families ??= Read();
        }
    }

    /// <summary>
    /// Asks the platform what is installed, and turns the answer into a list a
    /// picker can use.
    /// </summary>
    /// <returns>The family names, once each, in reading order.</returns>
    private static IReadOnlyList<string> Read() {
        // SystemFonts is an IReadOnlyList<FontFamily> over whatever the
        // platform reported. Windows repeats families that ship more than one
        // physical file, and a few report an empty name, so both are filtered
        // before the list reaches a ComboBox.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>();

        foreach (var family in FontManager.Current.SystemFonts) {
            var name = family.Name;
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name)) {
                names.Add(name);
            }
        }

        // Ordinal rather than the current culture's: the list is a set of
        // proper names, and the order should not change with the machine's
        // language settings.
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }
}
