using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Shiranuix.Converters;

/// <summary>
/// Turns the word-wrap setting into the editor's wrapping mode.
/// </summary>
/// <remarks>
/// A binding is the only way to say it in the view, because the setting is a
/// bool and the property is an enum. The mapping is deliberately one-way: the
/// setting is edited by the format bar's toggle, not by the editor.
/// </remarks>
public sealed class BoolToWrapConverter : IValueConverter {
    /// <summary>
    /// The single instance used from XAML.
    /// </summary>
    public static readonly BoolToWrapConverter Instance = new();

    /// <summary>
    /// <c>Wrap</c> when wrapping is on, <c>NoWrap</c> when it is off.
    /// </summary>
    /// <param name="value">The word-wrap setting.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The wrapping mode to set.</returns>
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

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
        throw new NotSupportedException("One-way: the toggle edits the setting itself.");
}
