using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Shiranuix.Views;

/// <summary>
/// Hangs the page's underline strips over an editor, and turns them with the
/// page's own setting.
/// </summary>
/// <remarks>
/// <para>
/// The strips draw at the glyphs' coordinates, which the editor only knows
/// inside its template. As with the gutter, the template's presenter is
/// found once the template is applied - on the editor's layout passes - and
/// every pass after that redraws the strips, because each of those passes is
/// a change to where the rows sit: a keystroke, a toggle, a wider window.
/// </para>
/// <para>
/// Scrolling moves the rows without a layout pass, so the template's
/// ScrollViewer is hooked when the strips hang and redraws them on its own
/// tick.
/// </para>
/// </remarks>
public static class EditorUnderline {
    /// <summary>Whether the editor's page draws its underline.</summary>
    public static readonly AttachedProperty<bool> IsOnProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("IsOn", typeof(EditorUnderline));

    private static readonly AttachedProperty<UnderlineStrips?> StripsProperty =
        AvaloniaProperty.RegisterAttached<TextBox, UnderlineStrips?>("Strips", typeof(EditorUnderline));

    /// <summary>Gets whether the given editor's page draws its underline.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns><c>true</c> while the strips are on.</returns>
    public static bool GetIsOn(TextBox editor) => editor.GetValue(IsOnProperty);

    /// <summary>Sets whether the given editor's page draws its underline.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="isOn">Whether the strips are on.</param>
    public static void SetIsOn(TextBox editor, bool isOn) =>
        editor.SetValue(IsOnProperty, isOn);

    static EditorUnderline() {
        IsOnProperty.Changed.AddClassHandler<TextBox>(OnIsOnChanged);
    }

    private static void OnIsOnChanged(TextBox editor, AvaloniaPropertyChangedEventArgs e) {
        // Every layout pass redraws the strips, and the pass that discovers
        // the template hangs them first. Re-adding is harmless: an event
        // removes by target and method, so the same one is not stacked.
        editor.LayoutUpdated -= OnEditorLayout;
        editor.LayoutUpdated += OnEditorLayout;

        if (editor.GetValue(StripsProperty) is { } strips) {
            strips.InvalidateVisual();
        }
    }

    private static void OnEditorLayout(object? sender, object? args) =>
        Hang((TextBox)sender!);

    private static void Hang(TextBox editor) {
        var strips = editor.GetValue(StripsProperty);
        if (strips is null) {
            if (AdornerLayer.GetAdornerLayer(editor) is not { } layer) {
                // The template is not applied yet; the next layout pass
                // tries again.
                return;
            }

            strips = new UnderlineStrips(editor) { IsHitTestVisible = false };
            AdornerLayer.SetAdornedElement(strips, editor);
            AdornerLayer.SetIsClipEnabled(strips, true);
            layer.Children.Add(strips);
            editor.SetValue(StripsProperty, strips);

            // The rows move with the page's scroll, and a scroll is no
            // layout pass; they redraw on the scroller's own tick.
            if (editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scroller) {
                scroller.ScrollChanged += (_, _) => strips.InvalidateVisual();
            }
        }

        strips.InvalidateVisual();
    }
}
