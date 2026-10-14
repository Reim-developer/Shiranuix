using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace Shiranuix.Services.Fonts;

/// <summary>
/// Reads the installed typefaces from Avalonia's font manager.
/// </summary>
public sealed class FontCatalog : IFontCatalog {
    /// <inheritdoc />
    public IReadOnlyList<string> GetFamilies() {
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

        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        return names;
    }
}
