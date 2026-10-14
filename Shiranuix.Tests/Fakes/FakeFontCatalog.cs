using System.Collections.Generic;
using Shiranuix.Services.Fonts;

namespace Shiranuix.Tests.Fakes;

/// <summary>
/// A fixed typeface list standing in for <see cref="FontCatalog"/>.
/// </summary>
/// <remarks>
/// The real catalog reads Avalonia's font manager, which needs a running
/// application. A headless test has none, so it gets this instead — and a
/// short, known list is better anyway, because a test asserting "Segoe UI is
/// in the picker" would otherwise depend on the machine it runs on.
/// </remarks>
public sealed class FakeFontCatalog : IFontCatalog {
    /// <summary>
    /// The families the fake reports, in the order it reports them.
    /// </summary>
    public IReadOnlyList<string> Families { get; set; } = new[] {
        "Segoe UI", "Consolas", "Georgia", "Times New Roman", "Verdana"
    };

    /// <summary>
    /// How many times the catalog was asked for the list. The real one does
    /// platform work, so this is worth keeping visible in a test.
    /// </summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string> GetFamilies() {
        CallCount++;
        return Families;
    }
}
