using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Shiranuix.Plugins.UI;

namespace ReadingTime;

/// <summary>
/// A small panel down the left of the page, showing what the last count found.
/// </summary>
/// <remarks>
/// <para>
/// The plugin works without this: on a host that offers no panel door — a test
/// harness, a build check, a future command-line mode — the command still
/// counts, still says so in the status line, and simply has nothing to draw
/// itself on. A panel is an extra, not a requirement.
/// </para>
/// <para>
/// The control is built once, while the plugin starts, and then only written
/// to. That is the whole shape of a panel: the editor mounts it and never
/// reaches inside it again.
/// </para>
/// </remarks>
internal sealed class ReadingTimePanel : IPluginPanel {
    private readonly TextBlock _reading;

    /// <summary>
    /// Builds the panel and its contents.
    /// </summary>
    public ReadingTimePanel() {
        _reading = new TextBlock {
            Text = "No count yet.",
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // The editor's own accent, found the way the editor's own markup finds
        // it: a plugin that picked a colour would be wrong in the dark theme.
        _reading.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Sx.AccentBrush"));

        View = new StackPanel {
            Spacing = 6,
            Margin = new Thickness(10, 16, 10, 0),
            Children = {
                Caption("Reading time"),
                _reading
            }
        };
    }

    /// <inheritdoc />
    public string Id => "reading-time.panel";

    /// <inheritdoc />
    public string Title => "Reading";

    /// <inheritdoc />
    public string? Glyph => "◷";

    /// <inheritdoc />
    public Geometry? Icon => null;

    /// <inheritdoc />
    public Control View { get; }

    /// <summary>
    /// Says what the last count found.
    /// </summary>
    /// <param name="words">How many words the document held.</param>
    /// <param name="minutes">How long that would take to read.</param>
    public void Show(int words, int minutes) {
        _reading.Text = minutes <= 0 ? "—" : $"{minutes} min";
        ToolTip.SetTip(_reading, $"{words} words");
    }

    private static TextBlock Caption(string text) => new() {
        Text = text,
        FontSize = 11,
        Opacity = 0.6,
        HorizontalAlignment = HorizontalAlignment.Center
    };
}
