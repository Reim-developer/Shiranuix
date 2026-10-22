namespace Shiranuix.Services.Pdf;

/// <summary>
/// The shelves a machine keeps its typeface files on: the answer to the
/// pen's own question of which file holds a family, in the spelling each
/// system keeps.
/// </summary>
/// <remarks>
/// <para>
/// The question is the same on every system and only the shelf differs —
/// a registry where Windows keeps one, folders where the other systems
/// keep theirs — so the pen asks through this seam and never learns which
/// system it is standing on. Every family is offered under the
/// lower-casing of its own title, the way the resolver asks for it: the
/// plain family for the plain cut, the family with its style after it for
/// a bold or an italic.
/// </para>
/// <para>
/// The spares are the faces this machine is expected to hold when the
/// page's own cannot carry the writing, asked in order until one can.
/// </para>
/// </remarks>
internal interface IFontShelf {
    /// <summary>
    /// The families the shelf holds — each key the lower-cased title, each
    /// value the file that answers it.
    /// </summary>
    IReadOnlyDictionary<string, string> ReadFaces();

    /// <summary>The faces asked in turn when the page's own cannot carry the writing.</summary>
    string[] Spares { get; }
}
