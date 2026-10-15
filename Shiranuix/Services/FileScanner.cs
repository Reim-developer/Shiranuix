using Shiranuix.Models;
using Shiranuix.Utilities;

namespace Shiranuix.Services;

/// <summary>
/// Reads a whole file once and says what it holds: words, characters,
/// lines, and the newline convention its breaks follow.
/// </summary>
/// <remarks>
/// The scan streams — the file's size never becomes the size of a string —
/// and asks each character the same questions a writer would: what counts
/// as a word, where the breaks fall, and whether the file has anything in
/// it at all. Its numbers are the baseline the document's counts stand on
/// for every line the window is not showing.
/// </remarks>
public static class FileScanner {
    /// <summary>
    /// Reads a file from its first character and counts what it holds.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The counts and convention of everything in the file.</returns>
    public static ScanResult Scan(string path) {
        using var source = Utf8File.OpenReader(path);

        var words = 0;
        var characters = 0;
        var newlines = 0;
        var inWord = false;
        var sawCrLf = false;
        var sawContent = false;

        Span<char> buffer = stackalloc char[4096];
        int read;

        while ((read = source.Read(buffer)) > 0) {
            for (var index = 0; index < read; index++) {
                var character = buffer[index];
                characters++;
                sawContent = true;

                if (char.IsWhiteSpace(character)) {
                    inWord = false;
                } else if (!inWord) {
                    inWord = true;
                    words++;
                }

                if (character == '\n') {
                    newlines++;
                } else if (character == '\r' && IsLfNext(buffer, index, read, source)) {
                    // Only a pair is the Windows convention: a carriage
                    // return at the end of a line on its own is not one, and
                    // this is the question Newline.Detect asks of a string.
                    sawCrLf = true;
                }
            }
        }

        return new ScanResult(
            words,
            characters,
            sawContent ? newlines + 1 : 0,
            sawCrLf ? Models.Newline.CrLf : Models.Newline.Lf);
    }

    /// <summary>
    /// Reports whether the character after the one at the buffer's
    /// <paramref name="index"/> is a line feed — from the buffer when it is
    /// still holding it, from the stream ahead when the pair straddles the
    /// chunk's end.
    /// </summary>
    private static bool IsLfNext(Span<char> buffer, int index, int read, StreamReader source) =>
        index + 1 < read ? buffer[index + 1] == '\n' : source.Peek() == '\n';
}
