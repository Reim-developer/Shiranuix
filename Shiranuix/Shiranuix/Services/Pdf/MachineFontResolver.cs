using PdfSharp.Fonts;

namespace Shiranuix.Services.Pdf;

/// <summary>
/// The typefaces the machine itself carries, answered in the pen's own
/// kind of question: which file holds a family, and where.
/// </summary>
/// <remarks>
/// <para>
/// The pen that draws the PDF is not fluent in the machine's fonts on its
/// own: left to itself it knows a private shelf of a few core faces, and
/// a page set in Georgia would arrive with no face at all. This resolver
/// asks the machine's own shelf — the registry where Windows keeps one,
/// the folders where the other systems keep theirs — the places where
/// each system says which file its families' names belong to. What the
/// sheet offers and what the paper draws in are then one answer rather
/// than two, on every system a release is cut for.
/// </para>
/// <para>
/// Styles arrive by their own names: the shelf keeps "Georgia Bold"
/// beside "Georgia", each a file of its own. Where a family has no
/// separate bold or italic file, the plain face answers anyway and the
/// pen lays the style on itself, so nothing is drawn in a face the
/// machine never carried.
/// </para>
/// </remarks>
internal sealed class MachineFontResolver : IFontResolver {
    /// <summary>The resolver the pen is handed.</summary>
    public static readonly MachineFontResolver Instance = new(FontShelf.Current);

    private readonly IFontShelf _shelf;

    /// <summary>The shelves the machine keeps its typeface files on, read once.</summary>
    private IReadOnlyDictionary<string, string>? _faces;

    /// <summary>
    /// One resolver answers for the process. The pen is handed this
    /// instance rather than building its own, so the shelves are read
    /// once and read the same every time.
    /// </summary>
    /// <param name="shelf">The shelf this machine keeps.</param>
    internal MachineFontResolver(IFontShelf shelf) =>
        _shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));

    /// <summary>
    /// The faces this machine is asked to hold in turn, when the page's
    /// own cannot carry the writing.
    /// </summary>
    internal string[] Spares => _shelf.Spares;

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) {
        _faces ??= _shelf.ReadFaces();

        return _faces.TryGetValue(AskName(familyName, isBold, isItalic), out var styled)
            ? new FontResolverInfo(styled)
            : _faces.TryGetValue(familyName.ToLowerInvariant(), out var plain)
                ? new FontResolverInfo(plain, isBold, isItalic)
                : null;
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName) {
        // The face the shelf named is carried whole: the file's own bytes
        // go into the paper, so the reader's machine does not need to
        // carry the same faces to read the page as it was written.
        return File.Exists(faceName) ? File.ReadAllBytes(faceName) : null;
    }

    /// <summary>
    /// The file that holds a family, as far as the machine's shelves say.
    /// </summary>
    /// <param name="familyName">The family, as the pickers carry it.</param>
    /// <remarks>
    /// One ask of the shelves before the letters are counted in it: whether
    /// a family can carry a document's writing is read off the file the
    /// shelf named.
    /// </remarks>
    internal string? FacePath(string familyName) {
        _faces ??= _shelf.ReadFaces();

        return _faces.TryGetValue(familyName.ToLowerInvariant(), out var path) ? path : null;
    }

    /// <summary>What the shelf would call the asked family and style.</summary>
    private static string AskName(string familyName, bool isBold, bool isItalic) =>
        (familyName
            + (isBold ? " Bold" : string.Empty)
            + (isItalic ? " Italic" : string.Empty))
        .ToLowerInvariant();
}
