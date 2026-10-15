namespace Shiranuix.Models;

/// <summary>
/// A place in a file: the byte a stream can seek to, and the character
/// offset everything else counts in.
/// </summary>
/// <remarks>
/// A window of a large file is never reached by reading from the start and
/// counting characters — that is the whole cost the window exists to avoid —
/// so every position the document keeps remembers how to get back to it in
/// both units at once. The byte seeks; the offset is what the text, the
/// ledger and the status bar speak.
///
/// An anchor is only ever produced by walking the file from another anchor
/// that was already exact, so the pair stays consistent for the life of the
/// document.
/// </remarks>
/// <param name="Byte">The first byte of the position, for a stream to seek to.</param>
/// <param name="Offset">How many characters stand before it.</param>
public readonly record struct LineAnchor(long Byte, int Offset);

/// <summary>
/// Where a walk along a file's lines came out.
/// </summary>
/// <remarks>
/// The count is signed with the direction of the walk — a step backwards is
/// negative — and is the number of lines actually moved, which can fall
/// short of the number asked for when the file runs out first.
/// </remarks>
/// <param name="At">The line the walk ended on: its start, or the file's edge.</param>
/// <param name="Lines">How many lines the walk moved, negative when backwards.</param>
public readonly record struct LineMove(LineAnchor At, int Lines);

/// <summary>
/// What a read of whole lines took from the file.
/// </summary>
/// <remarks>
/// The text never ends on the newline that terminates its last line — a
/// trailing newline would draw a line the window does not hold — but every
/// break between the lines is the file's own, exactly as it stands on disk.
/// </remarks>
/// <param name="Text">The lines' text, without the newline ending the last of them.</param>
/// <param name="End">The line after the last one read — where the next window begins.</param>
/// <param name="More">Whether the file says anything after those lines.</param>
public readonly record struct LineRead(string Text, LineMove End, bool More);
