using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Shiranuix.TextRender;

/// <summary>
/// A text formatter that lets each line of a layout stand at its own edge
/// instead of the whole page standing at one.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia builds text line by line through a formatter it asks for once
/// and keeps, and every question carries the paragraph's properties — the
/// seam this stands in. The framework's own formatter still does every
/// piece of the work (shaping, wrapping, the line objects themselves); it
/// is simply asked once more per line, with the edge that line was given.
/// Nothing about the line objects changes, so the caret, a dragged
/// selection and a click all land where the letters now stand, and
/// whatever else in the app asks for text to be laid out is answered by
/// the very formatter it would have had.
/// </para>
/// <para>
/// A line is told apart by the index the framework starts it from, so the
/// caller who knows what each line means stands a scope over the building
/// of the layout — naming the text being laid out and the edge each of
/// its lines asks for. The scope lives on the one thread for exactly as
/// long as the building takes, which is synchronous and cannot be
/// interrupted, and every formatter question outside it falls through
/// untouched.
/// </para>
/// </remarks>
public sealed class LineAlignedFormatter : TextFormatter {
    /// <summary>
    /// The formatter every question falls back to — the framework's own,
    /// handed over at the door so that the whole of its behaviour is kept.
    /// </summary>
    private readonly TextFormatter _framework;

    /// <summary>
    /// The request standing over the layout currently being built on this
    /// thread, if a caller asked for one.
    /// </summary>
    [ThreadStatic] private static Scope? _scope;

    /// <summary>
    /// Wraps the framework's formatter rather than replacing its work.
    /// </summary>
    /// <param name="framework">The formatter Avalonia would have used.</param>
    public LineAlignedFormatter(TextFormatter framework) =>
        _framework = framework ?? throw new ArgumentNullException(nameof(framework));

    /// <summary>
    /// Stands a per-line request over layouts built on this thread until
    /// the returned handle is let go.
    /// </summary>
    /// <param name="text">The text the layout is about to be built with,
    /// so that a line's index can be found in it.</param>
    /// <param name="alignmentOfLine">How the line with a given number
    /// should stand.</param>
    /// <returns>A handle whose release takes the request back down.</returns>
    public static IDisposable Over(string text, Func<int, TextAlignment> alignmentOfLine) =>
        new Scope(text, alignmentOfLine);

    /// <inheritdoc cref="TextFormatter.FormatLine(ITextSource, int, double, TextParagraphProperties, TextLineBreak?)"/>
    public override TextLine? FormatLine(
        ITextSource textSource,
        int firstTextSourceIndex,
        double paragraphWidth,
        TextParagraphProperties paragraphProperties,
        TextLineBreak? previousLineBreak = null) =>
        Format(textSource, firstTextSourceIndex, paragraphWidth, paragraphProperties,
            previousLineBreak, textRunCache: null);

    /// <inheritdoc cref="TextFormatter.FormatLine(ITextSource, int, double, TextParagraphProperties, TextLineBreak?, TextRunCache?)"/>
    public override TextLine? FormatLine(
        ITextSource textSource,
        int firstTextSourceIndex,
        double paragraphWidth,
        TextParagraphProperties paragraphProperties,
        TextLineBreak? previousLineBreak,
        TextRunCache? textRunCache) =>
        Format(textSource, firstTextSourceIndex, paragraphWidth, paragraphProperties,
            previousLineBreak, textRunCache);

    /// <summary>
    /// Asks the framework's formatter for a line, having stood its
    /// paragraph properties at the edge the line was given.
    /// </summary>
    /// <remarks>
    /// Nothing happens to a line while no request stands, or while the
    /// line's edge is the one its paragraph already had — which keeps the
    /// replacement invisible to every part of the app that never asked.
    /// </remarks>
    private TextLine? Format(
        ITextSource textSource,
        int firstTextSourceIndex,
        double paragraphWidth,
        TextParagraphProperties paragraphProperties,
        TextLineBreak? previousLineBreak,
        TextRunCache? textRunCache) {
        var alignment = _scope?.AlignmentAt(firstTextSourceIndex);

        if (alignment is null || alignment == paragraphProperties.TextAlignment) {
            return textRunCache is null
                ? _framework.FormatLine(textSource, firstTextSourceIndex, paragraphWidth,
                    paragraphProperties, previousLineBreak)
                : _framework.FormatLine(textSource, firstTextSourceIndex, paragraphWidth,
                    paragraphProperties, previousLineBreak, textRunCache);
        }

        var atItsOwnEdge = new GenericTextParagraphProperties(
            paragraphProperties.FlowDirection,
            alignment.Value,
            paragraphProperties.FirstLineInParagraph,
            paragraphProperties.AlwaysCollapsible,
            paragraphProperties.DefaultTextRunProperties,
            paragraphProperties.TextWrapping,
            paragraphProperties.LineHeight,
            paragraphProperties.Indent,
            paragraphProperties.LetterSpacing);

        return textRunCache is null
            ? _framework.FormatLine(textSource, firstTextSourceIndex, paragraphWidth,
                atItsOwnEdge, previousLineBreak)
            : _framework.FormatLine(textSource, firstTextSourceIndex, paragraphWidth,
                atItsOwnEdge, previousLineBreak, textRunCache);
    }

    /// <summary>
    /// One caller's standing request: the text being laid out, where each
    /// of its lines begins, and the edge each was given.
    /// </summary>
    private sealed class Scope : IDisposable {
        /// <summary>The text index each line of the request begins at.</summary>
        private readonly int[] _lineStarts;

        /// <summary>How the line with a given number should stand.</summary>
        private readonly Func<int, TextAlignment> _alignmentOfLine;

        /// <summary>Whatever request stood before this one, so that
        /// nested building unwinds cleanly.</summary>
        private readonly Scope? _previous;

        /// <summary>Whether the request still stands.</summary>
        private bool _open = true;

        /// <summary>
        /// Counts where each line of the text begins and takes the
        /// request's place until the handle is let go.
        /// </summary>
        /// <param name="text">The text about to be laid out.</param>
        /// <param name="alignmentOfLine">How the line with a given number
        /// should stand.</param>
        public Scope(string text, Func<int, TextAlignment> alignmentOfLine) {
            ArgumentNullException.ThrowIfNull(text);
            _alignmentOfLine = alignmentOfLine ?? throw new ArgumentNullException(nameof(alignmentOfLine));

            var starts = new List<int> { 0 };

            for (var index = 0; index < text.Length; index++) {
                if (text[index] == '\n') {
                    starts.Add(index + 1);
                }
            }

            _lineStarts = starts.ToArray();
            _previous = _scope;
            _scope = this;
        }

        /// <summary>
        /// How the line the given index stands in should be laid out.
        /// </summary>
        /// <param name="textIndex">Where the line begins in the text.</param>
        /// <returns>The edge its caller named, or flush left when the
        /// index stands past every line the text has.</returns>
        public TextAlignment AlignmentAt(int textIndex) {
            var line = 0;

            for (var candidate = 1; candidate < _lineStarts.Length; candidate++) {
                if (_lineStarts[candidate] > textIndex) {
                    break;
                }

                line = candidate;
            }

            var alignment = _alignmentOfLine(line);

            // Justify has no letters in this editor, and the framework
            // would draw a line left if it met it anyway.
            return alignment == TextAlignment.Justify ? TextAlignment.Left : alignment;
        }

        /// <summary>Takes the request back down, leaving what stood before.</summary>
        public void Dispose() {
            if (!_open) {
                return;
            }

            _open = false;
            _scope = _previous;
        }
    }
}
