using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Shiranuix.Views;

/// <summary>
/// Draws the page's underline: one strip per row the presenter laid out,
/// just under the row's baseline and no longer than its ink.
/// </summary>
/// <remarks>
/// Avalonia's text presenter takes no text decorations of its own in this
/// version, so the page draws its own. The rows and baselines come straight
/// off the presenter's layout, so the strips agree with the glyphs even as
/// the wrapping, the alignment or the typeface move them - and the strip's
/// own weight follows the point size, as a word processor's underline does.
/// </remarks>
public sealed class UnderlineStrips : Control {
    private readonly TextBox _editor;

    public UnderlineStrips(TextBox editor) => _editor = editor;

    public override void Render(DrawingContext context) {
        if (!_editor.GetValue(EditorUnderline.IsOnProperty)) {
            return;
        }

        var presenter = _editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter?.TextLayout.TextLines is not { Count: > 0 } rows) {
            return;
        }

        // The strips draw in the editor's own coordinates: the offset from
        // the editor's corner to the presenter keeps scroll and padding out
        // of the arithmetic below.
        if (presenter.TranslatePoint(new Point(0, 0), _editor) is not { } origin) {
            return;
        }

        if (presenter.Foreground is not IBrush ink) {
            return;
        }

        var thickness = Math.Max(1.0, presenter.FontSize / 14.0);
        var drop = presenter.FontSize * 0.16 + thickness;
        var pen = new Pen(ink, thickness);

        var top = origin.Y;
        foreach (var row in rows) {
            // The row carries where its ink sits: Start holds the centring
            // or the right-hand pull, and Width ends where the ink ends,
            // trailing spaces outside it.
            var y = top + row.Baseline + drop;
            var left = origin.X + row.Start;
            context.DrawLine(pen, new Point(left, y), new Point(left + row.Width, y));

            top += row.Height;
        }
    }
}
