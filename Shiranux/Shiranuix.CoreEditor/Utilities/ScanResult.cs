namespace Shiranuix.CoreEditor;

/// <summary>
/// What a whole file's text would say if it were in memory: the counts of a
/// stream read once, without the text.
/// </summary>
/// <remarks>
/// Reading a large file to count its lines defeats the purpose of not
/// reading it, so the counts come straight off the stream — one pass, no
/// text kept. The convention arrives in the same pass, because the editor
/// cannot ask a file it has not read how its lines break.
///
/// The counts mean exactly what <see cref="TextStatistics"/> means over the
/// file's text — the same whitespace words, the same lines-until-the-last
/// newline — so a document that later loads only part of itself still shows
/// the whole file's totals.
/// </remarks>
public readonly record struct ScanResult(int Words, int Characters, int Lines, Newline Newline);
