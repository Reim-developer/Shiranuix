namespace Shiranuix.CoreFormat;

/// <summary>
/// How a document is set: the typeface its letters are drawn in, and the
/// point size they are drawn at.
/// </summary>
/// <remarks>
/// <para>
/// The two things a paper says about itself that a machine may not be able
/// to answer — the name of a typeface is a name, and the machine reading the
/// file decides whether it has anything by that name. Carried in its own
/// section so a file without one is an older file rather than a broken one:
/// nothing said means the reader keeps standing on whatever it was standing
/// on.
/// </para>
/// <para>
/// The line spacing is deliberately absent. It is one fixed multiple of the
/// point size everywhere in the app — there is no choice behind it, so there
/// would be nothing to carry; the size travels and the spacing follows it.
/// </para>
/// </remarks>
/// <param name="FontFamily">The typeface the document names.</param>
/// <param name="FontSize">The point size the document is set at.</param>
public sealed record DocumentTypography(string FontFamily, double FontSize);
