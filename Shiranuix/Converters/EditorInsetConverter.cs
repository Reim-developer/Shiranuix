using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Shiranuix.Converters;

/// <summary>
/// Turns the page's line height into the inset the editor keeps inside the
/// sheet: near the gutter on the left, clear of the mark above, and a couple
/// of lines of desk below the last row.
/// </summary>
/// <remarks>
/// The bottom inset follows the line height so the desk under the text is
/// always about two lines tall whatever point size the page carries — a
/// small page draws a small desk, a large one a deep one — and so a settled
/// page never shows a row the sheet has no room to finish. The sides and the
/// top are the page's own geometry and do not follow the type.
/// </remarks>
public sealed class EditorInsetConverter : IValueConverter {
    /// <summary>
    /// The single instance used from XAML.
    /// </summary>
    public static readonly EditorInsetConverter Instance = new();

    /// <summary>
    /// The page's inset: a hand's width by the gutter, the mark's height on
    /// top, and two lines of desk below the last row.
    /// </summary>
    /// <param name="value">The page's line height.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The inset to set.</returns>
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is double lineHeight
            ? new Thickness(14, 44, 60, lineHeight * 2 + 10)
            : value;

    /// <summary>
    /// Unimplemented: the binding is one-way.
    /// </summary>
    /// <param name="value">Unused.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Always throws.</returns>
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        throw new NotSupportedException("One-way: the inset follows the page's line height.");
}
