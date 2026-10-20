namespace Shiranuix.CoreEditor;

/// <summary>
/// One move against the file: where it falls, what was there, and what the
/// text over that span says now.
/// </summary>
/// <remarks>
/// The ledger's entries in the shape the rest of the document reads them:
/// file coordinates that never move, so a window slid a thousand lines away
/// can still ask which moves stand outside it and add their effect to the
/// whole file's counts.
/// </remarks>
/// <param name="Start">The first file character replaced.</param>
/// <param name="Original">The file's own text over the span.</param>
/// <param name="Text">The text the span has now.</param>
public readonly record struct EditSpan(int Start, string Original, string Text);
