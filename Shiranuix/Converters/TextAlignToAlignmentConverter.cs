using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Shiranuix.Models;

namespace Shiranuix.Converters;

/// <summary>
/// Turns the alignment setting into the editor's text alignment.
/// </summary>
/// <remarks>
/// A binding is the only way to say it in the view, because the setting is]
/// the model's own <see cref="TextAlign"/> and the property is Avalonia's]
/// <c>TextAlignment</c>. The mapping is deliberately one-way: the setting is]
/// set by the format bar's chips, never by the editor itself.
/// </remarks>
public sealed class TextAlignToAlignmentConverter : IValueConverter {
    /// <summary>
    /// The single instance used from XAML.
    /// </summary>
    public static readonly TextAlignToAlignmentConverter Instance = new();

    /// <summary>
    /// The Avalonia alignment that draws the setting's value.
    /// </summary>
    /// <param name="value">The alignment setting.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The alignment to set on the page.</returns>
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value switch {
            TextAlign.Center => TextAlignment.Center,
            TextAlign.Right => TextAlignment.Right,
            TextAlign.Justify => TextAlignment.Justify,
            _ => TextAlignment.Left
        };

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
        throw new NotSupportedException("One-way: the bar edits the setting itself.");
}
