using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Shiranuix.Converters;

/// <summary>
/// Renders a point size as the label a size picker should show.
/// </summary>
/// <remarks>
/// Without this the ComboBox would print every size with a trailing ".0" —
/// "17.0" where a person would write "17", and "10.0" where they would write
/// "10". Sizes are whole numbers here, so a whole number is what is shown.
/// </remarks>
public sealed class DoubleToSizeConverter : IValueConverter {
    /// <summary>
    /// The single instance used from XAML.
    /// </summary>
    public static readonly DoubleToSizeConverter Instance = new();

    /// <summary>
    /// Points as a whole number, or the input unchanged when it is not a
    /// number at all.
    /// </summary>
    /// <param name="value">The size to render.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The label to show.</returns>
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is double number ? number.ToString(CultureInfo.InvariantCulture) : value;

    /// <summary>
    /// Unimplemented: the binding is display-only and the picker writes the
    /// numeric value straight through.
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
        throw new NotSupportedException("One-way: the picker edits the numeric value directly.");
}
