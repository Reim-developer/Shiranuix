using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Shiranuix.TextRender;

/// <summary>
/// The text presenter the editor's page stands on, knowing how to ask for
/// each of its lines to be laid out at its own edge.
/// </summary>
/// <remarks>
/// <para>
/// The presenter keeps the framework's own presenter whole — it builds
/// the same layout, with the same text, the same wrapping and the same
/// caret bookkeeping. The only thing it adds is the question it stands
/// over that building: which edge each line asks for, answered by
/// whoever set the provider. Because the layout itself comes out with
/// every line already standing where it should, the caret, the selection
/// and a click all land on the letters without this class having to know
/// where they are.
/// </para>
/// <para>
/// With no provider set, the building is the framework's own from end to
/// end — so a presenter with nothing to say draws exactly what it did
/// before this class existed.
/// </para>
/// </remarks>
public sealed class AlignedTextPresenter : TextPresenter {
    /// <summary>
    /// Defines the <see cref="LineAlignmentProvider"/> property.
    /// </summary>
    public static readonly StyledProperty<Func<int, TextAlignment>?> LineAlignmentProviderProperty =
        AvaloniaProperty.Register<AlignedTextPresenter, Func<int, TextAlignment>?>(
            nameof(LineAlignmentProvider));

    /// <summary>
    /// The presenter stands its lines up again when the answer changes:
    /// the layout it already built was built against the old one.
    /// </summary>
    static AlignedTextPresenter() {
        LineAlignmentProviderProperty.Changed.AddClassHandler<AlignedTextPresenter>(
            (presenter, _) => presenter.InvalidateTextLayout());
    }

    /// <summary>
    /// How the line with a given number in the text should stand, or
    /// nothing at all when the page stands at one edge throughout.
    /// </summary>
    public Func<int, TextAlignment>? LineAlignmentProvider {
        get => GetValue(LineAlignmentProviderProperty);
        set => SetValue(LineAlignmentProviderProperty, value);
    }

    /// <summary>
    /// Takes the lines down and has them laid out again against whatever
    /// the provider says now — for the caller that changed what the
    /// provider would answer without changing the provider itself.
    /// </summary>
    public void RefreshLineAlignments() => InvalidateTextLayout();

    /// <summary>
    /// Builds the layout for this presenter's text, standing a per-line
    /// request over the building when there is one to stand.
    /// </summary>
    /// <returns>The layout the framework's own building produced, with
    /// each line at the edge it was given.</returns>
    protected override TextLayout CreateTextLayout() {
        var provider = LineAlignmentProvider;

        if (provider is null) {
            return base.CreateTextLayout();
        }

        using (LineAlignedFormatter.Over(CombinedText(), provider)) {
            return base.CreateTextLayout();
        }
    }

    /// <summary>
    /// The text the layout is about to be built with — the presenter's
    /// own text, with any composition the input method is mid-way
    /// through standing where the caret does, which is the text the
    /// framework itself would lay out.
    /// </summary>
    private string CombinedText() {
        var text = Text ?? string.Empty;
        var preedit = PreeditText;

        if (string.IsNullOrEmpty(preedit)) {
            return text;
        }

        var caret = Math.Clamp(CaretIndex, 0, text.Length);

        return string.Concat(text.AsSpan(0, caret), preedit, text.AsSpan(caret));
    }
}
