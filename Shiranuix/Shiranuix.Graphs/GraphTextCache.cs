using System.Globalization;
using Avalonia.Media;

namespace Shiranuix.Graphs;

/// <summary>
/// The words the map writes, measured once and remembered: what they say,
/// how big they are and the ink they are drawn in.
/// </summary>
/// <remarks>
/// Measured afresh every frame, the map would ask the text engine the
/// same question about the same words over and over; remembered, it asks
/// once and draws. The brush is part of the memory, because a brush the
/// theme has replaced would draw the old colour's words however fresh the
/// frame is — so a theme that changes throws the memory away.
/// </remarks>
public sealed class GraphTextCache : IGraphTextMeasurer {
    private readonly Dictionary<string, (IBrush Brush, FormattedText Text)> _words = new();

    /// <inheritdoc />
    public FormattedText Measure(string words, double size, IBrush brush) {
        var key = $"{size}:{brush.GetHashCode()}:{words}";

        if (!_words.TryGetValue(key, out var entry) || !ReferenceEquals(entry.Brush, brush)) {
            entry = (brush, new FormattedText(
                words,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                size,
                brush));
            _words[key] = entry;
        }

        return entry.Text;
    }

    /// <inheritdoc />
    public void Clear() => _words.Clear();
}
