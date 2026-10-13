using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Shiranuix.Views;

/// <summary>
/// Keeps a line-number gutter in step with an editor's scroll position.
/// </summary>
/// <remarks>
/// The editor scrolls inside a ScrollViewer that is part of its own template,
/// so it cannot be named or reached from XAML. This finds that ScrollViewer
/// once the template is applied and mirrors its offset onto the gutter's top
/// margin, so the numbers track the text without the editor knowing they
/// exist.
///
/// The gutter's rest position is read from its own margin when the sync hooks
/// up, which makes the margin in the view the single place the gutter's top
/// inset is named.
/// </remarks>
public static class EditorGutter {
    /// <summary>The gutter that follows an editor's scroll position.</summary>
    public static readonly AttachedProperty<Control?> GutterProperty =
        AvaloniaProperty.RegisterAttached<TextBox, Control?>("Gutter", typeof(EditorGutter));

    /// <summary>Gets the gutter kept in step with the given editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The gutter, if one is set.</returns>
    public static Control? GetGutter(TextBox editor) => editor.GetValue(GutterProperty);

    /// <summary>Sets the gutter to keep in step with the given editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="gutter">The gutter to move as the editor scrolls.</param>
    public static void SetGutter(TextBox editor, Control? gutter) =>
        editor.SetValue(GutterProperty, gutter);

    static EditorGutter() {
        GutterProperty.Changed.AddClassHandler<TextBox>(OnGutterChanged);
    }

    private static void OnGutterChanged(TextBox editor, AvaloniaPropertyChangedEventArgs e) {
        if (e.NewValue is not Control gutter) {
            return;
        }

        // Where the gutter sits when the page is unscrolled: read from the
        // margin the view gave it, so the two are named in one place.
        var rest = gutter.Margin.Top;
        ScrollViewer? scroller = null;

        void Hook(object? sender, object? args) {
            if (scroller is not null) {
                return;
            }

            scroller = editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (scroller is null) {
                // The template is not applied yet; the next layout pass tries
                // again.
                return;
            }

            editor.LayoutUpdated -= Hook;
            scroller.ScrollChanged += OnScrolled;
        }

        void OnScrolled(object? sender, ScrollChangedEventArgs e) {
            // ScrollViewer.Offset is an attached property, read through it.
            var offset = scroller!.GetValue(ScrollViewer.OffsetProperty).Y;
            gutter.Margin = new Thickness(0, rest - offset, 0, 0);
        }

        editor.LayoutUpdated += Hook;
    }
}
