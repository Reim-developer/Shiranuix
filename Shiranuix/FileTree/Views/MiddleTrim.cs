using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.VisualTree;

namespace Shiranuix.FileTree.Views;

/// <summary>
/// Cuts a long name in the middle rather than at the end, so both ends survive:
/// the beginning says what it is, the end says what kind of thing it is.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia can only cut a <c>TextBlock</c> at its end, and a file tree cut
/// that way loses the only part of a name that says what the file is - a
/// writer left with <c>Notes for the chapter about instal…</c> cannot tell
/// which chapter. Cutting in the middle keeps both:
/// <c>Notes for the chapter…appendix.md</c>.
/// </para>
/// <para>
/// How much to keep is measured rather than guessed: the name is laid out at
/// its full width and the number of characters that fit is worked out from
/// that, so a row at the fifth level and a row at the top both keep as much as
/// they can. The full name is always kept beside the shortened one, so widening
/// the column brings every character back.
/// </para>
/// <para>
/// The work is asked for by the view rather than done from a property-changed
/// handler: Avalonia finds an attached property through its registry, which
/// never touches the class that registered it, so a handler installed in a
/// static constructor of that class is never installed at all. The view asks,
/// after every layout, and only rows that asked for it are touched.
/// </para>
/// </remarks>
public sealed class MiddleTrim {
    private MiddleTrim() {
        // Avalonia registers an attached property against an owner type, and a
        // static class is not one; this class exists only to be that type.
    }

    /// <summary>
    /// Whether a text block should keep both ends of its text.
    /// </summary>
    public static readonly AttachedProperty<bool> ShortenProperty =
        AvaloniaProperty.RegisterAttached<MiddleTrim, Control, bool>("Shorten");

    /// <summary>
    /// The name as it was written, kept while the shortened one is shown.
    /// </summary>
    private static readonly AttachedProperty<string?> WholeProperty =
        AvaloniaProperty.RegisterAttached<MiddleTrim, Control, string?>("Whole");

    /// <summary>
    /// The width the name was last shortened for, so a row is only measured
    /// again when it has actually changed size.
    /// </summary>
    private static readonly AttachedProperty<double> MeasuredProperty =
        AvaloniaProperty.RegisterAttached<MiddleTrim, Control, double>("Measured");

    /// <summary>Sets whether the control should shorten its text in the middle.</summary>
    /// <param name="control">The control to shorten for.</param>
    /// <param name="value">True to shorten.</param>
    public static void SetShorten(Control control, bool value) =>
        control.SetValue(ShortenProperty, value);

    /// <summary>Gets whether the control shortens its text in the middle.</summary>
    /// <param name="control">The control to ask about.</param>
    /// <returns>True when it shortens.</returns>
    public static bool GetShorten(Control control) => control.GetValue(ShortenProperty);

    /// <summary>
    /// The text a name of a given width is shown as.
    /// </summary>
    /// <param name="whole">The name as written.</param>
    /// <param name="charactersThatFit">How many of its characters fit.</param>
    /// <returns>
    /// The name itself when it all fits, otherwise the middle of it: the first
    /// half, an ellipsis, and the last half - never fewer than one character at
    /// either end, because an ellipsis on its own tells nobody anything.
    /// </returns>
    public static string Shorten(string whole, int charactersThatFit) {
        ArgumentNullException.ThrowIfNull(whole);

        if (charactersThatFit >= whole.Length || charactersThatFit < 4) {
            return whole;
        }

        var head = (charactersThatFit - 1) / 2;
        var tail = charactersThatFit - 1 - head;

        return string.Concat(
            whole.AsSpan(0, head).TrimEnd(),
            "…",
            whole.AsSpan(whole.Length - tail, tail));
    }

    /// <summary>
    /// Shortens every name in a control that asked to be shortened, and puts
    /// back every character of the ones that now have room.
    /// </summary>
    /// <param name="root">The control to walk, or null.</param>
    public static void Trim(Control? root) {
        if (root is null) {
            return;
        }

        foreach (var block in root.GetVisualDescendants().OfType<TextBlock>()) {
            Apply(block);
        }
    }

    /// <summary>
    /// Shortens a text block's text to what fits, if it does not all fit.
    /// </summary>
    /// <param name="block">The block to shorten.</param>
    private static void Apply(TextBlock block) {
        if (!GetShorten(block)) {
            return;
        }

        // The room a name has is the room left in its row, not its own width:
        // a block inside a row of boxes is given exactly the width it asked
        // for, so measuring itself only ever says that it fits.
        if (block.Parent is not Control boxes || boxes.Parent is not Control row) {
            return;
        }

        var whole = block.GetValue(WholeProperty) ?? block.Text ?? string.Empty;
        if (string.Equals(block.Text, whole, StringComparison.Ordinal)) {
            whole = block.Text ?? string.Empty;
            block.SetValue(WholeProperty, whole);
        }

        var offset = block.TranslatePoint(new Point(0, 0), row) ?? default;
        var available = row.Bounds.Width - offset.X - RowPadding;
        if (available <= 0 || whole.Length == 0) {
            return;
        }

        // Measured once per width: a row that has not changed size is not
        // measured again, so laying the tree out does not lay out every name.
        if (Math.Abs(block.GetValue(MeasuredProperty) - available) < 0.5
            && !string.Equals(block.Text, whole, StringComparison.Ordinal)) {
            return;
        }

        block.SetValue(MeasuredProperty, available);

        block.Text = whole;
        block.Measure(Size.Infinity);

        var needed = block.DesiredSize.Width;
        block.Text = needed <= 0 || needed <= available
            ? whole
            : Shorten(whole, (int)(available / (needed / whole.Length)));
    }

    /// <summary>
    /// The gap a row keeps on its right, so a name is never cut to the very
    /// edge of the column.
    /// </summary>
    private const double RowPadding = 10;
}
