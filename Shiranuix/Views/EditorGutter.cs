using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace Shiranuix.Views;

/// <summary>
/// Keeps a line-number gutter in step with an editor: with its scroll
/// position, and with the rows the editor laid its text out in.
/// </summary>
/// <remarks>
/// <para>
/// The editor scrolls inside a ScrollViewer that is part of its own template,
/// so it cannot be named or reached from XAML. This finds that ScrollViewer
/// once the template is applied and mirrors its offset onto the gutter's top
/// margin, so the numbers track the text without the editor knowing they
/// exist.
/// </para>
/// <para>
/// The gutter's rest position is read from its own margin when the sync hooks
/// up, which makes the margin in the view the single place the gutter's top
/// inset is named.
/// </para>
/// <para>
/// The numbers themselves count the file's lines, but a wrapped page draws
/// more rows than it has lines. That difference belongs to the view: the run
/// of numbers arrives through <see cref="NumbersProperty"/>, and every
/// layout pass redraws it against the rows the editor's own layout produced —
/// one number on the row a line starts on, nothing on the rows it wraps
/// over (see <see cref="GutterLabels"/>).
/// </para>
/// </remarks>
public static class EditorGutter {
    /// <summary>The gutter that follows an editor's scroll position.</summary>
    public static readonly AttachedProperty<Control?> GutterProperty =
        AvaloniaProperty.RegisterAttached<TextBox, Control?>("Gutter", typeof(EditorGutter));

    /// <summary>
    /// The run of line numbers the gutter draws, one per line of the window
    /// as the counts worked them out.
    /// </summary>
    public static readonly AttachedProperty<string?> NumbersProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("Numbers", typeof(EditorGutter));

    /// <summary>Gets the gutter kept in step with the given editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The gutter, if one is set.</returns>
    public static Control? GetGutter(TextBox editor) => editor.GetValue(GutterProperty);

    /// <summary>Sets the gutter to keep in step with the given editor.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="gutter">The gutter to move as the editor scrolls.</param>
    public static void SetGutter(TextBox editor, Control? gutter) =>
        editor.SetValue(GutterProperty, gutter);

    /// <summary>Gets the line numbers the given editor's gutter draws.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The run of numbers, if one is set.</returns>
    public static string? GetNumbers(TextBox editor) => editor.GetValue(NumbersProperty);

    /// <summary>Sets the line numbers the given editor's gutter draws.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="numbers">The run of numbers, one per window line.</param>
    public static void SetNumbers(TextBox editor, string? numbers) =>
        editor.SetValue(NumbersProperty, numbers);

    static EditorGutter() {
        GutterProperty.Changed.AddClassHandler<TextBox>(OnGutterChanged);
        NumbersProperty.Changed.AddClassHandler<TextBox>(OnNumbersChanged);
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

        // The labels follow every layout pass, not just the scrolled ones:
        // toggling the wrapping, widening the page or picking another
        // typeface all move where the rows fall, and none of them is a
        // scroll. Re-adding is harmless — an event removes by target and
        // method, so a gutter re-attached does not stack a second pass.
        editor.LayoutUpdated -= OnEditorLayout;
        editor.LayoutUpdated += OnEditorLayout;
        RefreshLabels(editor);
    }

    private static void OnNumbersChanged(TextBox editor, AvaloniaPropertyChangedEventArgs e) =>
        RefreshLabels(editor);

    private static void OnEditorLayout(object? sender, EventArgs e) =>
        RefreshLabels((TextBox)sender!);

    /// <summary>
    /// Redraws the gutter's labels for the rows the editor has laid out.
    /// </summary>
    /// <param name="editor">The editor whose rows the gutter draws.</param>
    /// <remarks>
    /// Written only when a label actually differs: setting the text is itself
    /// a layout change, and an unconditional write would schedule passes
    /// forever. Everything missing — no gutter, no numbers yet, no template —
    /// is left alone, because the next layout pass tries again.
    /// </remarks>
    private static void RefreshLabels(TextBox editor) {
        if (editor.GetValue(GutterProperty) is not TextBlock block) {
            return;
        }

        var numbers = editor.GetValue(NumbersProperty);
        if (string.IsNullOrEmpty(numbers)) {
            return;
        }

        var presenter = editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter?.TextLayout.TextLines is not { Count: > 0 } rows) {
            return;
        }

        // The rows come off the editor's own layout, so they are the rows it
        // drew — same wrapping, same width, same typeface — and the gutter
        // can only ever disagree with them if it stops listening.
        var starts = new int[rows.Count];
        for (var row = 0; row < rows.Count; row++) {
            starts[row] = rows[row].FirstTextSourceIndex;
        }

        var labels = GutterLabels.For(numbers, editor.Text ?? string.Empty, starts);
        if (block.Text != labels) {
            block.Text = labels;
        }
    }
}
