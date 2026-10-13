using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Shiranuix.Converters;

/// <summary>
/// Turns the view model's font name into a typeface Avalonia can draw.
/// </summary>
/// <remarks>
/// This exists because the binding system will not do the conversion on its
/// own. <c>FontFamily</c> is a struct with a type converter, but a compiled
/// binding carrying a <c>string</c> simply does not publish into it — the
/// property keeps whatever the control theme gave it, and the document
/// silently renders in the interface font no matter what the picker says.
///
/// Doing the conversion here rather than making the view model hold a
/// <c>FontFamily</c> is the point: a typeface is a UI type, and the view model
/// stays free of them so it can be tested without a running Avalonia
/// application.
/// </remarks>
public sealed class StringToFontFamilyConverter : IValueConverter {
    /// <summary>
    /// The single instance used from XAML.
    /// </summary>
    public static readonly StringToFontFamilyConverter Instance = new();

    /// <summary>
    /// A <see cref="FontFamily"/> for the given name, or unset when there is
    /// no name — leaving the control on its theme default rather than an
    /// empty typeface, which would render nothing.
    /// </summary>
    /// <param name="value">The font name, e.g. "Georgia".</param>
    /// <param name="targetType">Unused.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>The typeface to draw the document in.</returns>
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is string name && !string.IsNullOrWhiteSpace(name)
            ? new FontFamily(name)
            : AvaloniaProperty.UnsetValue;

    /// <summary>
    /// Unimplemented: the view model has nowhere to put a <c>FontFamily</c>.
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
        throw new NotSupportedException("One-way: the view model holds a name, not a typeface.");
}
