using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Shiranuix.Converters;

/// <summary>
/// Turns the italic setting into the editor's font style.
/// </summary>
/// <remarks>
/// A binding is the only way to say it in the view, because the setting is a
/// bool and the property is an enum. The mapping is deliberately one-way: the
/// setting is edited by the format bar's toggle, not by the editor.
/// </remarks>
public sealed class BoolToFontStyleConverter : IValueConverter {
    /// <summary>
    /// The single instance used from XAML.
    /// </summary>
    public static readonly BoolToFontStyleConverter Instance = new();

    /// <summary>
    /// <c>Italic</c> when the italic face is on, <c>Normal</c> when it is off.
    /// </summary>
    /// <param name="value">The italic setting.</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The style to set.</returns>
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is true ? FontStyle.Italic : FontStyle.Normal;

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
