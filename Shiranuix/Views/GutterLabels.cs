namespace Shiranuix.Views;

/// <summary>
/// The labels the gutter draws beside a page: the window's line numbers,
/// spread over the rows the editor actually laid the text out in.
/// </summary>
/// <remarks>
/// <para>
/// The numbers count the file's lines, but a wrapped page is drawn as more
/// rows than it has lines — a long line takes one row per wrapped stretch.
/// A number therefore belongs on the row a line <em>starts</em> on, and the
/// rows a line wraps over take no number at all. Without that spread, every
/// number below the first wrapped line sits beside the wrong text and the
/// page's last rows draw nothing.
/// </para>
/// <para>
/// The row starts come off the editor's own layout, so this only maps them
/// and never measures text itself. A page with no wrapping maps to the
/// numbers it was given — one row per line, unchanged.
/// </para>
/// </remarks>
public static class GutterLabels {
    /// <summary>
    /// Works the label for every row of a laid-out page.
    /// </summary>
    /// <param name="lineNumbers">
    /// The window's run of numbers, newline separated, as the counts worked
    /// them out.
    /// </param>
    /// <param name="text">The window's text, as the editor shows it.</param>
    /// <param name="rowStarts">
    /// The character index each laid-out row starts at, in order.
    /// </param>
    /// <returns>
    /// One label per row, newline separated: the line's number on the row a
    /// line starts on, empty on the rows it wraps over. Numbers past the
    /// last row are dropped, and rows past the last number draw nothing.
    /// </returns>
    public static string For(string lineNumbers, string text, IReadOnlyList<int> rowStarts) {
        // Nothing laid out yet: the numbers are all there is to show, and
        // the next layout pass will spread them once rows exist.
        if (rowStarts.Count == 0) {
            return lineNumbers;
        }

        var numbers = lineNumbers.Split('\n');
        var labels = new string[rowStarts.Count];
        var next = 0;

        for (var row = 0; row < rowStarts.Count; row++) {
            if (BeginsLine(text, rowStarts[row]) && next < numbers.Length) {
                labels[row] = numbers[next];
                next++;
            } else {
                labels[row] = string.Empty;
            }
        }

        return string.Join('\n', labels);
    }

    /// <summary>
    /// Whether a row is where a line begins rather than where a wrapped
    /// continuation picks up: the page's first row, or a row straight after
    /// a newline.
    /// </summary>
    /// <param name="text">The window's text, as the editor shows it.</param>
    /// <param name="start">The character index the row starts at.</param>
    /// <returns>
    /// <see langword="true"/> if the row carries its line's number. A lone
    /// carriage return breaks the layout but not the file's lines, so a row
    /// after one continues the line it broke and the numbering — which
    /// counts newlines, as the counts do — stays where it was.
    /// </returns>
    private static bool BeginsLine(string text, int start) =>
        start <= 0 || (start <= text.Length && text[start - 1] == '\n');
}
